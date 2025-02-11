using Pwneu.Shared.Extensions;
using Pwneu.Smtp;
using Pwneu.Smtp.Shared;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration(nameof(SmtpOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var assembly = typeof(AssemblyMarker).Assembly;
const string serviceName = nameof(Pwneu.Smtp);

// Assembly scanning of Mediator.
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));

builder.ConfigureServiceDefaults(serviceName);
builder.ConfigureMessageBroker(assembly);

var app = builder.Build();

app.MapDefaultEndpoints(serviceName);

app.Run();