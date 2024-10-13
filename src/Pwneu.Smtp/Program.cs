using MassTransit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Pwneu.Shared.Common;
using Pwneu.Smtp.Shared.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration(nameof(SmtpOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(nameof(Pwneu.Smtp)))
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddPrometheusExporter();
    });

var assembly = typeof(Program).Assembly;

// RabbitMQ
builder.Services.AddMassTransit(busConfigurator =>
{
    busConfigurator.SetKebabCaseEndpointNameFormatter();
    busConfigurator.AddConsumers(assembly);
    busConfigurator.UsingRabbitMq((context, configurator) =>
    {
        configurator.Host(new Uri(builder.Configuration[Consts.MessageBrokerHost]!), h =>
        {
            h.Username(builder.Configuration[Consts.MessageBrokerUsername]!);
            h.Password(builder.Configuration[Consts.MessageBrokerPassword]!);
        });
        configurator.ConfigureEndpoints(context);
    });
});

// Assembly scanning of Mediator
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();