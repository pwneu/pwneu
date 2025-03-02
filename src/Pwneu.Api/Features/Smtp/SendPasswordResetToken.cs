using MediatR;
using Microsoft.Extensions.Options;
using Pwneu.Api.Contracts;
using Pwneu.Api.Options;
using Razor.Templating.Core;
using System.Net;
using System.Net.Mail;

namespace Pwneu.Api.Features.Smtp;

public static class SendPasswordResetToken
{
    internal sealed class Handler(IOptions<SmtpOptions> smtpOptions, ILogger<Handler> logger)
        : INotificationHandler<ForgotPasswordEvent>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public async Task Handle(
            ForgotPasswordEvent notification,
            CancellationToken cancellationToken
        )
        {
            if (_smtpOptions.SendPasswordResetTokenIsEnabled is false)
            {
                logger.LogInformation(
                    "Failed to send reset password token to {email}: Email confirmation is disabled.",
                    notification.Email
                );
                return;
            }

            var encodedEmail = WebUtility.UrlEncode(notification.Email);
            var encodedPasswordResetToken = WebUtility.UrlEncode(notification.PasswordResetToken);

            var model = new Model
            {
                ResetPasswordUrl = _smtpOptions.ResetPasswordUrl,
                EncodedEmail = encodedEmail,
                EncodedPasswordResetToken = encodedPasswordResetToken,
                WebsiteUrl = _smtpOptions.WebsiteUrl,
                LogoUrl = _smtpOptions.LogoUrl,
            };

            var (success, sendPasswordResetTokenHtml) =
                await RazorTemplateEngine.TryRenderPartialAsync(
                    "Views/SendPasswordResetTokenView.cshtml",
                    model
                );

            if (!success)
            {
                logger.LogError(
                    "Failed to send reset password token to {email}: Failed to render email confirmation template",
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
            mailMessage.Subject = "PWNEU Password Reset.";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = sendPasswordResetTokenHtml;

            try
            {
                smtpClient.Send(mailMessage);
                logger.LogInformation("Sent reset password token to {email}", notification.Email);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Failed to send reset password token to {email}: {error}",
                    notification.Email,
                    ex.Message
                );
            }
        }
    }

    public class Model
    {
        public required string ResetPasswordUrl { get; init; }
        public required string EncodedEmail { get; init; }
        public required string EncodedPasswordResetToken { get; init; }
        public required string WebsiteUrl { get; init; }
        public required string LogoUrl { get; init; }
    }
}
