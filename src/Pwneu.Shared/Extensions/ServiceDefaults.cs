using System.Reflection;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Pwneu.Shared.Common;
using Swashbuckle.AspNetCore.Filters;

namespace Pwneu.Shared.Extensions;

public static class ServiceDefaults
{
    public static TBuilder ConfigureServiceDefaults<TBuilder>(this TBuilder builder, string serviceName)
        where TBuilder : IHostApplicationBuilder
    {
        // OpenTelemetry.
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddPrometheusExporter();
            });

        builder.Services.AddHealthChecks();

        return builder;
    }

    public static TBuilder ConfigureSwagger<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey
            });
            options.OperationFilter<SecurityRequirementsOperationFilter>();
        });

        return builder;
    }

    public static TBuilder ConfigureMessageBroker<TBuilder>(this TBuilder builder, Assembly assembly)
        where TBuilder : IHostApplicationBuilder
    {
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

        return builder;
    }

    public static TBuilder ConfigureAuth<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(0),
                    ValidIssuer = builder.Configuration[Consts.JwtOptionsIssuer],
                    ValidAudience = builder.Configuration[Consts.JwtOptionsAudience],
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration[Consts.JwtOptionsSigningKey]!)),
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Consts.AdminOnly, policy => { policy.RequireRole(Consts.Admin); })
            .AddPolicy(Consts.ManagerAdminOnly, policy => { policy.RequireRole(Consts.Manager); })
            .AddPolicy(Consts.MemberOnly, policy => { policy.RequireRole(Consts.Member); });

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app, string serviceName)
    {
        app.MapHealthChecks("/healthz");
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        if (app.Environment.IsDevelopment())
            app.MapGet("/api", async context =>
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString();
                var forwardedForHeader = context.Request.Headers["X-Forwarded-For"].ToString();
                var forwardedProtoHeader = context.Request.Headers["X-Forwarded-Proto"].ToString();
                var forwardedHostHeader = context.Request.Headers["X-Forwarded-Host"].ToString();
                var cfConnectingIp = context.Request.Headers[Consts.CfConnectingIp].ToString();

                var response = new
                {
                    Service = serviceName,
                    ClientIp = clientIp,
                    ForwardedFor = forwardedForHeader,
                    ForwardedProto = forwardedProtoHeader,
                    ForwardedHost = forwardedHostHeader,
                    CfConnectingIp = cfConnectingIp
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(response);
            });

        return app;
    }
}