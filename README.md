# Experience

[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-experience](https://github.com/crgolden/Experience/actions/workflows/main_crgolden-experience.yml/badge.svg)](https://github.com/crgolden/Experience/actions/workflows/main_crgolden-experience.yml)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=crgolden_Experience&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=crgolden_Experience)

A full-stack single-page application built with **Angular 21** and **ASP.NET Core 10**, using the [Backend-for-Frontend (BFF)](https://www.duendesoftware.com/products/bff) security pattern to handle OIDC authentication on the server side.

## Architecture

```
┌─────────────────────┐        ┌──────────────────────────┐
│  Angular 21 (SPA)   │◄──────►│  ASP.NET Core 10 (BFF)   │
│  :50212 (dev)       │        │  :7150 (dev)             │
└─────────────────────┘        └──────────┬───────────────┘
                                           │
                    ┌──────────────────────┼──────────────────────┐
                    │                      │                      │
             ┌──────▼──────┐    ┌──────────▼──────┐    ┌─────────▼──────┐
             │  Azure Key  │    │  Azure Monitor  │    │  Elasticsearch │
             │    Vault    │    │  + OpenTelemetry│    │  + Serilog     │
             └─────────────┘    └─────────────────┘    └────────────────┘
```

**Backend (`Experience.Server/`)**
- Minimal API with controller-based routing
- [Duende BFF](https://docs.duendesoftware.com/identityserver/v7/bff/) proxies OIDC login/logout and secures API calls
- All secrets (OIDC client credentials, Elasticsearch credentials) fetched at startup from **Azure Key Vault**
- Data protection keys stored in **Azure Blob Storage**, encrypted with an **Azure Key Vault** key
- Distributed tracing and metrics via **OpenTelemetry** exported to **Azure Monitor**
- Structured logging via **Serilog** → Elasticsearch (production) / console (development)

**Frontend (`experience.client/`)**
- Angular signals for reactive session state
- Calls `bff/user` to resolve the authenticated session and display user claims
- Proxies API requests to the ASP.NET Core backend in development

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 |
| Node.js | 22+ |
| npm | 11.9.0 |

**Azure resources (production / staging):**
- Azure Key Vault with the secrets listed below
- Azure Blob Storage container for data protection keys
- Azure Monitor workspace (Application Insights connection string)
- Elasticsearch cluster

## Configuration

In development, user secrets are used (ID `5480cab8-b41b-4dae-8c41-dbc2c01a15e0`). Set the following:

```jsonc
{
  // Azure credential options (DefaultAzureCredential)
  "TenantId": "<tenant-id>",

  // Key Vault URI
  "KeyVaultUri": "https://<vault-name>.vault.azure.net/",

  // Azure Blob Storage URI for data protection keys
  "BlobUri": "https://<account>.blob.core.windows.net/<container>/keys.xml",

  // Azure Key Vault key URI for data protection key encryption
  "DataProtectionKeyIdentifier": "https://<vault-name>.vault.azure.net/keys/<key-name>/<version>",

  // Elasticsearch node URI
  "ElasticsearchNode": "https://<host>:9200",

  // OpenID Connect options
  "OpenIdConnectOptions": {
    "Scope": [ "openid", "profile" ],
    "SaveTokens": true,
    "GetClaimsFromUserInfoEndpoint": true,
    "MapInboundClaims": false
  }
}
```

**Key Vault secrets required at runtime:**

| Secret name | Description |
|-------------|-------------|
| `ElasticsearchUsername` | Elasticsearch basic auth username |
| `ElasticsearchPassword` | Elasticsearch basic auth password |
| `OidcAuthority` | OIDC provider authority URL |
| `ExperienceClientId` | OIDC client ID |
| `ExperienceClientSecret` | OIDC client secret |

## Local Development

The easiest way to run the full stack locally is to open the solution in **Visual Studio** and run the `https` launch profile — this starts the ASP.NET Core backend and the Angular dev server together via the SPA proxy.

Alternatively, run each manually:

**Backend**
```bash
dotnet run --project Experience.Server
# Available at https://localhost:7150
```

**Frontend** (separate terminal)
```bash
cd experience.client
npm install
npm start
# Available at https://localhost:50212
```

The Angular dev server proxies `/bff` and other API paths to `https://localhost:7150` via `src/proxy.conf.js`.

## Building

**Backend**
```bash
dotnet build
```

**Frontend**
```bash
cd experience.client
npm run build
# Output → experience.client/dist/experience.client/browser/
```

In production, the ASP.NET Core app serves the Angular build output via `UseDefaultFiles()` + `MapStaticAssets()`.

## Testing

**Backend unit tests**
```bash
dotnet test
```

Tests use [xUnit v3](https://xunit.net/) and [Moq](https://github.com/devlooped/moq). Azure SDK clients (`SecretClient`) are mocked via Moq, which intercepts the internal 4-parameter routing method to avoid requiring live Azure credentials.

**Frontend unit tests**
```bash
cd experience.client
npm test
```

Frontend tests use [Vitest](https://vitest.dev/).

## Deployment

The application is deployed to **Azure App Service** via the GitHub Actions workflow at `.github/workflows/main_crgolden-experience.yml`. The workflow triggers on pushes to `main`.

Code quality is continuously monitored by [SonarCloud](https://sonarcloud.io/summary/new_code?id=crgolden_Experience).
