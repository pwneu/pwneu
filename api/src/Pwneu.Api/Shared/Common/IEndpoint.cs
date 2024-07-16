namespace Pwneu.Api.Shared.Common;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}