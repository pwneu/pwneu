using System.Threading.RateLimiting;
using Pwneu.Api.Constants;

namespace Pwneu.Api.Extensions;

public static class RateLimitingExtensions
{
    public static TBuilder ConfigureProductionRateLimiter<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(
                RateLimitingPolicies.Fixed,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromSeconds(10),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.ExpensiveRequest,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 2,
                            Window = TimeSpan.FromSeconds(10),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.VerifyEmail,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 3,
                            Window = TimeSpan.FromSeconds(10),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.OnceEveryMinute,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 1,
                            Window = TimeSpan.FromMinutes(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.AntiEmailAbuse,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 6,
                            Window = TimeSpan.FromDays(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.GetUsers,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromSeconds(10),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.Registration,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.ResetPassword,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.FileGeneration,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.GetArtifact,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 2,
                            Window = TimeSpan.FromSeconds(10),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.GetChallenges,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromSeconds(5),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.UseHint,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 2,
                            Window = TimeSpan.FromSeconds(10),
                        }
                    )
            );
        });

        return builder;
    }

    public static TBuilder ConfigureDevelopmentRateLimiter<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(
                RateLimitingPolicies.Fixed,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.ExpensiveRequest,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.VerifyEmail,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.OnceEveryMinute,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.AntiEmailAbuse,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.FileGeneration,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.GetUsers,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.Registration,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.ResetPassword,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.GetArtifact,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.GetChallenges,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );

            options.AddPolicy(
                RateLimitingPolicies.UseHint,
                httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = int.MaxValue,
                            Window = TimeSpan.FromSeconds(1),
                        }
                    )
            );
        });

        return builder;
    }
}
