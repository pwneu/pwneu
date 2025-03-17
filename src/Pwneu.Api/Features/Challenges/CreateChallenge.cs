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

namespace Pwneu.Api.Features.Challenges;

public static class CreateChallenge
{
    public record Command(
        Guid CategoryId,
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
    ) : IRequest<Result<Guid>>;

    private static readonly Error CategoryNotFound = new(
        "CreateCategory.CategoryNotFound",
        "The category with the specified ID was not found"
    );

    private static readonly Error ChallengesLocked = new(
        "DeleteChallenge.ChallengesLocked",
        "Challenges are locked. Cannot create challenges"
    );

    internal sealed class Handler(
        AppDbContext context,
        IFusionCache cache,
        IValidator<Command> validator,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(
                    new Error("CreateChallenge.Validation", validationResult.ToString())
                );

            if (!string.Equals(request.UserName, Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var challengesLocked = await cache.CheckIfChallengesAreLockedAsync(
                    context,
                    cancellationToken
                );

                if (challengesLocked)
                    return Result.Failure<Guid>(ChallengesLocked);
            }

            var category = await context
                .Categories.Where(ctg => ctg.Id == request.CategoryId)
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null)
                return Result.Failure<Guid>(CategoryNotFound);

            var challenge = Challenge.Create(
                request.CategoryId,
                request.Name,
                request.Description,
                request.Points,
                request.DeadlineEnabled,
                request.Deadline.ToUniversalTime(),
                request.MaxAttempts,
                request.Tags.ToList(),
                request.Flags.ToList()
            );

            context.Add(challenge);

            var audit = Audit.Create(
                request.UserId,
                request.UserName,
                $"Challenge {challenge.Id} created"
            );

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync(CacheKeys.Categories(), token: cancellationToken);

            logger.LogInformation(
                "Challenge ({ChallengeId}) created in category ({CategoryId}) by {UserName} ({UserId})",
                challenge.Id,
                request.CategoryId,
                request.UserName,
                request.UserId
            );

            return challenge.Id;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "play/categories/{categoryId:Guid}/challenges",
                    async (
                        Guid categoryId,
                        CreateChallengeRequest request,
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
                            CategoryId: categoryId,
                            Name: request.Name,
                            Description: request.Description,
                            Points: request.Points,
                            DeadlineEnabled: request.DeadlineEnabled,
                            Deadline: request.Deadline,
                            MaxAttempts: request.MaxAttempts,
                            Tags: request.Tags,
                            Flags: request.Flags,
                            UserId: userId,
                            UserName: userName
                        );

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
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
                .WithMessage("All flags must be non-empty.")
                .Must(flags => flags.All(flag => flag.Length <= 500))
                .WithMessage("Each flag must be 500 characters or less.");
        }
    }
}
