using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Configuration
{
    [MaxLength(100)]
    public string Key { get; init; } = null!;

    [MaxLength(100)]
    public string Value { get; set; } = null!;

    private Configuration() { }

    public static Configuration Create(string key, string value)
    {
        return new Configuration { Key = key, Value = value };
    }

    public void UpdateValue(string newValue)
    {
        Value = newValue;
    }
}
