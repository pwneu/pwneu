using MassTransit;
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
    public class NotifyLoginEventConsumer(
        IOptions<SmtpOptions> smtpOptions,
        ILogger<NotifyLoginEventConsumer> logger
    ) : IConsumer<LoggedInEvent>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public async Task Consume(ConsumeContext<LoggedInEvent> context)
        {
            var message = context.Message;

            if (message.Email is null)
            {
                logger.LogInformation(
                    "Failed to send login notification to {email}: No email provided.",
                    message.Email
                );
                return;
            }

            if (_smtpOptions.NotifyLoginIsEnabled is false)
            {
                logger.LogInformation(
                    "Failed to send login notification to {email}: Notify login is disabled.",
                    message.Email
                );
                return;
            }

            var model = new Model
            {
                FullName = message.FullName,
                Email = message.Email,
                IpAddress = string.IsNullOrWhiteSpace(message.IpAddress)
                    ? CommonConstants.Unknown
                    : message.IpAddress,
                UserAgent = string.IsNullOrWhiteSpace(message.UserAgent)
                    ? CommonConstants.Unknown
                    : message.UserAgent,
                Referer = string.IsNullOrWhiteSpace(message.Referer)
                    ? CommonConstants.Unknown
                    : message.Referer,
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
                    message.Email
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

            using var mailMessage = new MailMessage(_smtpOptions.SenderAddress, message.Email);
            mailMessage.Subject = "PWNEU Login Notification.";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = notifyLoginHtml;

            try
            {
                smtpClient.Send(mailMessage);
                logger.LogInformation("Sent login notification to {email}", message.Email);
            }
            catch (Exception e)
            {
                logger.LogError(
                    "Failed to send login notification to {email}: {error}",
                    message.Email,
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
