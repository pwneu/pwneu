using System.Net;
using System.Net.Mail;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Smtp.Shared;
using Razor.Templating.Core;

namespace Pwneu.Smtp.Features.Profile;

public static class SendPasswordResetToken
{
    private static readonly Error Disabled = new("SendEmailConfirmation.Disabled",
        "Email confirmation is disabled");

    private static readonly Error RenderFailed = new("SendPasswordResetToken.RenderFailed",
        "Failed to render html template for password reset");

    public record Command(string Email, string PasswordResetToken) : IRequest<Result>;

    internal sealed class Handler(IOptions<SmtpOptions> smtpOptions)
        : IRequestHandler<Command, Result>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (_smtpOptions.SendPasswordResetTokenIsEnabled is false)
                return Result.Failure(Disabled);

            var encodedEmail = WebUtility.UrlEncode(request.Email);
            var encodedPasswordResetToken = WebUtility.UrlEncode(request.PasswordResetToken);

            var model = new Model
            {
                ResetPasswordUrl = _smtpOptions.ResetPasswordUrl,
                EncodedEmail = encodedEmail,
                EncodedPasswordResetToken = encodedPasswordResetToken,
                WebsiteUrl = _smtpOptions.WebsiteUrl,
                LogoUrl = _smtpOptions.LogoUrl
            };

            var (success, sendPasswordResetTokenHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                "Views/SendPasswordResetTokenView.cshtml",
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
            mailMessage.Subject = "PWNEU Password Reset.";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = sendPasswordResetTokenHtml;

            try
            {
                smtpClient.Send(mailMessage);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("SendPasswordResetToken.Failed", ex.Message));
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

public class ForgotPasswordEventConsumer(ISender sender, ILogger<ForgotPasswordEventConsumer> logger)
    : IConsumer<ForgotPasswordEvent>
{
    public async Task Consume(ConsumeContext<ForgotPasswordEvent> context)
    {
        try
        {
            logger.LogInformation("Received forgot password event message");

            var message = context.Message;
            var command = new SendPasswordResetToken.Command(message.Email, message.PasswordResetToken);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                logger.LogInformation("Sent reset password token to {email}", context.Message.Email);
                return;
            }

            logger.LogError(
                "Failed to send reset password token to {email}: {error}", message.Email, result.Error.Message);
        }
        catch (Exception e)
        {
            logger.LogError("{e}", e.Message);
        }
    }
}