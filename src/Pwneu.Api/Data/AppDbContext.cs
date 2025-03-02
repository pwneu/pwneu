using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Entities;

namespace Pwneu.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<User>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(CommonConstants.PwneuSchema);

        builder.Entity<Configuration>().HasKey(c => c.Key);

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
            .HasMany(ch => ch.Hints)
            .WithOne(h => h.Challenge)
            .HasForeignKey(h => h.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder
            .Entity<Challenge>()
            .HasMany(ch => ch.Submissions)
            .WithOne(s => s.Challenge)
            .HasForeignKey(s => s.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder
            .Entity<Challenge>()
            .HasMany(ch => ch.Solves)
            .WithOne(s => s.Challenge)
            .HasForeignKey(s => s.ChallengeId)
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
            .Entity<User>()
            .HasMany(u => u.Submissions)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder
            .Entity<User>()
            .HasMany(u => u.Solves)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder
            .Entity<User>()
            .HasMany(u => u.HintUsages)
            .WithOne(hu => hu.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder
            .Entity<User>()
            .HasMany(u => u.PointsActivities)
            .WithOne(pa => pa.User)
            .HasForeignKey(pa => pa.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder
            .Entity<User>()
            .HasOne(u => u.Certificate)
            .WithOne(c => c.User)
            .HasForeignKey<Certificate>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder
            .Entity<User>()
            .HasMany(u => u.Conversations)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Entity<Challenge>().HasIndex(ch => ch.CategoryId);

        builder.Entity<Submission>().HasIndex(s => new { s.UserId, s.ChallengeId });

        builder.Entity<Solve>().HasIndex(s => s.UserId);
        builder.Entity<Solve>().HasIndex(s => s.ChallengeId);
        builder.Entity<Solve>().HasIndex(s => new { s.UserId, s.ChallengeId }).IsUnique();

        builder.Entity<HintUsage>().HasIndex(s => s.UserId);
        builder.Entity<HintUsage>().HasIndex(s => s.HintId);
        builder.Entity<HintUsage>().HasIndex(s => new { s.UserId, s.HintId }).IsUnique();

        builder.Entity<PointsActivity>().HasIndex(pa => new { pa.UserId, pa.IsSolve });
        builder.Entity<PointsActivity>().HasIndex(pa => pa.UserId);
        builder.Entity<PointsActivity>().HasIndex(pa => pa.ChallengeName);
        builder.Entity<PointsActivity>().HasIndex(pa => pa.ChallengeId);
        builder.Entity<PointsActivity>().HasIndex(pa => pa.OccurredAt);
        builder.Entity<PointsActivity>().HasIndex(pa => pa.IsSolve);
        builder.Entity<PointsActivity>().HasIndex(pa => pa.HintId);
        builder.Entity<PointsActivity>().HasIndex(pa => new { pa.IsSolve, pa.HintId });

        builder.Entity<User>().HasIndex(u => new { u.Points, u.LatestSolve });
        builder.Entity<User>().HasIndex(u => new { u.IsVisibleOnLeaderboards, u.Points });
        builder.Entity<User>().HasIndex(u => u.IsVisibleOnLeaderboards);
        builder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        builder.Entity<Certificate>().HasIndex(c => c.UserId);
        builder.Entity<Conversation>().HasIndex(c => c.UserId);
    }

    public virtual DbSet<AccessKey> AccessKeys { get; init; } = null!;
    public virtual DbSet<BlacklistedEmail> BlacklistedEmails { get; init; } = null!;
    public virtual DbSet<Configuration> Configurations { get; init; } = null!;
    public virtual DbSet<Certificate> Certificates { get; init; } = null!;
    public virtual DbSet<Category> Categories { get; init; } = null!;
    public virtual DbSet<Challenge> Challenges { get; init; } = null!;
    public virtual DbSet<Artifact> Artifacts { get; init; } = null!;
    public virtual DbSet<Hint> Hints { get; init; } = null!;
    public virtual DbSet<Audit> Audits { get; init; } = null!;
    public virtual DbSet<Submission> Submissions { get; init; } = null!;
    public virtual DbSet<Solve> Solves { get; init; } = null!;
    public virtual DbSet<HintUsage> HintUsages { get; init; } = null!;
    public virtual DbSet<Conversation> Conversations { get; init; } = null!;
    public virtual DbSet<PointsActivity> PointsActivities { get; init; } = null!;
}
