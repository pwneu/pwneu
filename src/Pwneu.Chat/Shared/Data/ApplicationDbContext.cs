using Microsoft.EntityFrameworkCore;
using Pwneu.Chat.Shared.Entities;

namespace Pwneu.Chat.Shared.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public virtual DbSet<Conversation> Conversations { get; init; } = null!;
}