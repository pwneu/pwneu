using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder
            .Entity<Category>()
            .HasMany(ctg => ctg.Challenges)
            .WithOne(c => c.Category)
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder
            .Entity<Challenge>()
            .HasMany(c => c.Artifacts)
            .WithOne(a => a.Challenge)
            .HasForeignKey(a => a.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder
            .Entity<Submission>()
            .HasOne(s => s.Challenge)
            .WithMany(c => c.Submissions)
            .HasForeignKey(s => s.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public virtual DbSet<Category> Categories { get; init; } = null!;
    public virtual DbSet<Challenge> Challenges { get; init; } = null!;
    public virtual DbSet<Artifact> Artifacts { get; init; } = null!;
    public virtual DbSet<Submission> Submissions { get; init; } = null!;
}