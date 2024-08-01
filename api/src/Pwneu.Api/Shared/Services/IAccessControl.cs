namespace Pwneu.Api.Shared.Services;

public interface IAccessControl
{
    Task<IEnumerable<string>> GetManagerIdsAsync(CancellationToken cancellationToken = default);
}