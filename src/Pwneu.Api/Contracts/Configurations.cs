namespace Pwneu.Api.Contracts;

public record ConfigurationResponse
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}
