using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Newtonsoft.Json;
using Pwneu.Play;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Play.Shared.Services;
using Pwneu.Play.Workers;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using QuestPDF.Infrastructure;
using Serilog;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Serilog.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// CORS (Cross-Origin Resource Sharing).
builder.Services.AddCors();

// Postgres Database.
var postgres = builder.Configuration.GetConnectionString(Consts.Postgres) ??
               throw new InvalidOperationException("No Postgres connection found");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(postgres);
});
builder.Services.AddDbContext<BufferDbContext>(options => { options.UseInMemoryDatabase("Buffer"); });

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

builder.Services.AddOutputCache();

var assembly = typeof(AssemblyMarker).Assembly;
const string serviceName = nameof(Pwneu.Play);

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

builder.Services.AddHostedService<SaveSolveBuffersService>();
builder.Services.AddHostedService<SaveSubmissionBuffersService>();
builder.Services.AddHostedService<CacheLeaderboardsService>();

builder.Services.AddScoped<IMemberAccess, MemberAccess>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints(serviceName);

app.ApplyMigrations();

app.UseCors(policy => policy.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin());

await app.Services.SeedPlayConfigurationAsync();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.UseOutputCache();

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
            Service = "Pwneu Play",
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