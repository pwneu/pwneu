using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pwneu.Api.Common;

namespace Pwneu.Api.Extensions;

public static class V1EndpointExtensions
{
    public static IServiceCollection AddV1Endpoints(this IServiceCollection services)
    {
        services.AddV1Endpoints(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IServiceCollection AddV1Endpoints(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        var serviceDescriptors = assembly
            .DefinedTypes.Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && type.IsAssignableTo(typeof(IV1Endpoint))
            )
            .Select(type => ServiceDescriptor.Transient(typeof(IV1Endpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IApplicationBuilder MapV1Endpoints(
        this WebApplication app,
        string prefix = "/api/v1"
    )
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IV1Endpoint>>();

        var routeGroup = app.MapGroup(prefix);

        foreach (var endpoint in endpoints)
            endpoint.MapV1Endpoint(routeGroup);

        return app;
    }

    public static IApplicationBuilder MapV1Endpoints(
        this WebApplication app,
        Assembly assembly,
        string prefix
    )
    {
        var endpoints = app
            .Services.GetRequiredService<IEnumerable<IV1Endpoint>>()
            .Where(endpoint => endpoint.GetType().Assembly == assembly);

        var routeGroup = app.MapGroup(prefix);

        foreach (var endpoint in endpoints)
            endpoint.MapV1Endpoint(routeGroup);

        return app;
    }
}
