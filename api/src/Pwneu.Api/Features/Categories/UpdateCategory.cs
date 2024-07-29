using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Categories;

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
                .Challenges
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

            await cache.RemoveAsync($"{nameof(CategoryResponse)}:{category.Id}", token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("categories/{id:Guid}", async (Guid id, UpdateCategoryRequest request, ISender sender) =>
                {
                    var command = new Command(id, request.Name, request.Description);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Constants.ManagerAdminOnly)
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