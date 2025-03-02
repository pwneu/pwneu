using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Features.Users;

namespace Pwneu.IntegrationTests.Features.Users;

[Collection(nameof(IntegrationTestCollection))]
public class GetUserDetailsTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetUser_WhenUserExists()
    {
        // Arrange
        var user = await UserManager.FindByNameAsync(CommonConstants.Unknown);

        // Act
        var getUser = await Sender.Send(new GetUserDetails.Query(user!.Id));

        // Assert
        getUser.Should().BeOfType<Result<UserDetailsNoEmailResponse>>();
    }
}
