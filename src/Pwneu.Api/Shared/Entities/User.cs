using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Pwneu.Api.Shared.Entities;

public class User : IdentityUser
{
    [MaxLength(100)] public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public ICollection<FlagSubmission> FlagSubmissions { get; set; } = [];
    public ICollection<Solve> Solves { get; set; } = [];
}