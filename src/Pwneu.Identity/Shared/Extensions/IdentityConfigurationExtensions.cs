using Newtonsoft.Json;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;

namespace Pwneu.Identity.Shared.Extensions;

public static class IdentityConfigurationExtensions
{
    public static async Task SetIdentityConfigurationValueAsync<T>(
        this ApplicationDbContext context,
        string key,
        T value,
        CancellationToken cancellationToken = default)
    {
        string valueString;

        // Store string as-is without wrapping quotes.
        if (value is string stringValue)
            valueString = stringValue;
        else
            valueString = value is not null ? JsonConvert.SerializeObject(value) : string.Empty;

        var config = await context.IdentityConfigurations.FindAsync([key], cancellationToken);

        if (config is not null)
            config.Value = valueString;
        else
            context.IdentityConfigurations.Add(new IdentityConfiguration { Key = key, Value = valueString });

        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task<T?> GetIdentityConfigurationValueAsync<T>(
        this ApplicationDbContext context,
        string key,
        CancellationToken cancellationToken = default)
    {
        var config = await context.IdentityConfigurations.FindAsync([key], cancellationToken);

        if (config is null)
            return default;

        var value = config.Value;
        return string.IsNullOrEmpty(value) ? default : JsonConvert.DeserializeObject<T>(value);
    }
}