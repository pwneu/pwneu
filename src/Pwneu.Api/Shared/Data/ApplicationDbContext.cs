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

    public virtual DbSet<Challenge> Challenges { get; init; } = null!;
    public virtual DbSet<Artifact> Artifacts { get; init; } = null!;
    public virtual DbSet<FlagSubmission> FlagSubmissions { get; init; } = null!;
    public virtual DbSet<Category> Categories { get; init; } = null!;
    public virtual DbSet<AccessKey> AccessKeys { get; init; } = null!;
}