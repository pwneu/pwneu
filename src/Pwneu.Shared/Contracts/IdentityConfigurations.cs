namespace Pwneu.Shared.Contracts;

public record IdentityConfigurationsResponse
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}