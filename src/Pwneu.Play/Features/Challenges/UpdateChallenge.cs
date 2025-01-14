using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Challenges;

/// <summary>
/// Updates a challenge under a specified ID.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class UpdateChallenge
{
    public record Command(
        Guid Id,
        string Name,
        string Description,
        int Points,
        bool DeadlineEnabled,
        DateTime Deadline,
        int MaxAttempts,
        IEnumerable<string> Tags,
        IEnumerable<string> Flags,
        string UserId,
        string UserName) : IRequest<Result>;

    private static readonly Error NotFound = new("UpdateChallenge.NotFound",
        "The challenge with the specified ID was not found");

    private static readonly Error ChallengesLocked = new("UpdateChallenge.ChallengesLocked",
        "Challenges are locked. Cannot delete challenges");

    internal sealed class Handler(
        ApplicationDbContext context,
        IValidator<Command> validator,
        IFusionCache cache,
        ILogger<Handler> logger) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // The admin can bypass the challenge lock protection.
            if (!string.Equals(request.UserName, Consts.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var challengesLocked = await cache.GetOrSetAsync(Keys.ChallengesLocked(), async _ =>
                        await context
                            .GetPlayConfigurationValueAsync<bool>(Consts.ChallengesLocked, cancellationToken),
                    token: cancellationToken);

                if (challengesLocked)
                    return Result.Failure(ChallengesLocked);
            }

            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<Guid>(NotFound);

            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("UpdateChallenge.Validation", validationResult.ToString()));

            var oldChallengePoints = challenge.Points;

            challenge.Name = request.Name;
            challenge.Description = request.Description;
            challenge.Points = request.Points;
            challenge.DeadlineEnabled = request.DeadlineEnabled;
            challenge.Deadline = request.Deadline.ToUniversalTime();
            challenge.MaxAttempts = request.MaxAttempts;
            challenge.Tags = request.Tags.ToList();
            challenge.Flags = request.Flags.ToList();

            context.Update(challenge);

            await context.SaveChangesAsync(cancellationToken);

            var invalidationTasks = new List<Task>
            {
                cache.RemoveAsync(Keys.ChallengeDetails(challenge.Id), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.Flags(challenge.Id), token: cancellationToken).AsTask()
            };

            // There's no need to clear the challenge's category evaluation cache of all users.

            if (oldChallengePoints != request.Points)
            {
                invalidationTasks.Add(cache.InvalidateUserGraphs(cancellationToken));
                invalidationTasks.Add(cache.RemoveAsync(Keys.UserRanks(), token: cancellationToken).AsTask());
                invalidationTasks.Add(cache.RemoveAsync(Keys.TopUsersGraph(), token: cancellationToken).AsTask());
            }

            await Task.WhenAll(invalidationTasks);

            logger.LogInformation(
                "Challenge ({Id}) updated by {UserName} ({UserId})",
                request.Id,
                request.UserName,
                request.UserId);

            var audit = new Audit
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                UserName = request.UserName,
                Action = $"Challenge {request.Id} updated",
                PerformedAt = DateTime.UtcNow
            };

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("challenges/{id:Guid}",
                    async (Guid id, UpdateChallengeRequest request, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var userName = claims.GetLoggedInUserName();
                        if (userName is null) return Results.BadRequest();

                        var command = new Command(id, request.Name, request.Description, request.Points,
                            request.DeadlineEnabled, request.Deadline, request.MaxAttempts, request.Tags,
                            request.Flags, userId, userName);

                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                    })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Challenges));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name)
                .NotEmpty()
                .WithMessage("Challenge name is required.")
                .MaximumLength(100)
                .WithMessage("Challenge name must be 100 characters or less.");

            RuleFor(c => c.Description)
                .NotEmpty()
                .WithMessage("Challenge description is required.")
                .MaximumLength(1000)
                .WithMessage("Challenge description must be 1000 characters or less.");

            RuleFor(c => c.Points)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Points must be greater than or equal to 0.");

            RuleFor(c => c.MaxAttempts)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Max attempts must be greater than or equal to 0.");

            RuleFor(c => c.Flags)
                .NotNull()
                .WithMessage("Flags are required.")
                .NotEmpty()
                .WithMessage("Flags cannot be empty.")
                .Must(flags => flags.All(flag => !string.IsNullOrWhiteSpace(flag)))
                .WithMessage("All flags must be non-empty.");
        }
    }
}