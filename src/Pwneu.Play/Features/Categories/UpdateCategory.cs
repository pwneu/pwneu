using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Categories;

/// <summary>
/// Updates a challenge under a specified ID.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class UpdateCategory
{
    public record Command(Guid Id, string Name, string Description) : IRequest<Result>;

    private static readonly Error NotFound = new("UpdateCategory.NotFound",
        "The category with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IValidator<Command> validator, IFusionCache cache)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var category = await context
                .Categories
                .Where(c => c.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null) return Result.Failure<Guid>(NotFound);

            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("UpdateCategory.Validation", validationResult.ToString()));

            category.Name = request.Name;
            category.Description = request.Description;

            context.Update(category);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(Keys.Category(request.Id), token: cancellationToken);

            return Result.Success();
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