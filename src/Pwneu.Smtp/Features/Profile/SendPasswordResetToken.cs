using System.Net;
using System.Net.Mail;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Smtp.Shared.Options;

namespace Pwneu.Smtp.Features.Profile;

public static class SendPasswordResetToken
{
    public record Command(string Email, string PasswordResetToken) : IRequest<Result>;

    private static readonly Error Disabled = new("SendEmailConfirmation.Disabled",
        "Email confirmation is disabled");

    internal sealed class Handler(IOptions<SmtpOptions> smtpOptions) : IRequestHandler<Command, Result>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (_smtpOptions.SendPasswordResetTokenIsEnabled is false)
                return Task.FromResult(Result.Failure(Disabled));

            var smtpClient = new SmtpClient("smtp.gmail.com", Consts.GmailSmtpPort)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpOptions.SenderAddress, _smtpOptions.SenderPassword)
            };

            using var mailMessage = new MailMessage(_smtpOptions.SenderAddress, request.Email);
            mailMessage.Subject = "Pwneu Reset!";
            mailMessage.Body = $"Password Reset Token: {request.PasswordResetToken}";

            try
            {
                smtpClient.Send(mailMessage);
                return Task.FromResult(Result.Success());
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result.Failure(new Error("SendPasswordResetToken.Failed", ex.Message)));
            }
        }
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