using FluentAssertions;
using Pwneu.Identity.Features.Users;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.IntegrationTests.Features.Users;

[Collection(nameof(IntegrationTestCollection))]
public class GetUserDetailsTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetUser_WhenUserExists()
    {
        // Arrange
        await Cache.RemoveAsync("managerIds");
        var user = await UserManager.FindByNameAsync("test");

        // Act
        var getUser = await Sender.Send(new GetUserDetails.Query(user!.Id));

        // Assert
        getUser.Should().BeOfType<Result<UserDetailsResponse>>();
    }
}