using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Shared.Common;

namespace Pwneu.Identity.Features.Certificates;

public static class DeleteUserCertificate
{
    public record Command(string Id) : IRequest<Result>;

    private static readonly Error NotFound = new("RemoveCertificate.NotFound",
        "The user doesn't have a certificate yet");

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var certificate = await context
                .Certificates
                .Where(c => c.UserId == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (certificate is null)
                return Result.Failure(NotFound);

            context.Certificates.Remove(certificate);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("users/{id:Guid}/certificate", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(Certificates));
        }
    }
}