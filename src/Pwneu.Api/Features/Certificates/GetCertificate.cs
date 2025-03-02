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

public static class GetCertificate
{
    public record Query(string UserId, string RequesterId) : IRequest<Result<CertificateResponse>>;

    public static readonly Error UserNotFound = new(
        "GetUserCertificate.UserNotFound",
        "The user with the specified ID was not found"
    );

    public static readonly Error CertificateNotFound = new(
        "GetUserCertificate.NotFound",
        "The user doesn't have a certificate yet"
    );

    public static readonly Error NotAllowed = new(
        "GetUserCertificate.NotAllowed",
        "Not allowed to receive certificate"
    );

    internal sealed class Handler(
        AppDbContext context,
        UserManager<User> userManager,
        IFusionCache cache
    ) : IRequestHandler<Query, Result<CertificateResponse>>
    {
        public async Task<Result<CertificateResponse>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            // Retrieve the requester (user who made the request).
            var requester = userManager.Users.SingleOrDefault(u => u.Id == request.RequesterId);

            // Check if the requester exists and if they have the 'Manager' role.
            var requesterIsManager =
                requester is not null && await userManager.IsInRoleAsync(requester, Roles.Manager);

            // If the requester does not exist, return "NotAllowed".
            if (requester is null)
                return Result.Failure<CertificateResponse>(NotAllowed);

            // Check if certifications are enabled, unless the requester is a manager (managers bypass this rule).
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

                // If certifications are disabled, return "NotAllowed".
                if (!isCertificationEnabled)
                    return Result.Failure<CertificateResponse>(NotAllowed);
            }

            // Retrieve the target user who is supposed to receive the certificate.
            var user = userManager.Users.SingleOrDefault(u => u.Id == request.UserId);

            // If the target user does not exist, return "UserNotFound".
            if (user is null)
                return Result.Failure<CertificateResponse>(UserNotFound);

            // Check if the target user has the 'Manager' role.
            var userIsManager = await userManager.IsInRoleAsync(user, Roles.Manager);

            // If the target user is a manager, they are not allowed to receive a certificate.
            if (userIsManager)
                return Result.Failure<CertificateResponse>(NotAllowed);

            // Retrieve the certificate details for the target user.
            var certificate = await context
                .Certificates.Where(c => c.UserId == request.UserId)
                .Select(c => new CertificateResponse
                {
                    FileName = c.FileName,
                    ContentType = c.ContentType,
                    Data = c.Data,
                })
                .FirstOrDefaultAsync(cancellationToken);

            // If no certificate is found, return "CertificateNotFound".
            return certificate ?? Result.Failure<CertificateResponse>(CertificateNotFound);
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/users/{id:Guid}/certificate",
                    async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var requesterId = claims.GetLoggedInUserId<string>();
                        if (requesterId is null)
                            return Results.BadRequest();

                        var query = new Query(id.ToString(), requesterId);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.File(
                                result.Value.Data,
                                result.Value.ContentType,
                                result.Value.FileName
                            );
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Certificates));

            app.MapGet(
                    "identity/me/certificate",
                    async (ClaimsPrincipal claims, ISender sender) =>
                    {
                        var id = claims.GetLoggedInUserId<string>();
                        if (id is null)
                            return Results.BadRequest();

                        var command = new Query(id, id);
                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.File(
                                result.Value.Data,
                                result.Value.ContentType,
                                result.Value.FileName
                            );
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .RequireRateLimiting(RateLimitingPolicies.FileGeneration)
                .WithTags(nameof(Certificates));
        }
    }
}
