using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Pwneu.Api.Shared.Data;

namespace Pwneu.Api.IntegrationTests;

public class BaseIntegrationTest : IClassFixture<IntegrationTestsWebAppFactory>
{
    protected readonly ISender Sender;
    protected readonly ApplicationDbContext DbContext;

    protected BaseIntegrationTest(IntegrationTestsWebAppFactory factory)
    {
        var scope = factory.Services.CreateScope();
        Sender = scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}