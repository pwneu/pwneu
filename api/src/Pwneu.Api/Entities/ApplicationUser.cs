using Microsoft.AspNetCore.Identity;

namespace Pwneu.Api.Entities;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; }
}