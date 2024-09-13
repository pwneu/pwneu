var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (Cross-Origin Resource Sharing)
builder.Services.AddCors();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(corsPolicy =>
    corsPolicy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
    app.MapGet("/", async context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        var forwardedForHeader = context.Request.Headers["X-Forwarded-For"].ToString();
        var forwardedProtoHeader = context.Request.Headers["X-Forwarded-Proto"].ToString();
        var forwardedHostHeader = context.Request.Headers["X-Forwarded-Host"].ToString();

        var response = new
        {
            Service = "Pwneu Gateway",
            ClientIp = clientIp,
            ForwardedFor = forwardedForHeader,
            ForwardedProto = forwardedProtoHeader,
            ForwardedHost = forwardedHostHeader
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    });

app.MapReverseProxy();

app.Run();