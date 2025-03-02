using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using Pwneu.Api.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Services;

public class TurnstileValidator(
    AppDbContext context,
    IOptions<AppOptions> appOptions,
    IFusionCache cache,
    HttpClient httpClient
) : ITurnstileValidator
{
    private readonly AppOptions _appOptions = appOptions.Value;

    /// <summary>
    /// Validates turnstile token from cloudflare.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> IsValidTurnstileTokenAsync(
        string? token,
        CancellationToken cancellationToken = default
    )
    {
        var isTurnstileEnabled = await cache.GetOrSetAsync(
            CacheKeys.IsTurnstileEnabled(),
            async _ =>
                await context.GetConfigurationValueAsync<bool>(
                    ConfigurationKeys.IsTurnstileEnabled,
                    cancellationToken
                ),
            token: cancellationToken
        );

        if (!isTurnstileEnabled)
            return true;

        if (token is null)
            return false;

        var validateTurnstileToken = await httpClient.PostAsync(
            CommonConstants.TurnstileChallengeUrl,
            new StringContent(
                JsonSerializer.Serialize(
                    new { secret = _appOptions.TurnstileSecretKey, response = token }
                ),
                Encoding.UTF8,
                "application/json"
            ),
            cancellationToken
        );

        if (!validateTurnstileToken.IsSuccessStatusCode)
            return false;

        var validationResponseBody = await validateTurnstileToken.Content.ReadAsStringAsync(
            cancellationToken
        );
        var validationResponse = JsonSerializer.Deserialize<TurnstileResponse>(
            validationResponseBody
        );

        return validationResponse is not null && validationResponse.Success;
    }
}

public interface ITurnstileValidator
{
    Task<bool> IsValidTurnstileTokenAsync(
        string? token,
        CancellationToken cancellationToken = default
    );
}
