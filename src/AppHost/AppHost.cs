var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var database = builder.AddPostgres("postgres")
    .AddDatabase("aspiredemo");

var subApi = builder.AddDotnetProject("sub-api", "../SubApi/SubApi.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpEndpoint(port: 8081, targetPort: 18081)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

var cache = builder.AddRedis("cache");

builder.AddDotnetProject("api", "../Api/Api.csproj")
    .WithReference(subApi)
    .WithReference(cache)
    .WaitFor(subApi)
    .WaitFor(cache)
    .WithHttpEndpoint(port: 8080, targetPort: 18080)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
