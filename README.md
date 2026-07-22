# Directory

[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-directory](https://github.com/crgolden/Directory/actions/workflows/main_crgolden-directory.yml/badge.svg)](https://github.com/crgolden/Directory/actions/workflows/main_crgolden-directory.yml)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=crgolden_Directory&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=crgolden_Directory)

ASP.NET Core 10 Minimal API serving a nationwide U.S. church directory, backed by SQL Server through BCL ADO.NET (no EF Core, no Dapper). Public search and church lookup are anonymous; correction submissions require a `directory`-scoped JWT, and moderation/crawl operations require the `churches.mod` claim. Observable via OpenTelemetry (Grafana Alloy) and documented via OpenAPI.

## Sibling Applications

Directory is a **standalone resource server**. It was extracted from the `Churches` repo: the `Church` / `Search` / `Crawling` / `Moderation` / `User` feature slices and the SQL schema now live here, while `Churches` is an Angular SSR app + Node (Express) BFF that proxies to this API.

The end-to-end platform architecture — how this API, the Churches UI/BFF, and the Functions data pipeline fit together (queue cascade, single-writer invariant, corrections lifecycle, Azure hosting/RBAC) — is documented in [Churches/ARCHITECTURE.md](https://github.com/crgolden/Churches/blob/main/ARCHITECTURE.md). This README is the API-level reference.

| Repo | Role | How Directory interacts |
|---|---|---|
| [Identity](https://github.com/crgolden/Identity) | OIDC Identity Provider | Issues the access tokens Directory validates (scope `directory`); the `churches.mod` claim authorizes moderators |
| [Churches](https://github.com/crgolden/Churches) | Angular 21 SSR + Node (Express) BFF | Sole interactive client — the BFF proxies `/directory/api/**` to this API, attaching the user access token when present |
| [Functions](https://github.com/crgolden/Functions) | Azure Functions isolated worker | The crawl/extract/enrich/dedup processing pipeline writes to the same `Directory` SQL database |
| [Infrastructure](https://github.com/crgolden/Infrastructure) | Health monitoring dashboard | Not yet — `DirectoryHealthCheck` is planned; currently covered by Uptime Kuma |

## Tech Stack

- **.NET 10** / ASP.NET Core (Minimal API, feature-folder vertical slices)
- **SQL Server** via BCL ADO.NET — `DbConnection` scoped from `SqlClientFactory.Instance`; schema owned by `Directory.Data` (`Microsoft.Build.Sql` SDK, `Sql150`), deployed as a `.dacpac`
- **JWT Bearer / OIDC** — authorization policies `Directory` (`scope: directory`) and `ChurchesMod` (claim `churches.mod: true`)
- **Azure Service Bus** — correction submissions are enqueued for the processing pipeline
- **OpenAPI** (`Microsoft.AspNetCore.OpenApi`) — contract at `/openapi/v1.json`
- **Azure** — Key Vault (secrets), Blob Storage (data protection keys)
- **OpenTelemetry** → Grafana Alloy (OTLP traces & metrics)
- **Serilog** → Elasticsearch (`logs-app-Directory` data stream)

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/search` | Anonymous | Search churches by free text, location (lat/lng/radius), state, denomination, worship style, accessibility |
| `GET` | `/churches` | Anonymous | Paged church list |
| `GET` | `/churches/{slug}` | Anonymous | Single church by slug |
| `POST` | `/churches` | `churches.mod` | Create a church |
| `PUT` | `/churches/{id:guid}` | `churches.mod` | Replace a church |
| `PATCH` | `/churches/{id:guid}` | `churches.mod` | Partially update a church |
| `DELETE` | `/churches/{id:guid}` | `churches.mod` | Soft-delete a church |
| `POST` | `/churches/{survivingId:guid}/merge/{absorbedId:guid}` | `churches.mod` | Merge two church records (transactional) |
| `GET` | `/crawl-sources` | `churches.mod` | List crawl sources |
| `POST` | `/crawl-sources` | `churches.mod` | Register a crawl source |
| `DELETE` | `/crawl-sources/{id:guid}` | `churches.mod` | Remove a crawl source |
| `POST` | `/crawl-sources/{id:guid}/trigger` | `churches.mod` | Trigger a crawl run |
| `GET` | `/corrections` | `churches.mod` | List submitted corrections |
| `GET` | `/corrections/{id:guid}` | `churches.mod` | Get a single correction |
| `POST` | `/corrections` | `directory` scope | Submit a user correction (enqueued to Service Bus) |
| `PATCH` | `/corrections/{id:guid}/approve` | `churches.mod` | Approve a correction |
| `PATCH` | `/corrections/{id:guid}/reject` | `churches.mod` | Reject a correction |
| `GET` | `/me` | Anonymous | Current identity (`IsAuthenticated`, `Sub`, `Email`, `Name`, `HasModerationScope`) |

OIDC tokens are issued by [Identity](https://github.com/crgolden/Identity); the [Churches](https://github.com/crgolden/Churches) BFF forwards the user token (type `UserOrNone`) when proxying `/directory/api/**`.

## Data Model

The schema is owned by `Directory.Data` (SQL Database Project). Core tables:

| Table | Purpose |
|-------|---------|
| `Directory` | Church records (canonical name, slug, address, contact, confidence score, soft-delete) |
| `Denominations` | Denomination lookup |
| `Campuses` | Multi-campus church locations |
| `Ministries` | Per-church ministries |
| `ServiceSchedules` | Service times |
| `ChurchAttributes` | Key/value enrichment attributes (feed the confidence score) |
| `CrawlSources` | Registered crawl sources and run state |
| `UserCorrections` | Submitted corrections awaiting moderation |
| `MergeAuditLog` | Audit trail for church merges |

`Functions/fn_HaversineDistance` powers radius search. Each church's `ConfidenceScore` is computed by the processing pipeline: `ConfidenceScoreCalculator` lives in the [Functions](https://github.com/crgolden/Functions) repo and runs when `ConfidenceWorker` consumes a `confidence-requests` message after every pipeline write.

## Configuration

In production these are sourced from Azure Key Vault and App Service configuration; locally, use [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) (ID `61549613-3239-4c31-8300-39334a7c2657`).

| Key | Source | Description |
|-----|--------|-------------|
| `OidcAuthority` | Config | OIDC authority URL for JWT validation |
| `SqlConnectionStringBuilder:DataSource` | Config | SQL Server host (catalog defaults to `Directory`) |
| `SqlServerUserId` | Key Vault secret | SQL Server login user |
| `SqlServerPassword` | Key Vault secret | SQL Server login password |
| `ServiceBusNamespace` | Config | Service Bus fully-qualified namespace (production) |
| `ServiceBusConnectionString` | Config | Service Bus connection string (non-production) |
| `ElasticsearchNode` | Config | Elasticsearch node URL |
| `ElasticsearchUsername` | Key Vault secret | Elasticsearch username |
| `ElasticsearchPassword` | Key Vault secret | Elasticsearch password |
| `BlobUri` | Config | Azure Blob Storage URL for data protection keys (production) |
| `DataProtectionKeyIdentifier` | Config | Azure Key Vault key URI for data protection (production) |

## Local Development

```bash
# Prerequisites: User Secrets configured, ASPNETCORE_ENVIRONMENT=Development

# Build
dotnet build Directory/

# Run (https://localhost:7002)
dotnet run --project Directory/

# View OpenAPI doc
curl https://localhost:7002/openapi/v1.json

# Run unit tests (no Azure creds, no live SQL — fully mocked)
dotnet build Directory.Tests.Unit --configuration Debug
.\Directory.Tests.Unit\bin\Debug\net10.0\Directory.Tests.Unit.exe -trait "Category=Unit" -showLiveOutput
```

### Database schema

```bash
# Install sqlpackage once (if not already installed)
dotnet tool install --global microsoft.sqlpackage

dotnet build Directory.Data/Directory.Data.sqlproj --configuration Release
sqlpackage /Action:Publish /SourceFile:Directory.Data/bin/Release/Directory.Data.dacpac /TargetConnectionString:"<connection-string>"
```

See [TESTING.md](TESTING.md) for the full testing guide and CI pipeline details.

## Health Check

```
GET /health
```

Returns `Healthy` when the application is running. No authentication required.

## Deployment

The GitHub Actions workflow (`.github/workflows/main_crgolden-directory.yml`) runs on every push and PR:

1. Build solution (`dotnet build --no-incremental --configuration Release`), which compiles `Directory.Data.sqlproj` to a `.dacpac`
2. Unit tests with coverage (`dotnet coverlet … --filter-trait Category=Unit`, OpenCover → `coverage.opencover.xml`), `ASPNETCORE_ENVIRONMENT=CI`
3. SonarCloud analysis
4. Publish the web app and upload both the app and dacpac artifacts

The deploy job (after a successful build) deploys the `.dacpac` to the production SQL Server via `SqlPackage`, then deploys the web app to **Azure App Service** `crgolden-directory` (Production slot) via Azure OIDC. The database schema is always deployed before the app.
