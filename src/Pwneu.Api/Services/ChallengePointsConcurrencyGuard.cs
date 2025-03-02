namespace Pwneu.Api.Services;

/// <inheritdoc />
public class ChallengePointsConcurrencyGuard : IChallengePointsConcurrencyGuard
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <inheritdoc />
    public async Task<bool> TryEnterAsync()
    {
        return await _semaphore.WaitAsync(0);
    }

    /// <inheritdoc />
    public void Exit()
    {
        _semaphore.Release();
    }
}

/// <summary>
/// Ensures that only one challenge-related operation runs at a time,
/// preventing concurrent processing and potential race conditions.
/// </summary>
public interface IChallengePointsConcurrencyGuard
{
    /// <summary>
    /// Attempts to acquire the lock without waiting.
    /// Returns <c>true</c> if successful, otherwise <c>false</c>.
    /// </summary>
    Task<bool> TryEnterAsync();

    /// <summary>
    /// Releases the lock, allowing the next request to proceed.
    /// </summary>
    void Exit();
}
