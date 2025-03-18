using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Extensions;

namespace Pwneu.Api.Features.Announcements;

public static class Announce
{
    public record Command(string Message, string UserName) : IRequest<Result>;

    internal sealed class Handler(
        IHubContext<AnnouncementHub> context,
        IValidator<Command> validator
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure(
                    new Error("Announce.Validation", validationResult.ToString())
                );

            await context.Clients.All.SendAsync(
                CommonConstants.ReceiveAnnouncement,
                $"Announcement:\n\n{request.Message}\n\n- {request.UserName}",
                cancellationToken
            );

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "announcements",
                    async (AnnounceRequest request, HttpContext httpContext, ISender sender) =>
                    {
                        var userName = httpContext.User.GetLoggedInUserName();
                        if (string.IsNullOrWhiteSpace(userName))
                            return Results.Unauthorized();

                        var command = new Command(request.Message, userName);

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Created();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Announcements));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Message)
                .NotEmpty()
                .WithMessage("Messsage is required.")
                .MaximumLength(200)
                .WithMessage("Message must be 200 characters or less.");
        }
    }
}
