using MassTransit;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Shared.Services;

public interface IMemberAccess
{
    Task<bool> MemberExistsAsync(string id, CancellationToken cancellationToken = default);
    Task<UserDetailsResponse?> GetMemberDetailsAsync(string id, CancellationToken cancellationToken = default);
    Task<List<string>> GetMemberIdsAsync(CancellationToken cancellationToken = default);
}

public class MemberAccess(
    IRequestClient<GetMemberRequest> memberClient,
    IRequestClient<GetMemberDetailsRequest> memberDetailsClient,
    IRequestClient<GetMemberIdsRequest> memberIdsClient) : IMemberAccess
{
    public async Task<bool> MemberExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return await GetMemberAsync(id, cancellationToken) is not null;
    }

    public async Task<List<string>> GetMemberIdsAsync(CancellationToken cancellationToken = default)
    {
        var response = await memberIdsClient.GetResponse<MemberIdsResponse>(
            new GetMemberIdsRequest(),
            cancellationToken);

        return response.Message.MemberIds;
    }

    public async Task<UserResponse?> GetMemberAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await memberClient.GetResponse<UserResponse, UserNotFoundResponse>(
            new GetMemberRequest { Id = id }, cancellationToken);

        if (response.Is(out Response<UserResponse>? userResponse))
            return userResponse.Message;

        response.Is(out Response<UserNotFoundResponse>? _);
        return null;
    }

    public async Task<UserDetailsResponse?> GetMemberDetailsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await memberDetailsClient.GetResponse<UserDetailsResponse, UserDetailsNotFoundResponse>(
            new GetMemberDetailsRequest { Id = id }, cancellationToken);

        if (response.Is(out Response<UserDetailsResponse>? userResponse))
            return userResponse.Message;

        response.Is(out Response<UserDetailsNotFoundResponse>? _);
        return null;
    }
}