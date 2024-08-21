using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Pwneu.Play.IntegrationTests.Shared;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Services;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace Pwneu.Play.IntegrationTests;

public class IntegrationTestsWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:latest")
        .WithDatabase("pwneu")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:latest")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(IMemberAccess));
            services.AddScoped<IMemberAccess, MockMemberAccess>();

            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(_dbContainer.GetConnectionString());
            });

            services.RemoveAll(typeof(IFusionCache));
            services.AddFusionCache()
                .WithDefaultEntryOptions(new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(2) })
                .WithSerializer(new FusionCacheNewtonsoftJsonSerializer(new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                }))
                .WithDistributedCache(new RedisCache(new RedisCacheOptions
                    { Configuration = _redisContainer.GetConnectionString() }));

            services.RemoveMassTransitHostedService();
            services.AddMassTransitTestHarness();
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _redisContainer.StopAsync();
    }
}