using MediatR;
using Microsoft.Extensions.Options;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Options;
using Razor.Templating.Core;
using System.Net;
using System.Net.Mail;

namespace Pwneu.Api.Features.Smtp;

public static class NotifyLogin
{
    internal sealed class Handler(IOptions<SmtpOptions> smtpOptions, ILogger<Handler> logger)
        : INotificationHandler<LoggedInEvent>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public async Task Handle(LoggedInEvent notification, CancellationToken cancellationToken)
        {
            if (notification.Email is null)
            {
                logger.LogInformation(
                    "Failed to send login notification to {email}: No email provided.",
                    notification.Email
                );
                return;
            }

            if (_smtpOptions.NotifyLoginIsEnabled is false)
            {
                logger.LogInformation(
                    "Failed to send login notification to {email}: Notify login is disabled.",
                    notification.Email
                );
                return;
            }

            var model = new Model
            {
                FullName = notification.FullName,
                Email = notification.Email,
                IpAddress = string.IsNullOrWhiteSpace(notification.IpAddress)
                    ? CommonConstants.Unknown
                    : notification.IpAddress,
                UserAgent = string.IsNullOrWhiteSpace(notification.UserAgent)
                    ? CommonConstants.Unknown
                    : notification.UserAgent,
                Referer = string.IsNullOrWhiteSpace(notification.Referer)
                    ? CommonConstants.Unknown
                    : notification.Referer,
                WebsiteUrl = _smtpOptions.WebsiteUrl,
                LogoUrl = _smtpOptions.LogoUrl,
            };

            var (success, notifyLoginHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                "Views/NotifyLoginView.cshtml",
                model
            );

            if (!success)
            {
                logger.LogError(
                    "Failed to send login notification to {email}: Failed to render HTML template for login notification.",
                    notification.Email
                );
                return;
            }

            var smtpClient = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = _smtpOptions.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _smtpOptions.SenderAddress,
                    _smtpOptions.SenderPassword
                ),
            };

            using var mailMessage = new MailMessage(_smtpOptions.SenderAddress, notification.Email);
            mailMessage.Subject = "PWNEU Login Notification.";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = notifyLoginHtml;

            try
            {
                smtpClient.Send(mailMessage);
                logger.LogInformation("Sent login notification to {email}", notification.Email);
            }
            catch (Exception e)
            {
                logger.LogError(
                    "Failed to send login notification to {email}: {error}",
                    notification.Email,
                    e.Message
                );
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
