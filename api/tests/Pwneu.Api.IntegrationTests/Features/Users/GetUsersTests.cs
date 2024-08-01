using FluentAssertions;
using Pwneu.Api.Features.Users;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;

namespace Pwneu.Api.IntegrationTests.Features.Users;

[Collection(nameof(IntegrationTestCollection))]
public class GetUsersTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetUsers_WithoutManagerAndAdmins()
    {
        // Act
        var getUsers = await Sender.Send(new GetUsers.Query());
        var userNames = getUsers.Value.Items.Select(u => u.UserName);

        // Assert
        getUsers.IsSuccess.Should().BeTrue();
        getUsers.Should().BeOfType<Result<PagedList<UserResponse>>>();
        userNames.Should().NotContain(Consts.Admin);
    }
}