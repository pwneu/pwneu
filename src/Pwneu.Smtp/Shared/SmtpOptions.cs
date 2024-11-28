using System.ComponentModel.DataAnnotations;

namespace Pwneu.Smtp.Shared;

public sealed class SmtpOptions
{
    [Required] public required bool NotifyLoginIsEnabled { get; init; }
    [Required] public required bool SendEmailConfirmationIsEnabled { get; init; }
    [Required] public required bool SendPasswordResetTokenIsEnabled { get; init; }
    [Required] public required string VerifyEmailUrl { get; init; }
    [Required] public required string ResetPasswordUrl { get; init; }
    [Required] public required string WebsiteUrl { get; init; }
    [Required] public required string LogoUrl { get; init; }
}