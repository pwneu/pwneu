using System.Threading.RateLimiting;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;

namespace Pwneu.Chat.Shared.Extensions;

public static class RateLimiterConfiguration
{
    public static TBuilder ConfigureRateLimiter<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(Consts.Conversation, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.GetLoggedInUserId<string>(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 1,
                        Window = TimeSpan.FromSeconds(7),
                    }));
        });

        return builder;
    }
}