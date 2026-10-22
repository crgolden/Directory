# Directory

[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-directory](https://github.com/crgolden/Directory/actions/workflows/main_crgolden-directory.yml/badge.svg)](https://github.com/crgolden/Directory/actions/workflows/main_crgolden-directory.yml)

[![Quality gate](https://sonarcloud.io/api/project_badges/quality_gate?project=crgolden_Directory)](https://sonarcloud.io/summary/new_code?id=crgolden_Directory)

An ASP.NET Core 10 Minimal API serving a U.S. church directory from SQL Server through plain ADO.NET (no EF Core, no
Dapper). Search and church lookup are anonymous; submitting a correction needs a JWT whose `scope` includes
`directory`, and moderation, curation and crawl operations need the `churches.mod` claim. Imports, crawl triggers and
corrections are handed to a processing pipeline through Azure Service Bus queues.

How the parts fit together and the rules each keeps: [ARCHITECTURE.md](ARCHITECTURE.md). How to run every test tier:
[TESTING.md](TESTING.md).

## Technologies

- .NET 10, ASP.NET Core Minimal API in feature folders.
- SQL Server through ADO.NET (`SqlClientFactory`); the schema is the `Directory.Data` SQL project (`Microsoft.Build.Sql`),
  published as a `.dacpac` with SqlPackage.
- JWT Bearer authentication against any OIDC provider; Azure Service Bus (sender only); OpenAPI at `/openapi/v1.json`.
- In production only: OpenTelemetry (OTLP), Serilog to Elasticsearch, and Azure Blob Storage plus Key Vault for data
  protection keys.
- xUnit v3 on Microsoft.Testing.Platform, coverlet, Stryker.NET, ReSharper `jb inspectcode` and SonarCloud.

## API

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/search` | anonymous | search by text, location and radius, state, denomination, worship style, accessibility, service day and time; paged |
| `GET` | `/churches`, `/churches/{slug}` | anonymous | paged list; one church by slug, with its schedules, ministries and campuses |
| `POST`, `PUT`, `PATCH`, `DELETE` | `/churches[/{id}]` | `churches.mod` | create, replace, partially update (an absent member is left alone, an explicit `null` clears it), soft-delete |
| `POST` | `/churches/{survivingId}/merge/{absorbedId}` | `churches.mod` | merge two church records in one transaction |
| `POST`, `PUT`, `DELETE` | `/churches/{churchId}/{schedules,ministries,campuses}`, `/{schedules,ministries,campuses}/{id}` | `churches.mod` | curate a church's children |
| `GET`, `POST`, `DELETE` | `/crawl-sources[/{id}]`, `POST /crawl-sources/{id}/trigger` | `churches.mod` | manage crawl sources and queue a crawl |
| `GET` | `/corrections[/{id}]` | `churches.mod` | submitted corrections |
| `POST` | `/corrections` | `directory` scope | submit a correction (queued, not written to SQL) |
| `PATCH` | `/corrections/{id}/approve`, `/corrections/{id}/reject` | `churches.mod` | moderate a correction |
| `POST`, `GET` | `/admin/import` (CSV), `/admin/export` | `churches.mod` | bulk import (one queued geocoding request per row) and CSV export |
| `GET` | `/denominations`, `/me`, `/health` | anonymous | denominations; the caller's identity; a SQL health check |

## Prerequisites

- The .NET 10 SDK (`global.json` pins `10.0.201` and rolls forward to the latest feature band).
- A GitHub personal access token with the `read:packages` scope. The `Shared` package restores from the `crgolden`
  GitHub Packages feed (`NuGet.Config`), which asks for a token even for a public package. Store it as the `GitHub`
  source's credentials in your user NuGet configuration, never in this repo:

  ```powershell
  dotnet nuget update source GitHub --username <your GitHub user> --password <token> --store-password-in-clear-text --configfile "$env:APPDATA\NuGet\NuGet.Config"
  ```

- A full SQL Server instance (not LocalDB, which lacks the full-text features the schema uses) and SqlPackage
  (`dotnet tool install --global microsoft.sqlpackage`).
- An Azure Service Bus namespace's connection string for the queues the API sends to: `geocoding-requests`,
  `scrape-requests` and `contributions`.
- An OIDC provider for the authenticated routes.

## Clone and build

```powershell
git clone https://github.com/crgolden/Directory.git
cd Directory
dotnet tool restore
dotnet build Directory.slnx
```

## Configuration

`appsettings.json` names the catalog `Directory`; `appsettings.Development.json` points at SQL Server on `localhost`
with integrated security and sets `OidcAuthority` to `https://localhost:7261`. Secrets go in User Secrets for the
`Directory` project:

```powershell
dotnet user-secrets set ServiceBusConnectionString "<connection string>" --project Directory
```

| Key | Needed | Purpose |
|---|---|---|
| `OidcAuthority` | always | the OIDC authority whose tokens the API accepts |
| `SqlConnectionStringBuilder:*` | always | `DataSource`, `InitialCatalog`, and `UserID`/`Password` when not using integrated security |
| `ServiceBusConnectionString` | outside Production | the Service Bus namespace to send to (a secret) |
| `ServiceBusNamespace` | Production | the namespace, reached with an Azure credential |
| `BlobUri`, `DataProtectionKeyIdentifier` | Production | Blob container and Key Vault key for data protection |
| `ElasticsearchNode`, `ElasticsearchUsername`, `ElasticsearchPassword` | Production | log shipping |
| `AlloyEndpoint` | Production | OTLP endpoint for traces and metrics |
| `WEBSITE_SITE_NAME` | Production | the service name for logs and telemetry (App Service sets it) |
| `DefaultAzureCredentialOptions` | Production | which Azure credential to use |

## Run locally

Publish the schema, then run the API:

```powershell
dotnet build Directory.Data/Directory.Data.sqlproj --configuration Release
sqlpackage /Action:Publish /SourceFile:Directory.Data/bin/Release/Directory.Data.dacpac /TargetConnectionString:"Server=localhost;Database=Directory;Integrated Security=true;TrustServerCertificate=true"

$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project Directory
```

It listens on `https://localhost:7002`; the OpenAPI document is at `/openapi/v1.json`. The post-deployment script seeds
`Denominations`.

## Tests

```powershell
dotnet test --project Directory.Tests.Unit --configuration Release -- --filter-trait "Category=Unit"
```

The unit tier needs no database or secrets. The integration tier needs the schema published to a catalog named with a
`Test` suffix; [TESTING.md](TESTING.md) has the setup.

## Gate

`gate.ps1` is the maintainers' gate. It dot-sources the gate library script that the `GATE_LIBRARY` environment
variable names, which defines the step functions `gate.ps1` calls; unset, as in a plain clone, it stops at once with
`GATE: FAILED` and records nothing. To gate with a library of your own, set `GATE_LIBRARY` to its path; it must
define every function `gate.ps1` calls, since those functions decide whether each step ran and passed. To gate your own change without one, run the checks CI runs, in this order:

```powershell
dotnet tool restore
dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
dotnet jb inspectcode Directory.slnx --no-build -e=WARNING --output=inspectcode.sarif
dotnet test --project Directory.Tests.Unit --no-build --configuration Release -- --filter-trait "Category=Unit"
dotnet test --project Directory.Tests.Integration --no-build --configuration Release -- --filter-trait "Category=Integration"
dotnet test --project Directory.Tests.FullText --no-build --configuration Release -- --filter-trait "Category=Integration"
Push-Location Directory
dotnet stryker --config-file ../stryker-config.json
dotnet stryker --config-file ../stryker-relevance-config.json
Pop-Location
```

`jb inspectcode` passes only when `inspectcode.sarif` holds no results, and each Stryker pass only at a 100% mutation
score.

## Deployment

`.github/workflows/main_crgolden-directory.yml` builds, inspects and tests every push and pull request. On a push to
`main` the deploy job publishes the dacpac to the production catalog with SqlPackage, then deploys the app to Azure
App Service through OIDC federated credentials; the schema always deploys before the app.
