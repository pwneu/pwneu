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
/// Creates a challenge under a specified category ID.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
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
        string UserName) : IRequest<Result<Guid>>;

    private static readonly Error CategoryNotFound = new("CreateCategory.CategoryNotFound",
        "The category with the specified ID was not found");

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        IValidator<Command> validator,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("CreateChallenge.Validation", validationResult.ToString()));

            var category = await context
                .Categories
                .Where(ctg => ctg.Id == request.CategoryId)
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null)
                return Result.Failure<Guid>(CategoryNotFound);

            var challenge = new Challenge
            {
                Id = Guid.NewGuid(),
                CategoryId = request.CategoryId,
                Name = request.Name,
                Description = request.Description,
                Points = request.Points,
                DeadlineEnabled = request.DeadlineEnabled,
                Deadline = request.Deadline.ToUniversalTime(),
                MaxAttempts = request.MaxAttempts,
                Tags = request.Tags.ToList(),
                Flags = request.Flags.ToList()
            };

            context.Add(challenge);

            await context.SaveChangesAsync(cancellationToken);

            await cache.InvalidateCategoryCacheAsync(request.CategoryId, cancellationToken);
            await cache.RemoveAsync(Keys.ChallengeIds(), token: cancellationToken);

            logger.LogInformation(
                "Challenge ({ChallengeId}) created in category ({CategoryId}) by {UserName} ({UserId})",
                challenge.Id,
                request.CategoryId,
                request.UserName,
                request.UserId);

            var audit = new Audit
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                UserName = request.UserName,
                Action = $"Challenge {challenge.Id} created",
                PerformedAt = DateTime.UtcNow
            };

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            return challenge.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("categories/{categoryId:Guid}/challenges",
                    async (Guid categoryId, CreateChallengeRequest request, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var userName = claims.GetLoggedInUserName();
                        if (userName is null) return Results.BadRequest();

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
                            UserName: userName);

                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
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
                .WithMessage("All flags must be non-empty.")
                .Must(flags => flags.All(flag => flag.Length <= 100))
                .WithMessage("Each flag must be 100 characters or less.");
        }
    }
}