namespace Pwneu.Shared.Contracts;

public record PlayConfigurationResponse
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}