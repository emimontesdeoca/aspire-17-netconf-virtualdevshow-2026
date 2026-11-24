using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("aspiredemo");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapGet("/database-info", async Task<ContentHttpResult> (
    NpgsqlDataSource database,
    CancellationToken cancellationToken) =>
{
    await using var command = database.CreateCommand(
        "SELECT current_database() || ' | ' || " +
        "to_char(CURRENT_TIMESTAMP, 'YYYY-MM-DD\"T\"HH24:MI:SS.USOF')");

    var value = await command.ExecuteScalarAsync(cancellationToken) as string
        ?? throw new InvalidOperationException("PostgreSQL no devolvió información de la base de datos.");

    return TypedResults.Text(value);
})
    .WithName("GetDatabaseInfo")
    .WithSummary("Consulta PostgreSQL")
    .WithDescription("Devuelve el nombre de la base de datos y la hora actual del servidor.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain");

app.Run();
