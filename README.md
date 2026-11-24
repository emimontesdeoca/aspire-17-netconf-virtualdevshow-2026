# Aspire 17: demo de .NET Conf 2026 (En Español)

Material de la sesión **“Aspire: de cero a las últimas novedades de Aspire 17.0”** para
[.NET Conf 2026 (En Español)](https://www.meetup.com/virtualdevshow/events/316451194/) · online ·
25 de noviembre de 2026 · 16:00.

La charla combina una demo progresiva con una introducción a Aspire:

1. dos APIs que se descubren por nombre;
2. PostgreSQL como única dependencia de la API interna;
3. Redis como caché de la API pública;
4. observabilidad en el Dashboard;
5. publicación a Docker Compose.

Después se recorren las novedades de Aspire 17.0 sin mezclarlas con el código de la demo.

## Estructura

```text
.
├── DEMO.md                 # Guion Bash completo para construir la demo desde cero
├── README.md
├── slides/
│   ├── deck.md             # Presentación Marp en español y notas del ponente
│   └── assets/
└── src/
    ├── AspireDemo.sln
    ├── AppHost/            # Recursos, referencias, puertos, health checks y Compose
    ├── ServiceDefaults/    # Telemetría, descubrimiento, resiliencia y health endpoints
    ├── Api/                # API pública: llama a SubApi y cachea en Redis
    └── SubApi/             # API interna: consulta PostgreSQL
```

## Arquitectura final

| Recurso | Función | Dependencias |
|---|---|---|
| `api` | Expuesto en `http://localhost:8080`; llama a `sub-api` y usa Redis | `sub-api`, `cache` |
| `sub-api` | Expuesto en `http://localhost:8081`; consulta la base `aspiredemo` | `aspiredemo` |
| `aspiredemo` | Base PostgreSQL creada por el AppHost | `postgres` |
| `postgres` | PostgreSQL 18.6 | — |
| `cache` | Redis 8.10 | — |
| `compose` | Destino de publicación Docker Compose | Todos los recursos |

La primera llamada a `GET /database-info` devuelve `X-Cache: MISS`; la siguiente, dentro de un minuto,
devuelve `X-Cache: HIT` sin recorrer SubApi ni PostgreSQL.

## Requisitos

- .NET 10 SDK o superior
- Aspire CLI 17.0
- Docker Desktop, Podman u otro runtime compatible

```bash
dotnet --version
aspire --version
docker version
```

## Ejecutar la demo final

Desde la raíz del repositorio:

```bash
dotnet build src/AspireDemo.sln
aspire run --apphost src/AppHost/AppHost.csproj
```

En otra terminal:

```bash
curl --include http://localhost:8080/database-info
curl --include http://localhost:8080/database-info
curl --fail http://localhost:8081/database-info
```

También puedes ejecutar las peticiones de:

- [`src/Api/Api.http`](./src/Api/Api.http)
- [`src/SubApi/SubApi.http`](./src/SubApi/SubApi.http)

## Probar el Dashboard

Con `aspire run` activo:

1. selecciona **Resources** y revisa los cinco recursos;
2. abre `api` y consulta su entorno inyectado;
3. ejecuta dos peticiones y compara las trazas;
4. comprueba que la segunda petición no llega a `sub-api`;
5. abre **All resources** y el grafo de dependencias.

Los proyectos usan el `ServiceDefaults` de Aspire para configurar health checks, descubrimiento de
servicios, resiliencia y OpenTelemetry.

## Publicar a Docker Compose

El AppHost incluye `Aspire.Hosting.Docker` y declara `AddDockerComposeEnvironment("compose")`.

```bash
aspire publish \
  --apphost src/AppHost/AppHost.csproj \
  --output-path aspire-output
```

Los artefactos quedan en `aspire-output/`. La publicación no cambia los recursos ni su relación; solo
aplica un destino distinto al modelo.

## Presentar desde cero

El documento [`DEMO.md`](./DEMO.md) contiene el guion Bash fase a fase, incluyendo:

- scaffolding con `dotnet new aspire`;
- fragmentos completos de `Program.cs` y `AppHost.cs`;
- los comandos exactos de paquetes 17.0.0;
- pruebas con `curl`;
- una tabla de recuperación durante la sesión.

## Slides

La presentación está en [`slides/deck.md`](./slides/deck.md) para
[Marp](https://marp.app). Incluye notas del ponente y está en español.

```bash
cd slides
npx @marp-team/marp-cli deck.md --output deck.html --allow-local-files --html
npx @marp-team/marp-cli deck.md --pdf --output deck.pdf --allow-local-files
npx @marp-team/marp-cli --server .
```

El workflow de GitHub Pages se recompila desde `slides/` al hacer push en `master`.

## Paquetes principales

| Proyecto | Paquetes |
|---|---|
| AppHost | `Aspire.Hosting.AppHost`, `Aspire.Hosting.Dotnet`, `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.Redis`, `Aspire.Hosting.Docker` |
| SubApi | `Aspire.Npgsql` |
| Api | `Aspire.StackExchange.Redis` |
| ServiceDefaults | OpenTelemetry, service discovery y resiliencia de .NET |

Todos los paquetes Aspire fijan la versión **17.0.0** para que el repositorio de la charla sea
reproducible.

## Referencias

- [Novedades de Aspire 17.0](https://aspire.dev/whats-new/aspire-17/)
- [AppHost](https://aspire.dev/get-started/app-host/)
- [Integración PostgreSQL](https://aspire.dev/integrations/databases/postgres/postgres-host/)
- [Cliente PostgreSQL](https://aspire.dev/integrations/databases/postgres/postgres-connect/)
- [Integración Redis](https://aspire.dev/integrations/caching/redis/redis-host/)
- [Cliente Redis](https://aspire.dev/integrations/caching/redis/redis-connect/)
- [Desplegar con Docker Compose](https://aspire.dev/deployment/docker-compose/)
- [Call for Speakers](https://sessionize.com/dotnet-conf-2026/)

La demo usa solo infraestructura local: no necesita suscripciones cloud, claves ni credenciales.
