using System.ComponentModel.DataAnnotations;

namespace Pwneu.Smtp.Shared.Options;

public sealed class SmtpOptions
{
    [Required] [EmailAddress] public required string SenderAddress { get; init; }
    [Required] public required string SenderPassword { get; init; }
    [Required] public required bool NotifyLoginIsEnabled { get; init; }
    [Required] public required bool SendEmailConfirmationIsEnabled { get; init; }
    [Required] public required bool SendPasswordResetTokenIsEnabled { get; init; }
}