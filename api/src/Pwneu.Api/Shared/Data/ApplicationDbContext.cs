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

        builder
            .Entity<Solve>()
            .HasKey(s => new { s.UserId, s.ChallengeId });

        builder
            .Entity<FlagSubmission>()
            .HasKey(s => new { s.UserId, s.ChallengeId });

        builder
            .Entity<Solve>()
            .HasOne(s => s.User)
            .WithMany(u => u.Solves)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Entity<Solve>()
            .HasOne(s => s.Challenge)
            .WithMany(c => c.Solves)
            .HasForeignKey(s => s.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public virtual DbSet<Challenge> Challenges { get; init; } = null!;
    public virtual DbSet<ChallengeFile> ChallengeFiles { get; init; } = null!;
    public virtual DbSet<FlagSubmission> FlagSubmissions { get; init; } = null!;
    public virtual DbSet<Solve> Solves { get; init; } = null!;
}