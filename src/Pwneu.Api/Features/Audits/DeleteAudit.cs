using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;

namespace Pwneu.Api.Features.Audits;

public static class DeleteAudit
{
    public record Command(Guid Id) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "DeleteAudit.NotFound",
        "The audit with the specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var audit = await context
                .Audits.Where(au => au.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (audit is null)
                return Result.Failure(NotFound);

            context.Audits.Remove(audit);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete(
                    "play/audits/{id:Guid}",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Command(id);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Audits));
        }
    }
}
