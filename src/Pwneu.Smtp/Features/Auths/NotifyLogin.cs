using System.Net;
using System.Net.Mail;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Smtp.Shared;
using Razor.Templating.Core;

namespace Pwneu.Smtp.Features.Auths;

public static class NotifyLogin
{
    private static readonly Error NoEmail = new("NotifyLogin.NoEmail", "No Email specified");
    private static readonly Error Disabled = new("NotifyLogin.Disabled", "Notify login is disabled");

    private static readonly Error RenderFailed = new("NotifyLogin.RenderFailed",
        "Failed to render html template for login notification");

    public record Command(
        string FullName,
        string? Email,
        string? IpAddress = null,
        string? UserAgent = null,
        string? Referer = null) : IRequest<Result>;

    internal sealed class Handler(IOptions<SmtpOptions> smtpOptions)
        : IRequestHandler<Command, Result>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Email is null)
                return Result.Failure(NoEmail);

            if (_smtpOptions.NotifyLoginIsEnabled is false)
                return Result.Failure(Disabled);

            var model = new Model
            {
                FullName = request.FullName,
                Email = request.Email,
                IpAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? "Unknown" : request.IpAddress,
                UserAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? "Unknown" : request.UserAgent,
                Referer = string.IsNullOrWhiteSpace(request.Referer) ? "Unknown" : request.Referer,
                WebsiteUrl = _smtpOptions.WebsiteUrl,
                LogoUrl = _smtpOptions.LogoUrl
            };

            var (success, notifyLoginHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                "Views/NotifyLoginView.cshtml",
                model);

            if (!success)
                return Result.Failure(RenderFailed);

            var smtpClient = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = _smtpOptions.EnableSsl,
                UseDefaultCredentials = true,
                Credentials = new NetworkCredential(_smtpOptions.SenderAddress, _smtpOptions.SenderPassword)
            };

            using var mailMessage = new MailMessage(_smtpOptions.SenderAddress, request.Email);
            mailMessage.Subject = "PWNEU Login Notification.";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = notifyLoginHtml;

            try
            {
                smtpClient.Send(mailMessage);
                return Result.Success();
            }
            catch (Exception e)
            {
                return Result.Failure(new Error("NotifyLogin.Failed", e.Message));
            }
        }
    }

    public class Model
    {
        public required string FullName { get; init; }
        public required string Email { get; init; }
        public required string IpAddress { get; init; }
        public required string UserAgent { get; init; }
        public required string Referer { get; init; }
        public required string WebsiteUrl { get; init; }
        public required string LogoUrl { get; init; }
    }
}

public class LoggedInEventConsumer(ISender sender, ILogger<LoggedInEventConsumer> logger)
    : IConsumer<LoggedInEvent>
{
    public async Task Consume(ConsumeContext<LoggedInEvent> context)
    {
        try
        {
            logger.LogInformation("Received logged in event message");

            var message = context.Message;
            var command = new NotifyLogin.Command(message.FullName, message.Email, message.IpAddress, message.UserAgent,
                message.Referer);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                logger.LogInformation("Sent login notification to {email}", context.Message.Email);
                return;
            }

            logger.LogError(
                "Failed to send login notification to {email}: {error}", message.Email, result.Error.Message);
        }
        catch (Exception e)
        {
            logger.LogError("{e}", e.Message);
        }
    }
}