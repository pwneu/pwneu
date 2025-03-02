using Pwneu.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var pwneudb = postgres.AddDatabase("pwneudb");

builder
    .AddProject<Projects.Pwneu_Api>("pwneuapi")
    .WithScalar()
    .WithExternalHttpEndpoints()
    .WithReference(pwneudb)
    .WaitFor(pwneudb);

builder.Build().Run();
