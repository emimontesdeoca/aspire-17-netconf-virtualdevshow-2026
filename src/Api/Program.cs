using Microsoft.AspNetCore.Http.HttpResults;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient("cache");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient("sub-api", client =>
    client.BaseAddress = new Uri("https+http://sub-api"));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapGet("/database-info", async Task<ContentHttpResult> (
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    IConnectionMultiplexer redis,
    CancellationToken cancellationToken) =>
{
    const string cacheKey = "demo:database-info";
    var cache = redis.GetDatabase();
    var cached = await cache.StringGetAsync(cacheKey);

    if (cached.HasValue)
    {
        httpContext.Response.Headers["X-Cache"] = "HIT";
        return TypedResults.Text(cached.ToString());
    }

    var value = await httpClientFactory
        .CreateClient("sub-api")
        .GetStringAsync("/database-info", cancellationToken);

    await cache.StringSetAsync(cacheKey, value, TimeSpan.FromMinutes(1));
    httpContext.Response.Headers["X-Cache"] = "MISS";
    return TypedResults.Text(value);
})
    .WithName("GetCachedDatabaseInfo")
    .WithSummary("Obtiene información de PostgreSQL")
    .WithDescription("Consulta SubApi y cachea la respuesta en Redis durante un minuto.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain");

app.Run();
