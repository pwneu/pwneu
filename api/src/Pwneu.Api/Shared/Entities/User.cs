using Microsoft.AspNetCore.Identity;

namespace Pwneu.Api.Shared.Entities;

public class User : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<FlagSubmission> FlagSubmissions { get; set; } = [];
    public ICollection<Solve> Solves { get; set; } = [];
}