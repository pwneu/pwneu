using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder
            .Entity<Challenge>()
            .HasMany(c => c.ChallengeFiles)
            .WithOne(cf => cf.Challenge)
            .HasForeignKey(cf => cf.ChallengeId)
            .IsRequired();

        builder
            .Entity<Challenge>()
            .Property(c => c.Name)
            .HasMaxLength(100);
    }

    public virtual DbSet<Challenge> Challenges { get; set; } = null!;
    public virtual DbSet<ChallengeFile> ChallengeFiles { get; set; } = null!;
}