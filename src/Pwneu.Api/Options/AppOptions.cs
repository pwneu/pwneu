﻿using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Options;

public sealed class AppOptions
{
    [Required]
    public required bool IsArchiveMode { get; init; }
    [Required]
    public required string InitialAdminPassword { get; init; }
    public required string? ValidEmailDomain { get; init; }

    [Required]
    public required bool IsTurnstileEnabled { get; init; }

    [Required]
    public required string TurnstileSecretKey { get; init; }

    [Required]
    public required string ResetPasswordUrl { get; init; }

    [Required]
    public required int MaxFailedIpAddressAttemptCount { get; init; }

    [Required]
    public required int MaxFailedUserAttemptCount { get; init; }

    [Required]
    public required string Flag { get; init; }

    [Required]
    public required bool AutoMigrate { get; init; }
}
