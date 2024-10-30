using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.IntegrationTests.Shared;

public class MockMemberAccess : IMemberAccess
{
    public Task<bool> MemberExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(id == "true");
    }

    public Task<UserDetailsResponse?> GetMemberDetailsAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UserDetailsResponse { Id = Guid.NewGuid().ToString(), UserName = "test" })!;
    }

    public Task<List<string>> GetMemberIdsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<string>());
    }
}