using Microsoft.EntityFrameworkCore;
using Pwneu.Chat.Shared.Entities;
using Pwneu.Shared.Common;

namespace Pwneu.Chat.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Consts.ChatSchema);
    }

    public virtual DbSet<Conversation> Conversations { get; init; } = null!;
}