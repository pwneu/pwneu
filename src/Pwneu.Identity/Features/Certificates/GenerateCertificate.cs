﻿using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using Razor.Templating.Core;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Certificates;

public class GenerateCertificate
{
    public record Command(string UserId, string? CustomName = null, DateTime? CustomIssueDate = null)
        : IRequest<Result<string>>;

    private static readonly Error UserNotFound = new(
        "GenerateCertificate.UserNotFound",
        "User with the specified ID was not found");

    private static readonly Error Failed = new(
        "GenerateCertificate.Failed",
        "Failed to create certificate");

    private static readonly Error NotAllowed = new(
        "GenerateCertificate.NotAllowed",
        "Not allowed to create certificate");

    internal sealed class Handler(ApplicationDbContext context, UserManager<User> userManager, IFusionCache cache)
        : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Check if the submissions are allowed.
            var isCertificationEnabled = await cache.GetOrSetAsync(Keys.IsCertificationEnabled(), async _ =>
                    await context.GetIdentityConfigurationValueAsync<bool>(Consts.IsCertificationEnabled,
                        cancellationToken),
                token: cancellationToken);

            if (!isCertificationEnabled)
                return Result.Failure<string>(NotAllowed);

            var user = userManager.Users.SingleOrDefault(u => u.Id == request.UserId);

            if (user is null)
                return Result.Failure<string>(UserNotFound);

            // The admin and the managers are not allowed to receive a certificate.
            var userIsManager = await userManager.IsInRoleAsync(user, Consts.Manager);

            if (userIsManager)
                return Result.Failure<string>(NotAllowed);

            var existingCertificate = await context
                .Certificates
                .Where(c => c.UserId == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingCertificate is not null)
            {
                var (success, certificateHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                    "Views/CertificateView.cshtml",
                    existingCertificate);

                return success ? certificateHtml : Result.Failure<string>(Failed);
            }
            else
            {
                var certificationIssuer = await cache.GetOrSetAsync(Keys.CertificationIssuer(),
                    async _ => await context.GetIdentityConfigurationValueAsync<string>(
                        Consts.CertificationIssuer,
                        cancellationToken),
                    token: cancellationToken);

                var certificate = new Certificate
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FullName = request.CustomName ?? user.FullName,
                    Issuer = certificationIssuer ?? "PWNEU",
                    IssuedAt = request.CustomIssueDate ?? DateTime.UtcNow,
                };

                var (success, certificateHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                    "Views/CertificateView.cshtml",
                    certificate);

                if (!success)
                    return Result.Failure<string>(Failed);

                context.Add(certificate);

                await context.SaveChangesAsync(cancellationToken);

                return certificateHtml;
            }
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("certificates", async (CreateCertificateRequest request, ISender sender) =>
                {
                    var command = new Command(request.UserId, request.CustomName);

                    var result = await sender.Send(command);

                    return result.IsFailure
                        ? Results.BadRequest(result.Error)
                        : Results.Content(result.Value, "text/html");
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(Certificates));

            app.MapPost("certificates/me", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var command = new Command(id);

                    var result = await sender.Send(command);

                    return result.IsFailure
                        ? Results.BadRequest(result.Error)
                        : Results.Content(result.Value, "text/html");
                })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Generate)
                .WithTags(nameof(Certificates));
        }
    }
}