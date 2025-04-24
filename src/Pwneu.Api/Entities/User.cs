using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class User : IdentityUser
{
    [MaxLength(40)]
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int Points { get; set; }
    public DateTime LatestSolve { get; set; }
    public bool IsVisibleOnLeaderboards { get; set; }

    [MaxLength(1000)]
    public string? RefreshToken { get; set; }

    [MaxLength(39)]
    public string? RegistrationIpAddress { get; set; }
    public DateTime RefreshTokenExpiry { get; set; }
    public ICollection<Submission> Submissions { get; init; } = [];
    public ICollection<Solve> Solves { get; init; } = [];
    public ICollection<HintUsage> HintUsages { get; init; } = [];
    public ICollection<PointsActivity> PointsActivities { get; init; } = [];
    public ICollection<Conversation> Conversations { get; init; } = [];
    public Certificate? Certificate { get; init; }
}
