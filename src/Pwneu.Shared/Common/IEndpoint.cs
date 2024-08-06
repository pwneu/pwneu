using Microsoft.AspNetCore.Routing;

namespace Pwneu.Shared.Common;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}