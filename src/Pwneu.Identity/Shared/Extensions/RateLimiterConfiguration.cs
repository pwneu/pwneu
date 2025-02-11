using System.Threading.RateLimiting;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;

namespace Pwneu.Identity.Shared.Extensions;

public static class RateLimiterConfiguration
{
    public static TBuilder ConfigureRateLimiter<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
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

                options.AddPolicy(Consts.GetUsers, httpContext =>
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

                options.AddPolicy(Consts.GetUsers, httpContext =>
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

        return builder;
    }
}