using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pwneu.Shared.Common;

namespace Pwneu.Shared.Extensions;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        services.AddEndpoints(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var serviceDescriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app, string prefix = "/api")
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        var routeGroup = app.MapGroup(prefix);

        foreach (var endpoint in endpoints) endpoint.MapEndpoint(routeGroup);

        return app;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app, Assembly assembly, string prefix)
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>()
            .Where(endpoint => endpoint.GetType().Assembly == assembly);

        var routeGroup = app.MapGroup(prefix);

        foreach (var endpoint in endpoints) endpoint.MapEndpoint(routeGroup);

        return app;
    }
}