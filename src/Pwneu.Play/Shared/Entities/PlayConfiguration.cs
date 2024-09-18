using System.ComponentModel.DataAnnotations;

namespace Pwneu.Play.Shared.Entities;

public class PlayConfiguration
{
    [MaxLength(100)] public string Key { get; init; } = null!;
    [MaxLength(100)] public string Value { get; set; } = null!;
}