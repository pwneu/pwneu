using FluentValidation;
using MediatR;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.IdentityConfigurations;

public class SetCertificationIssuer
{
    public record Command(string IssuerName) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IValidator<Command> validator)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error(
                    "SetCertificationIssuer.Validation",
                    validationResult.ToString()));

            await context.SetIdentityConfigurationValueAsync(
                Consts.CertificationIssuer,
                request.IssuerName,
                cancellationToken);

            await cache.RemoveAsync(Keys.CertificationIssuer(), token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("configurations/certificationIssuer", async (string issuerName, ISender sender) =>
                {
                    var query = new Command(issuerName);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(IdentityConfigurations));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.IssuerName)
                .NotEmpty()
                .WithMessage("Issuer name is required.")
                .MaximumLength(20)
                .WithMessage("Issuer name must be 20 characters or less.");
        }
    }
}