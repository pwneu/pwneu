using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>()
            .Property(u => u.FullName)
            .HasMaxLength(100);

        builder
            .Entity<IdentityRole>()
            .HasData(new List<IdentityRole>
            {
                new() { Name = Constants.Roles.User, NormalizedName = Constants.Roles.User.ToUpper() },
                new() { Name = Constants.Roles.Faculty, NormalizedName = Constants.Roles.Faculty.ToUpper() },
                new() { Name = Constants.Roles.Admin, NormalizedName = Constants.Roles.Admin.ToUpper() },
            });

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

        builder
            .Entity<Solve>()
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

    public virtual DbSet<Challenge> Challenges { get; set; } = null!;
    public virtual DbSet<ChallengeFile> ChallengeFiles { get; set; } = null!;
    public virtual DbSet<FlagSubmission> FlagSubmissions { get; set; } = null!;
    public virtual DbSet<Solve> Solves { get; set; } = null!;
}