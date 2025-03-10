using MassTransit;
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
    public class SendEmailConfirmationEventConsumer(
        IOptions<SmtpOptions> smtpOptions,
        ILogger<SendEmailConfirmationEventConsumer> logger
    ) : IConsumer<RegisteredEvent>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public async Task Consume(ConsumeContext<RegisteredEvent> context)
        {
            var message = context.Message;

            if (_smtpOptions.SendEmailConfirmationIsEnabled is false)
            {
                logger.LogInformation(
                    "Failed to send confirmation token to {Email}: Email confirmation is disabled.",
                    message.Email
                );
                return;
            }

            var encodedEmail = WebUtility.UrlEncode(message.Email);
            var encodedConfirmationToken = WebUtility.UrlEncode(message.ConfirmationToken);

            var model = new Model
            {
                UserName = message.UserName,
                FullName = message.FullName,
                VerifyEmailUrl = _smtpOptions.VerifyEmailUrl,
                EncodedEmail = encodedEmail,
                EncodedConfirmationToken = encodedConfirmationToken,
                WebsiteUrl = _smtpOptions.WebsiteUrl,
                LogoUrl = _smtpOptions.LogoUrl,
                IpAddress = !string.IsNullOrWhiteSpace(message.IpAddress)
                    ? message.IpAddress
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
            mailMessage.Subject = "Welcome to PWNEU! Verify Your Email to Activate Your Account.";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = sendEmailConfirmationHtml;

            try
            {
                smtpClient.Send(mailMessage);
                logger.LogInformation("Sent confirmation token to {email}", message.Email);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Failed to send confirmation token to {email}: {error}",
                    message.Email,
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
