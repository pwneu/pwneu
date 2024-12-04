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

public static class CheckCertificateStatus
{
    public record Query(string UserId) : IRequest<Result<CertificateStatus>>;

    public static readonly Error UserNotFound = new(
        "GetUserCertificate.UserNotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, UserManager<User> userManager, IFusionCache cache)
        : IRequestHandler<Query, Result<CertificateStatus>>
    {
        public async Task<Result<CertificateStatus>> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = userManager.Users.SingleOrDefault(u => u.Id == request.UserId);

            if (user is null)
                return Result.Failure<CertificateStatus>(UserNotFound);

            // The admin and the managers are not allowed to receive a certificate.
            var userIsManager = await userManager.IsInRoleAsync(user, Consts.Manager);

            if (userIsManager)
                return CertificateStatus.NotAllowed;

            var hasCertificate = await context
                .Certificates
                .Where(c => c.UserId == request.UserId)
                .AnyAsync(cancellationToken);

            // Check if the user has certificate first, before knowing if it's allowed to get a certificate.
            if (!hasCertificate)
                return CertificateStatus.WithoutCertificate;

            // Check if certifications are allowed.
            var isCertificationEnabled = await cache.GetOrSetAsync(Keys.IsCertificationEnabled(), async _ =>
                    await context.GetIdentityConfigurationValueAsync<bool>(Consts.IsCertificationEnabled,
                        cancellationToken),
                token: cancellationToken);

            if (!isCertificationEnabled)
                return CertificateStatus.NotAllowed;

            // Double-checking. But I don't care (〃￣ ω ￣〃).
            return hasCertificate
                ? CertificateStatus.WithCertificate
                : CertificateStatus.WithoutCertificate;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/certificate/check", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure
                        ? Results.NotFound(result.Error)
                        : Results.Ok(result.Value.ToString());
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Certificates));

            app.MapGet("me/certificate/check", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var command = new Query(id);
                    var result = await sender.Send(command);

                    return result.IsFailure
                        ? Results.NotFound(result.Error)
                        : Results.Ok(result.Value.ToString());
                })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Generate)
                .WithTags(nameof(Certificates));
        }
    }
}