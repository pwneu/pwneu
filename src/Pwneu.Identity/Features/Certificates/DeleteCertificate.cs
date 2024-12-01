using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Shared.Common;

namespace Pwneu.Identity.Features.Certificates;

public static class DeleteCertificate
{
    public record Command(string UserId) : IRequest<Result>;

    public static readonly Error UserNotFound = new(
        "DeleteCertificate.UserNotFound",
        "The user with the specified ID was not found");

    public static readonly Error CertificateNotFound = new(
        "DeleteCertificate.NotFound",
        "The user doesn't have a certificate yet");

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var userExists = await context
                .Users
                .Where(u => u.Id == request.UserId)
                .AnyAsync(cancellationToken);

            if (!userExists)
                return Result.Failure<Guid>(UserNotFound);

            var certificate = await context
                .Certificates
                .Where(c => c.UserId == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (certificate is null)
                return Result.Failure(CertificateNotFound);

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