using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Entities;

namespace Pwneu.Identity.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User>(options)
{
    public virtual DbSet<AccessKey> AccessKeys { get; init; } = null!;
}