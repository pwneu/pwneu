using Microsoft.AspNetCore.Identity;

namespace Pwneu.Api.Shared.Entities;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; }
}