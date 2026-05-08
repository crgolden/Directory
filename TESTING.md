# Testing

The Experience test suite is split across three tiers: **backend unit tests** (xUnit v3), **frontend unit tests** (Vitest), and **browser-based E2E tests** (Playwright). Unit and E2E tests share the same `Experience.Tests` project; frontend tests live inside `experience.client/`.

## Test tiers

| Tier | Trait / tool | Project | Requires Azure? | Requires Angular build? | Runs in CI |
|------|-------------|---------|-----------------|------------------------|------------|
| Backend unit | `Category=Unit` | `Experience.Tests` | No | No | Every push/PR |
| Frontend unit | Vitest | `experience.client` | No | No | Every push/PR |
| E2E (regression) | `Category=E2E` | `Experience.Tests` | No — test server is a static-file-only Kestrel host; all API routes are Playwright mocks | Yes (for static files) | Every push/PR |
| E2E (smoke) | `Category=Smoke` | `Experience.Tests` | No — targets the deployed app directly | No | Post-deploy only |

Smoke tests are a tagged subset of E2E tests (`[Trait("Category", "Smoke")]` on top of `[Trait("Category", "E2E")]`). They compile into the same binary, but run in a different mode: when `SMOKE_BASE_URL` is set, `PlaywrightFixture` skips `ExperienceWebApplicationFactory` entirely and points Playwright straight at that URL. CI sets `SMOKE_BASE_URL` to the Azure App Service URL emitted by the deploy step.

---

## Running tests locally

### Build configurations

`Experience.Server.csproj` has two Angular build targets:

| MSBuild configuration | Angular build | When to use |
|---|---|---|
| `Debug` (default) | `ng build --configuration development` — no optimization, fast (~1 min) | Local development and local test runs |
| `Release` (default) | `ng build --configuration production` — AOT, minification, tree-shaking (~4–5 min) | Local production-bundle testing only |
| `Release /p:AngularConfiguration=ci` | `ng build --configuration ci` — same optimizations as `production`, `enableTelemetry: false` | CI build step — tests run without the `/config/telemetry` fetch |

The `BuildAngularRelease` target accepts `/p:AngularConfiguration=<name>` to select any Angular configuration defined in `angular.json`. The default for Release is `production`. CI explicitly passes `ci`; adding a staging environment requires only a new `environment.staging.ts`, a matching entry in `angular.json`, and `/p:AngularConfiguration=staging` in the pipeline.

Always use `--configuration Debug` for local test runs. There is no reason to pay the production build cost just to run tests — Playwright only needs the files to exist and load in a browser.

### Prerequisites

```powershell
cd experience.client && npm ci        # install Angular dependencies (first time)
dotnet build --configuration Debug    # builds Angular (dev mode) + C# into bin/Debug/
```

No Azure credentials needed. `ExperienceWebApplicationFactory` is a minimal static-file Kestrel server — no Azure Key Vault, no BFF, no `DefaultAzureCredential`. All API routes (`/bff/**`, `/products/api/**`, `/manuals/api/**`, `/catalog/api/**`) are Playwright route mocks in local E2E mode.

The Angular build output must exist at `experience.client/dist/experience.client/browser/` before running E2E tests. The test factory sets the Kestrel web root to that directory; if it is absent the server still starts but serves no static files.

For the `.NET 10 SDK xUnit caveat` (why `dotnet test` doesn't work), see the workspace-level [TESTING.md](../TESTING.md).

### Backend Unit Tests

```powershell
dotnet build Experience.Tests --configuration Debug
.\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe --filter-trait "Category=Unit" --show-live-output on
```

### E2E Tests (critical pre-commit subset — ~5 tests, ~10 min)

A `Category=Critical` trait is applied to the 5 highest-signal E2E tests — the ones most likely to catch a real regression. Run these before every check-in instead of the full suite:

```powershell
dotnet build Experience.Tests --configuration Debug   # includes Angular dev build
.\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe --filter-trait "Category=Critical" --show-live-output on

# Redirect output for in-flight inspection
cmd /c "Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe --filter-trait ""Category=Critical"" --show-live-output on > C:\temp\experience-e2e.txt 2>&1"
```

| Test | File |
|------|------|
| `Create_product_navigates_to_detail_on_success` | `ProductCrudTests.cs` |
| `Edit_product_updates_name_and_returns_to_detail` | `ProductCrudTests.cs` |
| `Delete_product_removes_it_from_the_list` | `ProductCrudTests.cs` |
| `Sending_message_streams_response_and_shows_url_chip` | `ProductManualChatTests.cs` |
| `Catalog_navigates_to_detail_page_when_View_clicked` | `CatalogTests.cs` |

Run the full `Category=E2E` suite before merging a branch or after any infrastructure change.

### Frontend unit tests

```bash
cd experience.client
npx vitest run           # one-shot
npx vitest run --coverage  # with LCOV coverage report → coverage/lcov.info
```

### E2E tests (regression — full suite)

```powershell
dotnet build Experience.Tests --configuration Debug
.\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe --filter-trait "Category=E2E" --show-live-output on
```

### E2E tests (smoke subset only — against a deployed app)

Smoke tests require `SMOKE_BASE_URL` to point Playwright at a running instance. Pass any URL — production, a staging slot, a PR deployment, etc. No Azure CLI login and no Angular build are needed; `ExperienceWebApplicationFactory` is not started.

```powershell
$env:SMOKE_BASE_URL = "https://crgolden-experience.azurewebsites.net"
$env:TEST_USERNAME = "<your-username>"
$env:TEST_PASSWORD = "<your-password>"
.\Experience.Tests\bin\Release\net10.0\Experience.Tests.exe --filter-trait "Category=Smoke" --show-live-output on
```

`SMOKE_BASE_URL` is the same value that CI sets from `steps.deploy-to-webapp.outputs.webapp-url`. Credentials are required whenever `SMOKE_BASE_URL` is set — the fixture will throw if `TEST_USERNAME` or `TEST_PASSWORD` is absent.

### Run all tests in sequence

```powershell
dotnet build Experience.Tests --configuration Debug
.\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe --show-live-output on
cd experience.client && npx vitest run --coverage
```

---

## E2E test infrastructure

`PlaywrightFixture` operates in two modes depending on whether `SMOKE_BASE_URL` is set:

**Local / regression mode** (`SMOKE_BASE_URL` absent — used by `Category=E2E`):
```
PlaywrightFixture (IAsyncLifetime)
  └── ExperienceWebApplicationFactory (custom WebApplication host — NOT WebApplicationFactory<Program>)
        └── Kestrel HTTPS (random loopback port) ← Playwright browser talks to this
              web root = experience.client/dist/experience.client/browser/
              UseDefaultFiles() + MapStaticAssets() — serves Angular SPA
              No BFF, no Azure credentials, no Key Vault
              All API calls (/bff/**, /products/api/**, /manuals/api/**, /catalog/api/**) are Playwright mocks
```

**Smoke / post-deploy mode** (`SMOKE_BASE_URL` set — used by `Category=Smoke` in CI):
```
PlaywrightFixture (IAsyncLifetime)
  └── (no ExperienceWebApplicationFactory)
  BaseAddress = SMOKE_BASE_URL  ← Playwright browser talks directly to the deployed app
```

In local/regression mode, all API requests are intercepted by Playwright route mocks. In smoke mode, real API calls reach the deployed app.

### Authentication

Authentication behaviour differs between factory mode and smoke mode:

**Factory mode** (`SMOKE_BASE_URL` absent — `Category=E2E` and local smoke runs):

The fixture always uses a Playwright route mock for `/bff/user`, regardless of whether `TEST_USERNAME` / `TEST_PASSWORD` are set. Real OIDC login is not attempted because the Kestrel test server listens on a random port that cannot be pre-registered as a redirect URI in the Identity server. The mock returns a synthetic user:

```
{ type: "sub",   value: "e2e-user-id" }
{ type: "name",  value: "E2E Test User" }
{ type: "email", value: "e2e@test.invalid" }
{ type: "sid",   value: "e2e-session" }
```

**Smoke mode** (`SMOKE_BASE_URL` set — `Category=Smoke` in CI):

`PlaywrightFixture.LoginAsync` performs a real OIDC login against the Identity server during fixture initialization. `TEST_USERNAME` and `TEST_PASSWORD` are required; their absence causes a hard failure (`InvalidOperationException`).

1. Navigates to `/bff/login?returnUrl=%2Fproducts` — starts the Duende BFF OIDC challenge.
2. Fills the Identity server login form (`Input.Email` / `Input.Password`) and submits.
3. Waits for the BFF callback to redirect back to `/products`.
4. Saves the authenticated browser storage state (cookies) to a temp file.

Each per-test context is then created with `StorageStatePath` set to this file, so every test starts with a real BFF session — the full OIDC flow, BFF session ticket, and auth guard are exercised.

### Manuals API mocking

All `/manuals/api/**` requests are intercepted by Playwright before they reach the BFF proxy, backed by `InMemoryChatsStore` — a thread-safe in-memory store that mirrors the Manuals service data model. Each test calls `fixture.ChatStore.Clear()` before `NewProductsPageAsync()` to ensure a clean state.

`InMemoryChatsStore` provides:
- `MockManualUrl` *(const)* — canned URL (`https://example.com/manuals/test-manual.pdf`) embedded in both the completion and stream mock responses so the embedded `ManualChatPanelComponent`'s "Use this URL" chip has a deterministic target.
- `CreateChat()` — creates a new in-memory chat
- `CompleteMessage(chatId, input)` — stores user + assistant messages, sets auto-title on first message
- `CompleteStream(chatId, input)` — same as `CompleteMessage` but also returns an SSE body with three deltas ending in `[DONE]`; the middle delta includes `MockManualUrl`
- `GetMockResponse()` — returns the canned assistant response text used by both completion and stream routes

---

## E2E test coverage

### `E2E/ProductManualChatTests.cs` — `[Trait("Category", "E2E")]`

Covers the embedded `ManualChatPanelComponent` on `/products/new`. All `/manuals/api/**` calls are Playwright-mocked via `InMemoryChatsStore`.

| Test | What it verifies |
|------|-----------------|
| `Manual_chat_panel_toggle_is_visible_on_create_form` | The collapsed `.manual-chat-toggle` button renders on the create form; the expanded panel does not. |
| `Manual_chat_panel_opens_and_closes` | Clicking the toggle opens the panel; the panel's close button collapses it again. |
| `Sending_message_streams_response_and_shows_url_chip` | Sending a message triggers the SSE stream; a "Use this URL" chip appears with `title == InMemoryChatsStore.MockManualUrl`. |
| `Clicking_url_chip_populates_manual_url_field` | Clicking a URL chip writes `MockManualUrl` into the form's `#manualUrl` input. |
| `Message_list_scrolls_inside_panel_when_content_overflows` | The message list scroll container (not the page) handles overflow when the conversation grows past the panel height. |
| `Submitting_form_after_chip_click_persists_manual_url_on_product` | After chip selection + form submit, the created product (in `InMemoryProductsStore`) has `ManualUrl == MockManualUrl`. |

---

## CI pipeline

### Build job (every push / PR)

1. Build solution (`dotnet build --no-incremental --configuration Release /p:AngularConfiguration=ci`) — Angular uses `environment.ci.ts` (`enableTelemetry: false`); no `/config/telemetry` call during tests. `dotnet publish` (step 9) rebuilds Angular without the override, producing the `production` bundle (`enableTelemetry: true`) for the deployed artifact.
2. Backend unit tests with coverage (`dotnet-coverage collect … --filter-trait Category=Unit`)
3. Frontend unit tests with coverage (`npx vitest run --coverage`)
4. Azure login (OIDC)
5. Cache + install Playwright Chromium
6. E2E tests with coverage (`dotnet-coverage collect … --filter-trait Category=E2E`)
7. Upload TRX artifacts (`Experience.Tests/bin/Release/net10.0/TestResults/`)
8. Upload test binaries artifact (`Experience.Tests/bin/Release/net10.0/`) — consumed by the smoke job
9. Publish app + SonarCloud analysis

### Smoke job (post-deploy, `main` only)

Runs after the deploy job. Downloads the pre-built `test-binaries` artifact from the build job (no source checkout, no rebuild). Sets `SMOKE_BASE_URL` to the deployed Azure App Service URL emitted by the deploy step, and `TEST_USERNAME` / `TEST_PASSWORD` for real OIDC login. All `/manuals/api/**` calls are still intercepted by Playwright route mocks — no real Manuals service is contacted. No Azure CLI login is needed (no Key Vault, no `ExperienceWebApplicationFactory`).

1. Download `test-binaries` artifact
2. Set `SMOKE_BASE_URL`, `TEST_USERNAME`, `TEST_PASSWORD`
3. Cache + install Playwright Chromium
4. Run `--filter-trait Category=Smoke` (subset of E2E)
5. Upload TRX artifacts

### Playwright browser cache

Both the build and smoke jobs cache the Playwright Chromium binary keyed on the hash of `Experience.Tests/Experience.Tests.csproj`. The cache is stored at `~\AppData\Local\ms-playwright` on Windows runners.
