using System.Net;
using System.Net.Mail;
using MassTransit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Pwneu.Shared.Common;
using Pwneu.Smtp.Shared;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

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

// SMTP Client
var senderAddressConfig = builder.Configuration["SmtpOptions:SenderAddress"];
var senderAddress = string.IsNullOrWhiteSpace(senderAddressConfig)
    ? throw new InvalidOperationException("Sender address is required")
    : senderAddressConfig;

var senderPasswordConfig = builder.Configuration["SmtpOptions:SenderPassword"];
var senderPassword = string.IsNullOrWhiteSpace(senderPasswordConfig)
    ? throw new InvalidOperationException("Sender password is required")
    : senderPasswordConfig;

var hostConfig = builder.Configuration["SmtpOptions:Host"];
var host = string.IsNullOrWhiteSpace(hostConfig)
    ? "pwneu.smtp.host"
    : hostConfig;

var portConfig = builder.Configuration["SmtpOptions:Port"];
var port = string.IsNullOrWhiteSpace(portConfig) || !int.TryParse(portConfig, out var parsedPort)
    ? 25
    : parsedPort;

var enableSslConfig = builder.Configuration["SmtpOptions:EnableSsl"];
var enableSsl = !string.IsNullOrWhiteSpace(enableSslConfig) &&
                bool.TryParse(enableSslConfig, out var parsedEnableSsl) &&
                parsedEnableSsl;

builder.Services
    .AddFluentEmail(senderAddress)
    .AddSmtpSender(new SmtpClient(host)
    {
        Port = port,
        DeliveryMethod = SmtpDeliveryMethod.Network,
        EnableSsl = enableSsl,
        UseDefaultCredentials = false,
        Timeout = 10_000,
        Credentials = new NetworkCredential(senderAddress, senderPassword)
    });

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();