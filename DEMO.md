# Guion de la demo: Aspire 17 desde cero

Sesión: **Aspire: de cero a las últimas novedades de Aspire 17.0**  
Evento: [.NET Conf 2026 (En Español)](https://www.meetup.com/virtualdevshow/events/316451194/)  
Duración recomendada para la demo: **20-23 minutos**

El objetivo es construir progresivamente una aplicación distribuida sin saltar directamente al
resultado final:

1. crear la solución con dos APIs;
2. conectar una API con otra;
3. añadir PostgreSQL;
4. añadir Redis;
5. generar artefactos de Docker Compose.

Todos los comandos de este documento están escritos para **Bash**. Usa una terminal para `aspire run`
y otra para `curl`.

## Requisitos

- .NET 10 SDK o superior
- Aspire CLI 17.0.0 o superior
- Docker Desktop, Podman u otro runtime de contenedores compatible
- Bash
- Puertos locales `8080` y `8081` libres

Comprobación rápida:

```bash
dotnet --version
aspire --version
docker version
```

Si el puerto está ocupado, usa las URLs que aparezcan en el Dashboard o cambia los valores de
`WithHttpEndpoint`.

## Fase 0 · Crear la solución vacía

Desde la carpeta padre:

```bash
dotnet new aspire -n AspireDemo -o AspireDemo -f net10.0
cd AspireDemo

dotnet new webapi -n Api -o Api -f net10.0
dotnet new webapi -n SubApi -o SubApi -f net10.0

dotnet sln AspireDemo.sln add \
  Api/Api.csproj \
  SubApi/SubApi.csproj

dotnet add Api/Api.csproj reference \
  AspireDemo.ServiceDefaults/AspireDemo.ServiceDefaults.csproj

dotnet add SubApi/SubApi.csproj reference \
  AspireDemo.ServiceDefaults/AspireDemo.ServiceDefaults.csproj
```

La plantilla crea:

```text
AspireDemo/
├── AspireDemo.AppHost/
├── AspireDemo.ServiceDefaults/
├── Api/
├── SubApi/
└── AspireDemo.sln
```

Aspire 17 incluye el paquete `Aspire.Hosting.Dotnet` y permite registrar proyectos mediante
`AddDotnetProject`. No hay que añadir referencias de `Api` o `SubApi` al `.csproj` del AppHost.

## Fase 1 · Conectar las dos APIs

### 1.1 · Crear una respuesta fija en SubApi

```bash
cat > SubApi/Program.cs <<'CSHARP'
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapGet("/database-info", () =>
        TypedResults.Text("Hola desde SubApi"))
    .WithName("GetDatabaseInfo")
    .WithSummary("Devuelve una respuesta fija de SubApi");

app.Run();
CSHARP
```

### 1.2 · Hacer que Api llame a SubApi

```bash
cat > Api/Program.cs <<'CSHARP'
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
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

app.MapGet("/database-info", async (
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    var value = await clients
        .CreateClient("sub-api")
        .GetStringAsync("/database-info", cancellationToken);

    return TypedResults.Text(value);
})
    .WithName("GetDatabaseInfo")
    .WithSummary("Llama a SubApi y devuelve su respuesta");

app.Run();
CSHARP
```

La dirección `https+http://sub-api` es un nombre lógico resuelto por Aspire. No contiene un puerto local.

### 1.3 · Describir la topología en el AppHost

```bash
cat > AspireDemo.AppHost/AppHost.cs <<'CSHARP'
var builder = DistributedApplication.CreateBuilder(args);

var subApi = builder.AddDotnetProject(
        "sub-api",
        "../SubApi/SubApi.csproj")
    .WithHttpEndpoint(port: 8081, targetPort: 18081)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject(
        "api",
        "../Api/Api.csproj")
    .WithReference(subApi)
    .WaitFor(subApi)
    .WithHttpEndpoint(port: 8080, targetPort: 18080)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
CSHARP
```

### 1.4 · Ejecutar y probar

Terminal 1:

```bash
dotnet build
aspire run
```

Terminal 2:

```bash
curl --fail --silent --show-error \
  http://localhost:8081/database-info

curl --fail --silent --show-error \
  http://localhost:8080/database-info
```

Resultado esperado en ambos casos:

```text
Hola desde SubApi
```

En el Dashboard:

1. abrir **Resources**;
2. comprobar que `api` espera a `sub-api`;
3. abrir la traza de la segunda llamada;
4. señalar el `span` HTTP entre ambos servicios.

Detener Aspire con `Ctrl+C` antes de continuar.

## Fase 2 · Añadir PostgreSQL

### 2.1 · Añadir los paquetes

```bash
dotnet add AspireDemo.AppHost/AspireDemo.AppHost.csproj \
  package Aspire.Hosting.PostgreSQL \
  --version 17.0.0

dotnet add SubApi/SubApi.csproj \
  package Aspire.Npgsql \
  --version 17.0.0
```

La integración de hosting va en el **AppHost**. La integración cliente va en el proyecto que consulta la
base de datos.

### 2.2 · Añadir el servidor y la base al modelo

```bash
cat > AspireDemo.AppHost/AppHost.cs <<'CSHARP'
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .AddDatabase("aspiredemo");

var subApi = builder.AddDotnetProject(
        "sub-api",
        "../SubApi/SubApi.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpEndpoint(port: 8081, targetPort: 18081)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject(
        "api",
        "../Api/Api.csproj")
    .WithReference(subApi)
    .WaitFor(subApi)
    .WithHttpEndpoint(port: 8080, targetPort: 18080)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
CSHARP
```

### 2.3 · Consultar PostgreSQL desde SubApi

```bash
cat > SubApi/Program.cs <<'CSHARP'
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
        ?? throw new InvalidOperationException(
            "PostgreSQL no devolvió información de la base de datos.");

    return TypedResults.Text(value);
})
    .WithName("GetDatabaseInfo")
    .WithSummary("Consulta PostgreSQL")
    .WithDescription(
        "Devuelve el nombre de la base de datos y la hora actual del servidor.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain");

app.Run();
CSHARP
```

El valor pasado a `AddNpgsqlDataSource` debe coincidir exactamente con el nombre del recurso de base de
datos: `aspiredemo`.

### 2.4 · Ejecutar y probar

Terminal 1:

```bash
dotnet build
aspire run
```

Terminal 2:

```bash
curl --fail http://localhost:8081/database-info
curl --fail http://localhost:8080/database-info
```

Ambas llamadas deben mostrar una línea parecida a:

```text
aspiredemo | 2026-11-25T16:42:18.120000+00
```

En el Dashboard señalar:

- el contenedor `postgres`;
- la base `aspiredemo`;
- que SubApi espera a la base;
- el `span` de Npgsql dentro de la petición.

Detener Aspire con `Ctrl+C`.

## Fase 3 · Añadir Redis como caché

### 3.1 · Añadir los paquetes

```bash
dotnet add AspireDemo.AppHost/AspireDemo.AppHost.csproj \
  package Aspire.Hosting.Redis \
  --version 17.0.0

dotnet add Api/Api.csproj \
  package Aspire.StackExchange.Redis \
  --version 17.0.0
```

### 3.2 · Añadir Redis al AppHost

```bash
cat > AspireDemo.AppHost/AppHost.cs <<'CSHARP'
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .AddDatabase("aspiredemo");

var subApi = builder.AddDotnetProject(
        "sub-api",
        "../SubApi/SubApi.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpEndpoint(port: 8081, targetPort: 18081)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

var cache = builder.AddRedis("cache");

builder.AddDotnetProject(
        "api",
        "../Api/Api.csproj")
    .WithReference(subApi)
    .WithReference(cache)
    .WaitFor(subApi)
    .WaitFor(cache)
    .WithHttpEndpoint(port: 8080, targetPort: 18080)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
CSHARP
```

### 3.3 · Cachear la respuesta de SubApi

```bash
cat > Api/Program.cs <<'CSHARP'
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

    await cache.StringSetAsync(
        cacheKey,
        value,
        TimeSpan.FromMinutes(1));

    httpContext.Response.Headers["X-Cache"] = "MISS";
    return TypedResults.Text(value);
})
    .WithName("GetCachedDatabaseInfo")
    .WithSummary("Obtiene información de PostgreSQL")
    .WithDescription(
        "Consulta SubApi y cachea la respuesta en Redis durante un minuto.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain");

app.Run();
CSHARP
```

### 3.4 · Probar el acierto de caché

Terminal 1:

```bash
dotnet build
aspire run
```

Terminal 2:

```bash
curl --include http://localhost:8080/database-info
curl --include http://localhost:8080/database-info
```

La primera respuesta debe incluir `X-Cache: MISS`. La segunda debe incluir `X-Cache: HIT` y conservar
exactamente la misma marca temporal:

```text
X-Cache: MISS
aspiredemo | 2026-11-25T16:44:01.233000+00

X-Cache: HIT
aspiredemo | 2026-11-25T16:44:01.233000+00
```

En el Dashboard:

1. comparar las dos trazas;
2. mostrar que la segunda no llama a SubApi;
3. abrir los logs de Redis si se quiere profundizar;
4. ejecutar una tercera llamada después de un minuto para volver a `MISS`.

## Fase 4 · Publicar a Docker Compose

Detener `aspire run`.

```bash
aspire add docker --version 17.0.0
```

El comando añade el paquete `Aspire.Hosting.Docker`. Después, añadir el entorno de publicación al inicio
de `AspireDemo.AppHost/AppHost.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");
```

El resto del archivo no cambia.

Generar los artefactos:

```bash
aspire publish -o aspire-output
find aspire-output -maxdepth 3 -type f -print
```

La publicación usa las relaciones del mismo AppHost: proyectos, PostgreSQL, Redis y red compartida. Si el
tiempo lo permite, abrir el archivo Compose y localizar:

- `api`;
- `sub-api`;
- `postgres`;
- `cache`;
- las variables de conexión generadas;
- las dependencias entre servicios.

## Comandos de diagnóstico

Con la aplicación en ejecución:

```bash
aspire describe
aspire describe --format mermaid
aspire ps
```

Comprobación de salud:

```bash
curl --fail http://localhost:8080/health
curl --fail http://localhost:8081/health
```

## Plan B si algo falla en directo

| Incidencia | Plan B |
|---|---|
| Descarga lenta de imágenes | Mostrar el repositorio final y continuar con `aspire describe` sobre una ejecución ya preparada |
| Puerto `8080` u `8081` ocupado | Usar la URL externa que muestra el Dashboard |
| Contenedor `postgres` o `cache` no arranca | Reiniciarlo desde el Dashboard y mostrar los logs |
| `X-Cache` sigue en `MISS` | Esperar un segundo, confirmar que `cache` está healthy y repetir |
| Se acaba el tiempo antes de PostgreSQL | Saltar la edición y mostrar la fase final del repositorio |
| `aspire publish` tarda demasiado | Mostrar `aspire-output/` generado durante la preparación |

## Resultado final de la demo

```text
api (8080)
  ├── llama a sub-api por service discovery
  └── guarda la respuesta en cache (Redis)

sub-api (8081)
  └── consulta aspiredemo en PostgreSQL
```

El código final comprobado está en [`src/`](./src/). El AppHost usa Aspire 17.0 y registra los cuatro
recursos mediante el modelo por ruta recomendado en esta versión.
