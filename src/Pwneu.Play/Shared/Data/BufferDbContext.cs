using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Entities;

namespace Pwneu.Play.Shared.Data;

public class BufferDbContext(DbContextOptions<BufferDbContext> options) : DbContext(options)
{
    public DbSet<SolveBuffer> SolveBuffers { get; init; } = null!;
    public DbSet<SubmissionBuffer> SubmissionBuffers { get; init; } = null!;
}