using System.Text;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using Swashbuckle.AspNetCore.Filters;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry (for metrics, traces, and logs)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Pwneu"))
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    });

builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter());

// Swagger UI
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

// CORS (Cross-Origin Resource Sharing)
builder.Services.AddCors();

// Postgres Database 
var postgres = builder.Configuration.GetConnectionString(Consts.Postgres) ??
               throw new InvalidOperationException("No Postgres connection found");

builder.Services.AddDbContext<ApplicationDbContext>(options => { options.UseNpgsql(postgres); });

// Redis Caching
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

// RabbitMQ
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

// Assembly scanning of Mediator and Fluent Validations
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
builder.Services.AddValidatorsFromAssembly(assembly);

// Add endpoints from the Features folder (Vertical Slice)
builder.Services.AddEndpoints(assembly);

// Authentication and Authorization (JSON Web Token)
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

builder.Services.AddScoped<IMemberAccess, MemberAccess>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.ApplyMigrations();

app.UseCors(corsPolicy =>
    corsPolicy
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowAnyOrigin());

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapGet("/", async context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        var forwardedForHeader = context.Request.Headers["X-Forwarded-For"].ToString();
        var forwardedProtoHeader = context.Request.Headers["X-Forwarded-Proto"].ToString();
        var forwardedHostHeader = context.Request.Headers["X-Forwarded-Host"].ToString();

        var response = new
        {
            Service = "Pwneu Play",
            ClientIp = clientIp,
            ForwardedFor = forwardedForHeader,
            ForwardedProto = forwardedProtoHeader,
            ForwardedHost = forwardedHostHeader
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    });

app.MapEndpoints();

app.Run();

public partial class Program;