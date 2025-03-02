using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Category
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }

    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; init; } = string.Empty;

    public ICollection<Challenge> Challenges { get; init; } = [];

    private Category() { }

    public static Category Create(string name, string description)
    {
        return new Category
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            Name = name,
            Description = description,
        };
    }
}
