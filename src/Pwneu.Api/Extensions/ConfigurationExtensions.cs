using System.Text.Json;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;

namespace Pwneu.Api.Extensions;

public static class ConfigurationExtensions
{
    public static async Task SetConfigurationValueAsync<T>(
        this AppDbContext context,
        string key,
        T value,
        CancellationToken cancellationToken = default
    )
    {
        string valueString;

        // Store string as-is without wrapping quotes.
        if (value is string stringValue)
            valueString = stringValue;
        else
            valueString = value is not null ? JsonSerializer.Serialize(value) : string.Empty;

        var config = await context.Configurations.FindAsync([key], cancellationToken);

        if (config is not null)
            config.UpdateValue(valueString);
        else
            context.Configurations.Add(Configuration.Create(key, valueString));

        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task<T?> GetConfigurationValueAsync<T>(
        this AppDbContext context,
        string key,
        CancellationToken cancellationToken = default
    )
    {
        var config = await context.Configurations.FindAsync([key], cancellationToken);

        if (config is null)
            return default;

        var value = config.Value;

        // Return directly if it requests a string.
        if (typeof(T) == typeof(string))
            return (T)(object)value;

        if (typeof(T).IsValueType)
            return string.IsNullOrEmpty(value) ? default : JsonSerializer.Deserialize<T>(value);

        return string.IsNullOrEmpty(value) ? default : JsonSerializer.Deserialize<T>(value);
    }
}
