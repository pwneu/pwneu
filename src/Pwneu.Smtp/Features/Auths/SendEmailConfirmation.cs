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

public static class SendEmailConfirmation
{
    private static readonly Error Disabled = new("SendEmailConfirmation.Disabled",
        "Email confirmation is disabled");

    private static readonly Error RenderFailed = new("SendEmailConfirmation.RenderFailed",
        "Failed to render html template for email confirmation");

    public record Command(string UserName, string FullName, string Email, string ConfirmationToken) : IRequest<Result>;

    internal sealed class Handler(IOptions<SmtpOptions> smtpOptions)
        : IRequestHandler<Command, Result>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (_smtpOptions.SendEmailConfirmationIsEnabled is false)
                return Result.Failure(Disabled);

            var encodedEmail = WebUtility.UrlEncode(request.Email);
            var encodedConfirmationToken = WebUtility.UrlEncode(request.ConfirmationToken);

            var model = new Model
            {
                UserName = request.UserName,
                FullName = request.FullName,
                VerifyEmailUrl = _smtpOptions.VerifyEmailUrl,
                EncodedEmail = encodedEmail,
                EncodedConfirmationToken = encodedConfirmationToken,
                WebsiteUrl = _smtpOptions.WebsiteUrl,
                LogoUrl = _smtpOptions.LogoUrl
            };

            var (success, sendEmailConfirmationHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                "Views/SendEmailConfirmationView.cshtml",
                model);

            if (!success)
                return Result.Failure(RenderFailed);

            var smtpClient = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = _smtpOptions.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpOptions.SenderAddress, _smtpOptions.SenderPassword)
            };

            using var mailMessage = new MailMessage(_smtpOptions.SenderAddress, request.Email);
            mailMessage.Subject = "Welcome to PWNEU! Verify Your Email to Activate Your Account.";
            mailMessage.IsBodyHtml = true;
            mailMessage.Body = sendEmailConfirmationHtml;

            try
            {
                smtpClient.Send(mailMessage);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(new Error("SendEmailConfirmation.Failed", ex.Message));
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
    }
}

public class RegisteredEventConsumer(ISender sender, ILogger<RegisteredEventConsumer> logger)
    : IConsumer<RegisteredEvent>
{
    public async Task Consume(ConsumeContext<RegisteredEvent> context)
    {
        try
        {
            logger.LogInformation("Received registered event message");

            var message = context.Message;
            var command = new SendEmailConfirmation.Command(
                message.UserName,
                message.FullName,
                message.Email,
                message.ConfirmationToken);

            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                logger.LogInformation("Sent confirmation token to {email}", context.Message.Email);
                return;
            }

            logger.LogError(
                "Failed to send confirmation token to {email}: {error}", message.Email, result.Error.Message);
        }
        catch (Exception e)
        {
            logger.LogError("{e}", e.Message);
        }
    }
}