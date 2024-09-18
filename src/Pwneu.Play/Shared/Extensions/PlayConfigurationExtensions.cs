using Newtonsoft.Json;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;

namespace Pwneu.Play.Shared.Extensions;

public static class PlayConfigurationExtensions
{
    public static async Task SetPlayConfigurationValueAsync<T>(
        this ApplicationDbContext context,
        string key,
        T value,
        CancellationToken cancellationToken = default)
    {
        var valueString = value is not null ? JsonConvert.SerializeObject(value) : string.Empty;
        var config = await context.PlayConfigurations.FindAsync([key], cancellationToken);

        if (config is not null)
            config.Value = valueString;
        else
            context.PlayConfigurations.Add(new PlayConfiguration { Key = key, Value = valueString });

        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task<T?> GetPlayConfigurationValueAsync<T>(
        this ApplicationDbContext context,
        string key,
        CancellationToken cancellationToken = default)
    {
        var config = await context.PlayConfigurations.FindAsync([key], cancellationToken);

        if (config is null)
            return default;

        var value = config.Value;
        return string.IsNullOrEmpty(value) ? default : JsonConvert.DeserializeObject<T>(value);
    }
}