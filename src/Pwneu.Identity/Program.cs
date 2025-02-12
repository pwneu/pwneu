using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Newtonsoft.Json;
using Pwneu.Identity;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Identity.Shared.Options;
using Pwneu.Identity.Shared.Services;
using Pwneu.Identity.Workers;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using Serilog;
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

// JWT Options.
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration($"{nameof(JwtOptions)}")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient();

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

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(postgres);
});

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

var assembly = typeof(AssemblyMarker).Assembly;
const string serviceName = nameof(Pwneu.Identity);

// Assembly scanning of Mediator and Fluent Validations.
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
builder.Services.AddValidatorsFromAssembly(assembly);

// Add endpoints from the Features folder (Vertical Slice).
builder.Services.AddEndpoints(assembly);

builder.ConfigureServiceDefaults(serviceName);
builder.ConfigureMessageBroker(assembly);
builder.ConfigureSwagger();
builder.ConfigureAuth();
builder.ConfigureRateLimiter();

builder.Services.AddScoped<IAccessControl, AccessControl>();
builder.Services.AddScoped<ITurnstileValidator, TurnstileValidator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints(serviceName);

app.ApplyMigrations();

app.UseCors(policy => policy.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin());

await app.Services.SeedRolesAsync();
await app.Services.SeedAdminAsync();

await app.Services.SeedIdentityConfigurationAsync();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapEndpoints();

app.Run();

public partial class Program;