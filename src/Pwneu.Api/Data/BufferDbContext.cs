using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Entities;

namespace Pwneu.Api.Data;

public class BufferDbContext(DbContextOptions<BufferDbContext> options) : DbContext(options)
{
    public DbSet<SolveBuffer> SolveBuffers { get; init; } = null!;
    public DbSet<SubmissionBuffer> SubmissionBuffers { get; init; } = null!;
    public DbSet<HintUsageBuffer> HintUsageBuffers { get; init; } = null!;
}
