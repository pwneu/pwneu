using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User>(options)
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

        builder
            .Entity<Challenge>()
            .Property(c => c.Description)
            .HasMaxLength(100);

        builder
            .Entity<FlagSubmission>()
            .Property(fs => fs.FlagStatus)
            .HasConversion<string>();

        builder
            .Entity<FlagSubmission>()
            .HasOne(fs => fs.Challenge)
            .WithMany(c => c.FlagSubmissions)
            .HasForeignKey(fs => fs.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Entity<FlagSubmission>()
            .HasOne(fs => fs.User)
            .WithMany(u => u.FlagSubmissions)
            .HasForeignKey(fs => fs.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public virtual DbSet<Challenge> Challenges { get; set; } = null!;
    public virtual DbSet<ChallengeFile> ChallengeFiles { get; set; } = null!;
    public virtual DbSet<FlagSubmission> FlagSubmissions { get; set; } = null!;
}