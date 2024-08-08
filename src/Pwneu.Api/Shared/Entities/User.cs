using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Pwneu.Api.Shared.Entities;

public class User : IdentityUser
{
    [MaxLength(100)] public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    [MaxLength(1000)] public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiry { get; set; }
    public ICollection<FlagSubmission> FlagSubmissions { get; set; } = [];
}