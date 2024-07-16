using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

        var converter = new ValueConverter<List<string>, string>(
            v => string.Join(',', v),
            v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        var comparer = new ValueComparer<List<string>>(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        builder
            .Entity<Challenge>()
            .Property(c => c.Flags)
            .HasConversion(converter)
            .Metadata
            .SetValueComparer(comparer);
    }

    public virtual DbSet<Challenge> Challenges { get; set; } = null!;
    public virtual DbSet<ChallengeFile> ChallengeFiles { get; set; } = null!;
}