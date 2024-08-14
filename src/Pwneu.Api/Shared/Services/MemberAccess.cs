using MassTransit;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.Shared.Services;

public interface IMemberAccess
{
    Task<bool> MemberExistsAsync(string id, CancellationToken cancellationToken = default);
    Task<string?> GetMemberNameAsync(string id, CancellationToken cancellationToken = default);
}

public class MemberAccess(IRequestClient<MemberRequest> client) : IMemberAccess
{
    public async Task<bool> MemberExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await client.GetResponse<UserResponse, UserNotFoundResponse>(
            new MemberRequest { Id = id }, cancellationToken);

        return !response.Is(out Response<UserNotFoundResponse> _);
    }

    public async Task<string?> GetMemberNameAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await client.GetResponse<UserResponse, UserNotFoundResponse>(
            new MemberRequest { Id = id }, cancellationToken);

        return response.Is(out Response<UserResponse>? userResponse) ? userResponse.Message.UserName : null;
    }
}