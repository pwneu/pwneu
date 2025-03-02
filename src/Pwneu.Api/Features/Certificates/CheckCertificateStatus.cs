using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Certificates;

public static class CheckCertificateStatus
{
    public record Query(string UserId, string RequesterId) : IRequest<Result<CertificateStatus>>;

    public static readonly Error UserNotFound = new(
        "GetUserCertificate.UserNotFound",
        "The user with the specified ID was not found"
    );

    internal sealed class Handler(
        AppDbContext context,
        UserManager<User> userManager,
        IFusionCache cache
    ) : IRequestHandler<Query, Result<CertificateStatus>>
    {
        public async Task<Result<CertificateStatus>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var requester = userManager.Users.SingleOrDefault(u => u.Id == request.RequesterId);

            // If the requester does not exist, they are not allowed to check certificate statuses.
            if (requester is null)
                return CertificateStatus.NotAllowed;

            var user = userManager.Users.SingleOrDefault(u => u.Id == request.UserId);

            // If the target user does not exist, return a failure response.
            if (user is null)
                return Result.Failure<CertificateStatus>(UserNotFound);

            // If the target user is a manager, they are not allowed to receive a certificate.
            if (await userManager.IsInRoleAsync(user, Roles.Manager))
                return CertificateStatus.NotAllowed;

            // Check if the requester has the Manager role.
            var requesterIsManager = await userManager.IsInRoleAsync(requester, Roles.Manager);

            // Check if the user has a certificate in the database.
            var hasCertificate = await context
                .Certificates.Where(c => c.UserId == request.UserId)
                .AnyAsync(cancellationToken);

            // If the user does not have a certificate, return the corresponding status.
            if (!hasCertificate)
                return CertificateStatus.WithoutCertificate;

            // If the requester is NOT a manager, check if certification issuance is enabled.
            if (!requesterIsManager)
            {
                var isCertificationEnabled = await cache.GetOrSetAsync(
                    CacheKeys.IsCertificationEnabled(),
                    async _ =>
                        await context.GetConfigurationValueAsync<bool>(
                            ConfigurationKeys.IsCertificationEnabled,
                            cancellationToken
                        ),
                    token: cancellationToken
                );

                // If certifications are disabled, return a not allowed status.
                if (!isCertificationEnabled)
                    return CertificateStatus.NotAllowed;
            }

            // The user has a certificate and is allowed to receive it.
            return CertificateStatus.WithCertificate;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/users/{id:Guid}/certificate/check",
                    async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var requesterId = claims.GetLoggedInUserId<string>();
                        if (requesterId is null)
                            return Results.BadRequest();

                        var query = new Query(id.ToString(), requesterId);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value.ToString());
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Certificates));

            app.MapGet(
                    "identity/me/certificate/check",
                    async (ClaimsPrincipal claims, ISender sender) =>
                    {
                        var id = claims.GetLoggedInUserId<string>();
                        if (id is null)
                            return Results.BadRequest();

                        var command = new Query(id, id);
                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value.ToString());
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .RequireRateLimiting(RateLimitingPolicies.FileGeneration)
                .WithTags(nameof(Certificates));
        }
    }
}
