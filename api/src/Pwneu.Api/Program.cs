using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Extensions;
using Swashbuckle.AspNetCore.Filters;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddCors();

builder.Services.AddIdentity<User, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 12;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

var postgres = builder.Configuration.GetConnectionString("Postgres") ??
               throw new InvalidOperationException("No Postgres connection found");

builder.Services.AddDbContext<ApplicationDbContext>(options => { options.UseNpgsql(postgres); });

var redis = builder.Configuration.GetConnectionString("Redis") ??
            throw new InvalidOperationException("No Redis connection found");

builder.Services.AddFusionCache()
    .WithDefaultEntryOptions(new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(2) })
    .WithSerializer(new FusionCacheNewtonsoftJsonSerializer())
    .WithDistributedCache(new RedisCache(new RedisCacheOptions { Configuration = redis }));

var assembly = typeof(Program).Assembly;
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
builder.Services.AddValidatorsFromAssembly(assembly);

builder.Services.AddEndpoints();

var issuer = Environment.GetEnvironmentVariable(Env.JwtIssuer);
var audience = Environment.GetEnvironmentVariable(Env.JwtAudience);
var signingKey = Environment.GetEnvironmentVariable(Env.JwtSigningKey);

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
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey!)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, policy => { policy.RequireRole(Constants.Roles.Admin); })
    .AddPolicy(Policies.FacultyAdminOnly, policy => { policy.RequireRole(Constants.Roles.Faculty); })
    .AddPolicy(Policies.UserOnly, policy => { policy.RequireRole(Constants.Roles.User); });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.ApplyMigrations();

await app.Services.SeedAdminAsync();

// TODO -- Only allow frontend framework on deployment
app.UseCors(x => x
    .AllowAnyMethod()
    .AllowAnyHeader()
    .SetIsOriginAllowed(_ => true)
    .AllowCredentials());

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

app.Run();