using System.ComponentModel.DataAnnotations;

namespace Pwneu.Play.Shared.Entities;

public class Category
{
    public Guid Id { get; init; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(300)] public string Description { get; set; } = string.Empty;
    public ICollection<Challenge> Challenges { get; init; } = [];
}