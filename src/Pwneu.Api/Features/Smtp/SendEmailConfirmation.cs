using MediatR;
using Microsoft.Extensions.Options;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Options;
using Razor.Templating.Core;
using System.Net;
using System.Net.Mail;

namespace Pwneu.Api.Features.Smtp;

public static class SendEmailConfirmation
{
    internal sealed class Handler(IOptions<SmtpOptions> smtpOptions, ILogger<Handler> logger)
        : INotificationHandler<RegisteredEvent>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public async Task Handle(RegisteredEvent notification, CancellationToken cancellationToken)
        {
            if (_smtpOptions.SendEmailConfirmationIsEnabled is false)
            {
                logger.LogInformation(
                    "Failed to send confirmation token to {Email}: Email confirmation is disabled.",
                    notification.Email
                );
                return;
            }

            var encodedEmail = WebUtility.UrlEncode(notification.Email);
            var encodedConfirmationToken = WebUtility.UrlEncode(notification.ConfirmationToken);

            var model = new Model
            {
                UserName = notification.UserName,
                FullName = notification.FullName,
                VerifyEmailUrl = _smtpOptions.VerifyEmailUrl,
                EncodedEmail = encodedEmail,
                EncodedConfirmationToken = encodedConfirmationToken,
                WebsiteUrl = _smtpOptions.WebsiteUrl,
                LogoUrl = _smtpOptions.LogoUrl,
                IpAddress = !string.IsNullOrWhiteSpace(notification.IpAddress)
                    ? notification.IpAddress
                    : CommonConstants.Unknown,
            };

            var (success, sendEmailConfirmationHtml) =
                await RazorTemplateEngine.TryRenderPartialAsync(
                    "Views/SendEmailConfirmationView.cshtml",
                    model
                );

            if (!success)
            {
                logger.LogError(
                    "Failed to send confirmation token to {Email}: Failed to render email confirmation template",
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
            mailMessage.Subject = "Welcome to PWNEU! Verify Your Email to Activate Your Account.";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = sendEmailConfirmationHtml;

            try
            {
                smtpClient.Send(mailMessage);
                logger.LogInformation("Sent confirmation token to {email}", notification.Email);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Failed to send confirmation token to {email}: {error}",
                    notification.Email,
                    ex.Message
                );
            }
        }
    }

    public class Model
    {
        public required string UserName { get; init; }
        public required string FullName { get; init; }
        public required string VerifyEmailUrl { get; init; }
        public required string EncodedEmail { get; init; }
        public required string EncodedConfirmationToken { get; init; }
        public required string WebsiteUrl { get; init; }
        public required string LogoUrl { get; init; }
        public required string IpAddress { get; init; }
    }
}
