using FluentAssertions;
using Pwneu.Identity.Features.Users;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.IntegrationTests.Features.Users;

[Collection(nameof(IntegrationTestCollection))]
public class GetUserDetailsTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData(Consts.Admin, false)]
    [InlineData("test", true)]
    public async Task Handle_Should_GetUser_WhenUserIsNotManagerOrAdmin(string userName, bool expected)
    {
        // Arrange
        await Cache.RemoveAsync("managerIds");
        var user = await UserManager.FindByNameAsync(userName);

        // Act
        var getUser = await Sender.Send(new GetUserDetails.Query(user!.Id));

        // Assert
        getUser.IsSuccess.Should().Be(expected);
        getUser.Should().BeOfType<Result<UserDetailsResponse>>();
    }
}