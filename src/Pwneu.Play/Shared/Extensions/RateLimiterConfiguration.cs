using System.Threading.RateLimiting;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;

namespace Pwneu.Play.Shared.Extensions;

public static class RateLimiterConfiguration
{
    public static TBuilder ConfigureRateLimiter<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(Consts.Fixed, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromSeconds(10),
                    }));

            options.AddPolicy(Consts.Download, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 2,
                        Window = TimeSpan.FromSeconds(3),
                    }));

            options.AddPolicy(Consts.Challenges, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromSeconds(5),
                    }));

            options.AddPolicy(Consts.UseHint, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 2,
                        Window = TimeSpan.FromSeconds(10),
                    }));

            // In development, set very high limits to effectively disable rate limiting.
            if (builder.Environment.IsDevelopment())
            {
                options.AddPolicy(Consts.Generate, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }));
            }
            // Actual rate limiting for production environment.
            else
            {
                options.AddPolicy(Consts.Generate, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                        }));
            }
        });

        return builder;
    }
}