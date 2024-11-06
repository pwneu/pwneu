using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Entities;

namespace Pwneu.Identity.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityConfiguration>()
            .HasKey(c => c.Key);
    }

    public virtual DbSet<AccessKey> AccessKeys { get; init; } = null!;
    public virtual DbSet<Certificate> Certificates { get; init; } = null!;
    public virtual DbSet<IdentityConfiguration> IdentityConfigurations { get; init; } = null!;
}