using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Pwneu.Chat;
using Pwneu.Chat.Shared.Data;
using Pwneu.Chat.Shared.Extensions;
using Pwneu.Chat.Shared.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// App options.
builder.Services
    .AddOptions<ChatOptions>()
    .BindConfiguration($"{nameof(ChatOptions)}")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOpenAIChatCompletion("gpt-4o-mini", builder.Configuration[Consts.ChatOptionsOpenAiApiKey]!);

// CORS (Cross-Origin Resource Sharing).
builder.Services.AddCors();

// Postgres Database.
var postgres = builder.Configuration.GetConnectionString(Consts.Postgres) ??
               throw new InvalidOperationException("No Database connection found");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(postgres));

var assembly = typeof(AssemblyMarker).Assembly;
const string serviceName = nameof(Pwneu.Chat);

// Assembly scanning of Mediator and Fluent Validations.
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
builder.Services.AddValidatorsFromAssembly(assembly);

// Add endpoints from the Features folder (Vertical Slice).
builder.Services.AddEndpoints(assembly);

builder.ConfigureServiceDefaults(serviceName);
builder.ConfigureMessageBroker(assembly);
builder.ConfigureSwagger();
builder.ConfigureAuth();
builder.ConfigureRateLimiter();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints(serviceName);

app.ApplyMigrations();

app.UseCors(policy => policy.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin());

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapEndpoints();

app.Run();

public partial class Program;