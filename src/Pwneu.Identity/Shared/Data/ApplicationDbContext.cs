using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;

namespace Pwneu.Identity.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Consts.IdentitySchema);

        builder.Entity<IdentityConfiguration>()
            .HasKey(c => c.Key);

        builder.Entity<User>()
            .HasOne(u => u.Certificate)
            .WithOne(c => c.User)
            .HasForeignKey<Certificate>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public virtual DbSet<AccessKey> AccessKeys { get; init; } = null!;
    public virtual DbSet<Certificate> Certificates { get; init; } = null!;
    public virtual DbSet<BlacklistedEmail> BlacklistedEmails { get; init; } = null!;
    public virtual DbSet<IdentityConfiguration> IdentityConfigurations { get; init; } = null!;
}