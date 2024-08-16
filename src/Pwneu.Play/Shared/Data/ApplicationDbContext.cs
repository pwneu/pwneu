using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Entities;

namespace Pwneu.Play.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder
            .Entity<Category>()
            .HasMany(c => c.Challenges)
            .WithOne(ch => ch.Category)
            .HasForeignKey(ch => ch.CategoryId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder
            .Entity<Challenge>()
            .HasMany(ch => ch.Artifacts)
            .WithOne(a => a.Challenge)
            .HasForeignKey(a => a.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder
            .Entity<Challenge>()
            .HasMany(ch => ch.Submissions)
            .WithOne(s => s.Challenge)
            .HasForeignKey(s => s.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Entity<Challenge>()
            .HasMany(ch => ch.Hints)
            .WithOne(h => h.Challenge)
            .HasForeignKey(h => h.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder
            .Entity<Hint>()
            .HasMany(h => h.HintUsages)
            .WithOne(hu => hu.Hint)
            .HasForeignKey(hu => hu.HintId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder
            .Entity<HintUsage>()
            .HasKey(hu => new { hu.UserId, hu.HintId });
    }

    public virtual DbSet<Category> Categories { get; init; } = null!;
    public virtual DbSet<Challenge> Challenges { get; init; } = null!;
    public virtual DbSet<Artifact> Artifacts { get; init; } = null!;
    public virtual DbSet<Submission> Submissions { get; init; } = null!;
    public virtual DbSet<Hint> Hints { get; init; } = null!;
    public virtual DbSet<HintUsage> HintUsages { get; init; } = null!;
}