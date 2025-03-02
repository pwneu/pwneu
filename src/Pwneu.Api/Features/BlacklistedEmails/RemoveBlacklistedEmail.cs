using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.BlacklistedEmails;

public static class RemoveBlacklistedEmail
{
    public record Command(Guid Id) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "RemoveBlacklistedEmail.NotFound",
        "The specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var blacklistedEmail = await context
                .BlacklistedEmails.Where(a => a.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (blacklistedEmail is null)
                return Result.Failure(NotFound);

            context.BlacklistedEmails.Remove(blacklistedEmail);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(CacheKeys.BlacklistedEmails(), token: cancellationToken);

            logger.LogInformation("Blacklisted email removed: {Email}", blacklistedEmail.Email);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete(
                    "identity/blacklist/{id:Guid}",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Command(id);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(BlacklistedEmails));
        }
    }
}
