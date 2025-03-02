using FluentValidation;
using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Categories;

public static class CreateCategory
{
    public record Command(string Name, string Description, string UserId, string UserName)
        : IRequest<Result<Guid>>;

    private static readonly Error NotAllowed = new(
        "CreateCategory.NotAllowed",
        "Not allowed to create categories when submissions are enabled"
    );

    internal sealed class Handler(
        AppDbContext context,
        IFusionCache cache,
        ILogger<Handler> logger,
        IValidator<Command> validator
    ) : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(
                    new Error("CreateCategory.Validation", validationResult.ToString())
                );

            var submissionsEnabled = await cache.CheckIfSubmissionsAllowedAsync(
                context,
                cancellationToken
            );

            if (submissionsEnabled)
                return Result.Failure<Guid>(NotAllowed);

            var category = Category.Create(request.Name, request.Description);

            context.Add(category);

            var audit = Audit.Create(
                request.UserId,
                request.UserName,
                $"Category {category.Id} created"
            );

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Category ({CategoryId}) created by {UserName} ({UserId})",
                category.Id,
                request.UserName,
                request.UserId
            );

            await Task.WhenAll(
                cache.RemoveAsync(CacheKeys.Categories(), token: cancellationToken).AsTask()
            );

            return category.Id;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "play/categories",
                    async (CreateCategoryRequest request, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var userName = claims.GetLoggedInUserName();
                        if (userName is null)
                            return Results.BadRequest();

                        var command = new Command(
                            request.Name,
                            request.Description,
                            userId,
                            userName
                        );

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
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
