using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Pwneu.Api;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Features.Announcements;
using Pwneu.Api.Options;
using Pwneu.Api.Services;
using Pwneu.ServiceDefaults;
using QuestPDF.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Threading.Channels;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();

builder.Host.UseSerilog();

builder
    .Services.AddOptions<AppOptions>()
    .BindConfiguration(nameof(AppOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<JwtOptions>()
    .BindConfiguration(nameof(JwtOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<ChatOptions>()
    .BindConfiguration(nameof(ChatOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<SmtpOptions>()
    .BindConfiguration(nameof(SmtpOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddCors();
builder.Services.AddHttpClient();
builder.Services.AddOutputCache();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<ITurnstileValidator, TurnstileValidator>();
builder.Services.AddSingleton<IChallengePointsConcurrencyGuard, ChallengePointsConcurrencyGuard>();
builder.Services.AddHostedService<SaveBuffersService>();
builder.Services.AddHostedService<RecalculateLeaderboardsService>();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "oauth2",
        new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
        }
    );
    options.OperationFilter<SecurityRequirementsOperationFilter>();
});

builder.Services.AddSingleton<Channel<RecalculateRequest>>(_ =>
    Channel.CreateBounded<RecalculateRequest>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            AllowSynchronousContinuations = true,
        }
    )
);

builder.Services.AddOpenAIChatCompletion(
    "gpt-4o-mini",
    builder.Configuration[BuilderConfigurations.ChatOptionsOpenAIApiKey]!
);

if (builder.Environment.IsProduction())
    builder.ConfigureProductionRateLimiter();
else
    builder.ConfigureDevelopmentRateLimiter();

builder.AddNpgsqlDbContext<AppDbContext>("pwneudb");
builder.Services.AddDbContext<BufferDbContext>(options =>
{
    options.UseInMemoryDatabase("Buffer");
});

builder
    .Services.AddFusionCache()
    .WithDefaultEntryOptions(new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(1) })
    .WithSerializer(new FusionCacheSystemTextJsonSerializer());

builder
    .Services.AddIdentity<User, IdentityRole>(x =>
    {
        x.User.RequireUniqueEmail = true;

        // Email confirmation and account confirmation.
        x.SignIn.RequireConfirmedEmail = true;
        x.SignIn.RequireConfirmedAccount = true;

        // Password requirements.
        x.Password.RequireDigit = true;
        x.Password.RequireLowercase = true;
        x.Password.RequireUppercase = true;
        x.Password.RequireNonAlphanumeric = true;
        x.Password.RequiredLength = 12;

        x.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Password Reset Token Expiration.
builder.Services.Configure<DataProtectionTokenProviderOptions>(x =>
{
    x.TokenLifespan = TimeSpan.FromHours(2);
});

builder
    .Services.AddAuthentication(x =>
    {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(0),
            ValidIssuer = builder.Configuration[BuilderConfigurations.JwtOptionsIssuer],
            ValidAudience = builder.Configuration[BuilderConfigurations.JwtOptionsAudience],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration[BuilderConfigurations.JwtOptionsSigningKey]!
                )
            ),
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (
                    !string.IsNullOrEmpty(accessToken)
                    && path.StartsWithSegments("/api/v1/announcements")
                )
                    context.Token = accessToken;

                return Task.CompletedTask;
            },
        };
    });

builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        AuthorizationPolicies.AdminOnly,
        policy =>
        {
            policy.RequireRole(Roles.Admin);
        }
    )
    .AddPolicy(
        AuthorizationPolicies.ManagerAdminOnly,
        policy =>
        {
            policy.RequireRole(Roles.Manager);
        }
    )
    .AddPolicy(
        AuthorizationPolicies.MemberOnly,
        policy =>
        {
            policy.RequireRole(Roles.Member);
        }
    );

var assembly = typeof(AssemblyMarker).Assembly;

builder.Services.AddMediatR(x => x.RegisterServicesFromAssembly(assembly));
builder.Services.AddValidatorsFromAssembly(assembly);
builder.Services.AddV1Endpoints(assembly);
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumers(assembly);
    x.UsingInMemory(
        (context, configurator) =>
        {
            configurator.ConfigureEndpoints(context);
        }
    );
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.ApplyMigrations();

await app.Services.SeedRolesAsync();
await app.Services.SeedAdminAsync();
await app.Services.SeedConfigurationAsync();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.UseOutputCache();

app.MapHub<AnnouncementHub>("/api/v1/announcements");

app.MapV1Endpoints();

app.UseExceptionHandler();

var flag = builder.Configuration[BuilderConfigurations.AppOptionsFlag];

app.MapGet("/flag", () => "This is a GET request!\n");
app.MapPost("/flag", () => $"{flag}\n");

app.Run();

public partial class Program;
