using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace Pwneu.AppHost;

internal static class ResourceBuilderExtensions
{
    internal static IResourceBuilder<T> WithScalar<T>(this IResourceBuilder<T> builder)
        where T : IResourceWithEndpoints
    {
        return builder.WithOpenApiDocs("scalar-docs", "Scalar API Documentation", "scalar/v1");
    }

    private static IResourceBuilder<T> WithOpenApiDocs<T>(
        this IResourceBuilder<T> builder,
        string name,
        string displayName,
        string openApiUiPath
    )
        where T : IResourceWithEndpoints
    {
        return builder.WithCommand(
            name,
            displayName,
            executeCommand: _ =>
            {
                try
                {
                    var endpoint = builder.GetEndpoint("http");

                    var url = $"{endpoint.Url}/{openApiUiPath}";

                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

                    return Task.FromResult(new ExecuteCommandResult { Success = true });
                }
                catch (Exception e)
                {
                    return Task.FromResult(
                        new ExecuteCommandResult { Success = false, ErrorMessage = e.ToString() }
                    );
                }
            },
            updateState: context =>
                context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled,
            iconName: "Document",
            iconVariant: IconVariant.Filled
        );
    }
}
