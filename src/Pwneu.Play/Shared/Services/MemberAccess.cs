using MassTransit;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Shared.Services;

public interface IMemberAccess
{
    Task<bool> MemberExistsAsync(string id, CancellationToken cancellationToken = default);
    Task<UserResponse?> GetMemberAsync(string id, CancellationToken cancellationToken = default);
}

public class MemberAccess(IRequestClient<GetMemberRequest> client) : IMemberAccess
{
    public async Task<bool> MemberExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return await GetMemberAsync(id, cancellationToken) is not null;
    }

    public async Task<UserResponse?> GetMemberAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await client.GetResponse<UserResponse, UserNotFoundResponse>(
            new GetMemberRequest { Id = id }, cancellationToken);

        if (response.Is(out Response<UserResponse>? userResponse))
            return userResponse.Message;

        response.Is(out Response<UserNotFoundResponse> _);
        return null;
    }
}