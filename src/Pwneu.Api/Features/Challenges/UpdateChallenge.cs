using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Services;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

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
        string UserName
    ) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "UpdateChallenge.NotFound",
        "The challenge with the specified ID was not found"
    );

    private static readonly Error ChallengesLocked = new(
        "UpdateChallenge.ChallengesLocked",
        "Challenges are locked. Cannot update challenges"
    );

    internal sealed class Handler(
        AppDbContext context,
        IValidator<Command> validator,
        IFusionCache cache,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(
                    new Error("UpdateChallenge.Validation", validationResult.ToString())
                );

            // The admin can bypass the challenge lock protection.
            if (!string.Equals(request.UserName, Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var challengesLocked = await cache.GetOrSetAsync(
                    CacheKeys.ChallengesLocked(),
                    async _ =>
                        await context.GetConfigurationValueAsync<bool>(
                            ConfigurationKeys.ChallengesLocked,
                            cancellationToken
                        ),
                    token: cancellationToken
                );

                if (challengesLocked)
                    return Result.Failure(ChallengesLocked);
            }

            var challenge = await context
                .Challenges.Where(c => c.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<Guid>(NotFound);

            challenge.Name = request.Name;
            challenge.Description = request.Description;
            challenge.Points = request.Points;
            challenge.DeadlineEnabled = request.DeadlineEnabled;
            challenge.Deadline = request.Deadline.ToUniversalTime();
            challenge.MaxAttempts = request.MaxAttempts;
            challenge.Tags = request.Tags.ToList();
            challenge.Flags = request.Flags.ToList();

            context.Update(challenge);

            var audit = Audit.Create(
                request.UserId,
                request.UserName,
                $"Challenge {request.Id} updated"
            );

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Challenge ({Id}) updated by {UserName} ({UserId})",
                request.Id,
                request.UserName,
                request.UserId
            );

            var invalidationTasks = new List<Task>
            {
                cache
                    .RemoveAsync(CacheKeys.ChallengeDetails(challenge.Id), token: cancellationToken)
                    .AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPut(
                    "play/challenges/{id:Guid}",
                    async (
                        Guid id,
                        UpdateChallengeRequest request,
                        ClaimsPrincipal claims,
                        ISender sender,
                        IChallengePointsConcurrencyGuard guard
                    ) =>
                    {
                        if (!await guard.TryEnterAsync())
                            return Results.BadRequest(Error.AnotherProcessRunning);

                        try
                        {
                            var userId = claims.GetLoggedInUserId<string>();
                            if (userId is null)
                                return Results.BadRequest();

                            var userName = claims.GetLoggedInUserName();
                            if (userName is null)
                                return Results.BadRequest();

                            var command = new Command(
                                id,
                                request.Name,
                                request.Description,
                                request.Points,
                                request.DeadlineEnabled,
                                request.Deadline,
                                request.MaxAttempts,
                                request.Tags,
                                request.Flags,
                                userId,
                                userName
                            );

                            var result = await sender.Send(command);

                            return result.IsFailure
                                ? Results.BadRequest(result.Error)
                                : Results.NoContent();
                        }
                        finally
                        {
                            guard.Exit();
                        }
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
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
                .MaximumLength(2000)
                .WithMessage("Challenge description must be 2000 characters or less.");

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
