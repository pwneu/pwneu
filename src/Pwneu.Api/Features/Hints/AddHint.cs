using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Hints;

public static class AddHint
{
    public record Command(
        Guid ChallengeId,
        string Content,
        int Deduction,
        string UserId,
        string UserName
    ) : IRequest<Result<Guid>>;

    private static readonly Error NoChallenge = new("AddHint.NoChallenge", "No challenge found");

    private static readonly Error ChallengesLocked = new(
        "AddChallengeHint.ChallengesLocked",
        "Challenges are locked. Cannot add hints"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            // The admin can bypass the challenge lock protection.
            if (!string.Equals(request.UserName, Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var challengesLocked = await cache.CheckIfChallengesAreLockedAsync(
                    context,
                    cancellationToken
                );

                if (challengesLocked)
                    return Result.Failure<Guid>(ChallengesLocked);
            }

            var challenge = await context
                .Challenges.Where(c => c.Id == request.ChallengeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<Guid>(NoChallenge);

            var hint = Hint.Create(request.ChallengeId, request.Content, request.Deduction);

            context.Add(hint);

            var audit = Audit.Create(request.UserId, request.UserName, $"Hint {hint.Id} added");

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(
                CacheKeys.ChallengeDetails(challenge.Id),
                token: cancellationToken
            );

            logger.LogInformation(
                "Hint ({HintId}) added on challenge {ChallengeId} by {UserName} ({UserId})",
                hint.Id,
                request.ChallengeId,
                request.UserName,
                request.UserId
            );

            return hint.Id;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "play/challenges/{id:Guid}/hints",
                    async (
                        Guid id,
                        AddHintRequest request,
                        ClaimsPrincipal claims,
                        ISender sender
                    ) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var userName = claims.GetLoggedInUserName();
                        if (userName is null)
                            return Results.BadRequest();

                        var command = new Command(
                            id,
                            request.Content,
                            request.Deduction,
                            userId,
                            userName
                        );

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Hints));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Content)
                .NotEmpty()
                .WithMessage("Challenge description is required.")
                .MaximumLength(300)
                .WithMessage("Challenge description must be 300 characters or less.");

            RuleFor(c => c.Deduction)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Deduction must be greater than or equal to 0.");
        }
    }
}
