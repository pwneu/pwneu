using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Hints;

public class UseHint
{
    public record Command(string UserId, Guid HintId) : IRequest<Result<string>>;

    private static readonly Error NotFound = new("UseHint.NotFound",
        "The hint with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            var hint = await cache.GetOrSetAsync(Keys.Hint(request.HintId), async _ =>
                await context
                    .Hints
                    .Where(h => h.Id == request.HintId)
                    .Select(h => h.Content)
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            if (hint is null)
                return Result.Failure<string>(NotFound);

            var alreadyUsedHint = await context
                .HintUsages
                .AnyAsync(hu => hu.UserId == request.UserId &&
                                hu.HintId == request.HintId,
                    cancellationToken);

            if (alreadyUsedHint)
                return hint;

            var hintUsage = new HintUsage
            {
                UserId = request.UserId,
                HintId = request.HintId,
                UsedAt = DateTime.UtcNow
            };

            context.Add(hintUsage);

            await context.SaveChangesAsync(cancellationToken);

            // TODO -- Update cache on user evaluation on the hint's category

            return hint;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("hints/{id:Guid}", async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.BadRequest();

                    var query = new Command(userId, id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.MemberOnly)
                .WithTags(nameof(Hints));
        }
    }
}