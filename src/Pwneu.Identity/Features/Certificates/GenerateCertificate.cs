﻿using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Views;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using Razor.Templating.Core;

namespace Pwneu.Identity.Features.Certificates;

public class GenerateCertificate
{
    public record Command(string UserId, string? CustomName = null, DateTime? CustomIssueDate = null)
        : IRequest<Result<string>>;

    private static readonly Error UserNotFound = new(
        "CreateCertificate.UserNotFound",
        "User with the specified ID was not found");

    private static readonly Error Failed = new(
        "CreateCertificate.Failed",
        "Failed to create certificate");

    private static readonly Error NotAllowed = new(
        "CreateCertificate.NotAllowed",
        "Not allowed to create certificate");

    internal sealed class Handler(UserManager<User> userManager)
        : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = userManager.Users.SingleOrDefault(u => u.Id == request.UserId);

            if (user is null)
                return Result.Failure<string>(UserNotFound);

            var userIsManager = await userManager.IsInRoleAsync(user, Consts.Manager);

            if (userIsManager)
                return Result.Failure<string>(NotAllowed);

            // TODO -- Design certificate
            // TODO -- Enable/Disable certifications
            // TODO -- Certificate issuer and signature

            var certificate = new Certificate
            {
                UserId = user.Id,
                FullName = request.CustomName ?? user.FullName,
                IssuedAt = request.CustomIssueDate ?? DateTime.UtcNow,
                AuthorizedSignatureSvg = string.Empty,
                EventOrganizer = string.Empty
            };

            var (success, certificateHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                "Views/CertificateView.cshtml",
                certificate);

            return success ? certificateHtml : Result.Failure<string>(Failed);
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
                .RequireRateLimiting(Consts.Certify)
                .WithTags(nameof(Certificates));
        }
    }
}