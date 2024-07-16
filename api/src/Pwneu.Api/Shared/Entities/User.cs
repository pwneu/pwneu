using Microsoft.AspNetCore.Identity;

namespace Pwneu.Api.Shared.Entities;

public class User : IdentityUser
{
    public DateTime CreatedAt { get; set; }
}