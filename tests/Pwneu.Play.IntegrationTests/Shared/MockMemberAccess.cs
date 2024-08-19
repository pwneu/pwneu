using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.IntegrationTests.Shared;

public class MockMemberAccess : IMemberAccess
{
    public Task<bool> MemberExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(id == "true");
    }

    public Task<UserResponse?> GetMemberAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UserResponse { Id = Guid.NewGuid().ToString(), UserName = "test" })!;
    }
}