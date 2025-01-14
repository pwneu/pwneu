using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Identity.Shared.Options;
using Pwneu.Identity.Shared.Services;
using Pwneu.Identity.Workers;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using Serilog;
using Swashbuckle.AspNetCore.Filters;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

var builder = WebApplication.CreateBuilder(args);

// Serilog.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// App options.
builder.Services
    .AddOptions<AppOptions>()
    .BindConfiguration($"{nameof(AppOptions)}")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// OpenTelemetry.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(nameof(Pwneu.Identity)))
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddPrometheusExporter();
    });

builder.Services.AddHttpClient();

// JWT Options.
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration($"{nameof(JwtOptions)}")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Swagger.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    options.OperationFilter<SecurityRequirementsOperationFilter>();
});

// CORS (Cross-Origin Resource Sharing).
builder.Services.AddCors();

// ASP.NET Identity.
builder.Services.AddIdentity<User, IdentityRole>(options =>
    {
        // Email confirmation and account confirmation.
        options.SignIn.RequireConfirmedEmail = true;
        options.SignIn.RequireConfirmedAccount = true;

        // Password requirements.
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 12;

        options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Password Reset Token Expiration.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(2);
});

if (builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<UserCleanupService>();

// Postgres Database.
var postgres = builder.Configuration.GetConnectionString(Consts.Postgres) ??
               throw new InvalidOperationException("No Postgres connection found");

builder.Services.AddDbContext<ApplicationDbContext>(options => { options.UseNpgsql(postgres); });

// Redis Caching.
var redis = builder.Configuration.GetConnectionString(Consts.Redis) ??
            throw new InvalidOperationException("No Redis connection found");

builder.Services.AddFusionCache()
    .WithDefaultEntryOptions(new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(2) })
    .WithSerializer(new FusionCacheNewtonsoftJsonSerializer(new JsonSerializerSettings
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    }))
    .WithDistributedCache(new RedisCache(new RedisCacheOptions { Configuration = redis }));

var assembly = typeof(Program).Assembly;

// RabbitMQ.
builder.Services.AddMassTransit(busConfigurator =>
{
    busConfigurator.SetKebabCaseEndpointNameFormatter();
    busConfigurator.AddConsumers(assembly);
    busConfigurator.UsingRabbitMq((context, configurator) =>
    {
        configurator.Host(new Uri(builder.Configuration[Consts.MessageBrokerHost]!), h =>
        {
            h.Username(builder.Configuration[Consts.MessageBrokerUsername]!);
            h.Password(builder.Configuration[Consts.MessageBrokerPassword]!);
        });
        configurator.ConfigureEndpoints(context);
    });
});

// Assembly scanning of Mediator and Fluent Validations.
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
builder.Services.AddValidatorsFromAssembly(assembly);

// Add endpoints from the Features folder (Vertical Slice).
builder.Services.AddEndpoints(assembly);

// Authentication and Authorization (JSON Web Token).
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
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
            ValidIssuer = builder.Configuration[Consts.JwtOptionsIssuer],
            ValidAudience = builder.Configuration[Consts.JwtOptionsAudience],
            IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration[Consts.JwtOptionsSigningKey]!)),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Consts.AdminOnly, policy => { policy.RequireRole(Consts.Admin); })
    .AddPolicy(Consts.ManagerAdminOnly, policy => { policy.RequireRole(Consts.Manager); })
    .AddPolicy(Consts.MemberOnly, policy => { policy.RequireRole(Consts.Member); });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(Consts.VerifyEmail, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers[Consts.CfConnectingIp].ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromSeconds(10),
            }));

    options.AddPolicy(Consts.ChangePassword, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers[Consts.CfConnectingIp].ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromMinutes(1),
            }));

    // In development, set very high limits to effectively disable rate limiting.
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy(Consts.AntiEmailAbuse, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Request.Headers[Consts.CfConnectingIp].ToString(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = int.MaxValue,
                    Window = TimeSpan.FromSeconds(1),
                }));

        options.AddPolicy(Consts.Generate, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = int.MaxValue,
                    Window = TimeSpan.FromSeconds(1),
                }));

        options.AddPolicy(Consts.Fixed, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = int.MaxValue,
                    Window = TimeSpan.FromSeconds(1),
                }));

        options.AddPolicy(Consts.Registration, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Request.Headers[Consts.CfConnectingIp].ToString(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = int.MaxValue,
                    Window = TimeSpan.FromSeconds(1),
                }));

        options.AddPolicy(Consts.ResetPassword, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Request.Headers[Consts.CfConnectingIp].ToString(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = int.MaxValue,
                    Window = TimeSpan.FromSeconds(1),
                }));
    }
    // Actual rate limiting for production environment.
    else
    {
        options.AddPolicy(Consts.AntiEmailAbuse, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Request.Headers[Consts.CfConnectingIp].ToString(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 6,
                    Window = TimeSpan.FromDays(1),
                }));

        options.AddPolicy(Consts.Generate, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                }));

        options.AddPolicy(Consts.Fixed, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromSeconds(10),
                }));

        options.AddPolicy(Consts.Registration, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Request.Headers[Consts.CfConnectingIp].ToString(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                }));

        options.AddPolicy(Consts.ResetPassword, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Request.Headers[Consts.CfConnectingIp].ToString(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                }));
    }
});

builder.Services.AddScoped<IAccessControl, AccessControl>();
builder.Services.AddScoped<ITurnstileValidator, TurnstileValidator>();

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/healthz");

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.ApplyMigrations();

app.UseCors(policy => policy.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin());

await app.Services.SeedRolesAsync();
await app.Services.SeedAdminAsync();

await app.Services.SeedIdentityConfigurationAsync();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
    app.MapGet("/api", async context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        var forwardedForHeader = context.Request.Headers["X-Forwarded-For"].ToString();
        var forwardedProtoHeader = context.Request.Headers["X-Forwarded-Proto"].ToString();
        var forwardedHostHeader = context.Request.Headers["X-Forwarded-Host"].ToString();
        var cfConnectingIp = context.Request.Headers[Consts.CfConnectingIp].ToString();

        var response = new
        {
            Service = "Pwneu Identity",
            ClientIp = clientIp,
            ForwardedFor = forwardedForHeader,
            ForwardedProto = forwardedProtoHeader,
            ForwardedHost = forwardedHostHeader,
            CfConnectingIp = cfConnectingIp
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    });

app.MapEndpoints();

app.Run();

public partial class Program;