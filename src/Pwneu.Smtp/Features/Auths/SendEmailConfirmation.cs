using System.Net;
using System.Net.Mail;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Smtp.Shared.Options;

namespace Pwneu.Smtp.Features.Auths;

public static class SendEmailConfirmation
{
    public record Command(string Email, string ConfirmationToken) : IRequest<Result>;

    private static readonly Error Disabled = new("SendEmailConfirmation.Disabled",
        "Email confirmation is disabled");

    internal sealed class Handler(IOptions<SmtpOptions> smtpOptions) : IRequestHandler<Command, Result>
    {
        private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (_smtpOptions.SendEmailConfirmationIsEnabled is false)
                return Task.FromResult(Result.Failure(Disabled));

            var smtpClient = new SmtpClient("smtp.gmail.com", Consts.GmailSmtpPort)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpOptions.SenderAddress, _smtpOptions.SenderPassword)
            };

            using var mailMessage = new MailMessage(_smtpOptions.SenderAddress, request.Email);
            mailMessage.Subject = "Hello Pwneu!";
            mailMessage.Body = $"Confirmation Token: {request.ConfirmationToken}";

            try
            {
                smtpClient.Send(mailMessage);
                return Task.FromResult(Result.Success());
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result.Failure(new Error("SendEmailConfirmation.Failed", ex.Message)));
            }
        }
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
            var command = new SendEmailConfirmation.Command(message.Email, message.ConfirmationToken);
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