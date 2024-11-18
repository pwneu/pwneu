using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.BlacklistedEmails;

public static class RemoveBlacklistedEmail
{
    public record Command(Guid Id) : IRequest<Result>;

    private static readonly Error NotFound = new("RemoveBlacklistedEmail.NotFound",
        "The specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var blacklistedEmail = await context
                .BlacklistedEmails
                .Where(a => a.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (blacklistedEmail is null) return Result.Failure(NotFound);

            context.BlacklistedEmails.Remove(blacklistedEmail);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(Keys.BlacklistedEmails(), token: cancellationToken);

            logger.LogInformation("Blacklisted email removed: {Email}", blacklistedEmail.Email);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("blacklist/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(BlacklistedEmails));
        }
    }
}