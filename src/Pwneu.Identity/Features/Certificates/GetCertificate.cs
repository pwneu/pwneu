using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Certificates;

public static class GetCertificate
{
    public record Query(string UserId) : IRequest<Result<CertificateResponse>>;

    public static readonly Error UserNotFound = new(
        "GetUserCertificate.UserNotFound",
        "The user with the specified ID was not found");

    public static readonly Error CertificateNotFound = new(
        "GetUserCertificate.NotFound",
        "The user doesn't have a certificate yet");

    public static readonly Error NotAllowed = new(
        "GetUserCertificate.NotAllowed",
        "Not allowed to receive certificate");

    internal sealed class Handler(ApplicationDbContext context, UserManager<User> userManager, IFusionCache cache)
        : IRequestHandler<Query, Result<CertificateResponse>>
    {
        public async Task<Result<CertificateResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            // Check if certifications are allowed.
            var isCertificationEnabled = await cache.GetOrSetAsync(Keys.IsCertificationEnabled(), async _ =>
                    await context.GetIdentityConfigurationValueAsync<bool>(Consts.IsCertificationEnabled,
                        cancellationToken),
                token: cancellationToken);

            if (!isCertificationEnabled)
                return Result.Failure<CertificateResponse>(NotAllowed);

            var user = userManager.Users.SingleOrDefault(u => u.Id == request.UserId);

            if (user is null)
                return Result.Failure<CertificateResponse>(UserNotFound);

            // The admin and the managers are not allowed to receive a certificate.
            var userIsManager = await userManager.IsInRoleAsync(user, Consts.Manager);

            if (userIsManager)
                return Result.Failure<CertificateResponse>(NotAllowed);

            var certificate = await context
                .Certificates
                .Where(c => c.UserId == request.UserId)
                .Select(c => new CertificateResponse
                {
                    FileName = c.FileName,
                    ContentType = c.ContentType,
                    Data = c.Data
                })
                .FirstOrDefaultAsync(cancellationToken);

            return certificate ?? Result.Failure<CertificateResponse>(CertificateNotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/certificate", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure
                        ? Results.BadRequest(result.Error)
                        : Results.File(result.Value.Data, result.Value.ContentType, result.Value.FileName);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Certificates));

            app.MapGet("me/certificate", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var command = new Query(id);
                    var result = await sender.Send(command);

                    return result.IsFailure
                        ? Results.BadRequest(result.Error)
                        : Results.File(result.Value.Data, result.Value.ContentType, result.Value.FileName);
                })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Generate)
                .WithTags(nameof(Certificates));
        }
    }
}