using System.Security.Claims;
using FluentValidation;
using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Categories;

/// <summary>
/// Creates a category.
/// Only admin can create a category.
/// </summary>
public static class CreateCategory
{
    public record Command(string Name, string Description, string UserId, string UserName) : IRequest<Result<Guid>>;

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        ILogger<Handler> logger,
        IValidator<Command> validator) : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("CreateCategory.Validation", validationResult.ToString()));

            var category = new Category
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                Name = request.Name,
                Description = request.Description
            };

            context.Add(category);

            await context.SaveChangesAsync(cancellationToken);

            // Clear categories cache.
            await Task.WhenAll(
                cache.RemoveAsync(Keys.Categories(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.CategoryIds(), token: cancellationToken).AsTask()
            );

            logger.LogInformation(
                "Category ({CategoryId}) created by {UserName} ({UserId})",
                category.Id,
                request.UserName,
                request.UserId);

            var audit = new Audit
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                UserName = request.UserName,
                Action = $"Category {category.Id} created",
                PerformedAt = DateTime.UtcNow
            };

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            return category.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("categories", async (CreateCategoryRequest request, ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.BadRequest();

                    var userName = claims.GetLoggedInUserName();
                    if (userName is null) return Results.BadRequest();

                    var command = new Command(request.Name, request.Description, userId, userName);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(Categories));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name)
                .NotEmpty()
                .WithMessage("Category name is required.")
                .MaximumLength(100)
                .WithMessage("Category name must be 100 characters or less.");

            RuleFor(c => c.Description)
                .NotEmpty()
                .WithMessage("Category description is required.")
                .MaximumLength(300)
                .WithMessage("Category description must be 300 characters or less.");
        }
    }
}