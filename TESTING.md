# Testing

The Experience test suite is split across three tiers: **backend unit tests** (xUnit v3), **frontend unit tests** (Vitest), and **browser-based E2E tests** (Playwright). Unit and E2E tests share the same `Experience.Tests` project; frontend tests live inside `experience.client/`.

Unit test coding standards (MockBehavior.Strict, argument verification, SetupSequence, no control-flow in tests, etc.) are in the workspace-level [Unit Test Standards](../TESTING.md#unit-test-standards). Note for Playwright E2E tests: a `for` or `foreach` is acceptable when it is test setup (e.g. sending N chat messages to prime state) rather than an assertion branch.

## Test tiers

| Tier | Trait / tool | Project | Requires Azure? | Requires Angular build? | Runs in CI |
|------|-------------|---------|-----------------|------------------------|------------|
| Backend unit | `Category=Unit` | `Experience.Tests` | No | No | Every push/PR |
| Frontend unit | Vitest | `experience.client` | No | No | Every push/PR |
| E2E (regression) | `Category=E2E` | `Experience.Tests` | No — test server is a static-file-only Kestrel host; all API routes are Playwright mocks | Yes (for static files) | Every push/PR |
| E2E (smoke) | `Category=Smoke` | `Experience.Tests` | No — targets the deployed app directly | No | Post-deploy only |

Smoke tests are a tagged subset of E2E tests (`[Trait("Category", "Smoke")]` on top of `[Trait("Category", "E2E")]`). They compile into the same binary, but run in a different mode: when `SmokeBaseUrl` is set, `PlaywrightFixture` skips `ExperienceWebApplicationFactory` entirely and points Playwright straight at that URL. CI sets `SmokeBaseUrl` to the Azure App Service URL emitted by the deploy step.

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
.\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe -trait "Category=Unit" -showLiveOutput
```

### E2E Tests (critical pre-commit subset — ~5 tests, ~10 min)

A `Category=Critical` trait is applied to the 5 highest-signal E2E tests — the ones most likely to catch a real regression. Run these before every check-in instead of the full suite:

```powershell
dotnet build Experience.Tests --configuration Debug   # includes Angular dev build
.\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe -trait "Category=Critical" -showLiveOutput

# Redirect output for in-flight inspection
cmd /c "Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe -trait ""Category=Critical"" -showLiveOutput > C:\temp\experience-e2e.txt 2>&1"
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
.\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe -trait "Category=E2E" -showLiveOutput
```

### E2E tests (smoke subset only)

Smoke tests require a running Experience BFF and a real OIDC login. `ExperienceWebApplicationFactory` is not started — Playwright talks directly to the target URL.

Run via the committed helper script, which reads `AdminEmail` and `AdminPassword` from User Secrets (ID `5480cab8-b41b-4dae-8c41-dbc2c01a15e0`) so credentials never need to be set as OS environment variables.

**Local (default):** targets the deployed `https://crgolden-experience.azurewebsites.net`. The deployed Identity server must be configured with reCAPTCHA test keys (in Key Vault) so headless Playwright passes the reCAPTCHA v3 check.

```powershell
# From Experience/
.\Invoke-SmokeTests.ps1
```

**Against a different target:**

```powershell
.\Invoke-SmokeTests.ps1 -BaseUrl https://your-experience-app.azurewebsites.net
```

To add credentials to User Secrets if not already present:

```powershell
dotnet user-secrets --project Experience.Server set AdminEmail "<your-email>"
dotnet user-secrets --project Experience.Server set AdminPassword "<your-password>"
```

Credentials are required whenever `SmokeBaseUrl` is set — the fixture will throw if `AdminEmail` or `AdminPassword` is absent.

### Run all tests in sequence

```powershell
dotnet build Experience.Tests --configuration Debug
.\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe -showLiveOutput
cd experience.client && npx vitest run --coverage
```

---

## E2E test infrastructure

`PlaywrightFixture` operates in two modes depending on whether `SmokeBaseUrl` is set:

**Local / regression mode** (`SmokeBaseUrl` absent — used by `Category=E2E`):
```
PlaywrightFixture (IAsyncLifetime)
  └── ExperienceWebApplicationFactory (custom WebApplication host — NOT WebApplicationFactory<Program>)
        └── Kestrel HTTPS (random loopback port) ← Playwright browser talks to this
              web root = experience.client/dist/experience.client/browser/
              UseDefaultFiles() + MapStaticAssets() — serves Angular SPA
              No BFF, no Azure credentials, no Key Vault
              All API calls (/bff/**, /products/api/**, /manuals/api/**, /catalog/api/**) are Playwright mocks
```

**Smoke / post-deploy mode** (`SmokeBaseUrl` set — used by `Category=Smoke` in CI):
```
PlaywrightFixture (IAsyncLifetime)
  └── (no ExperienceWebApplicationFactory)
  BaseAddress = SmokeBaseUrl  ← Playwright browser talks directly to the deployed app
```

In local/regression mode, all API requests are intercepted by Playwright route mocks. In smoke mode, real API calls reach the deployed app.

### Authentication

Authentication behaviour differs between factory mode and smoke mode:

**Factory mode** (`SmokeBaseUrl` absent — `Category=E2E` and local smoke runs):

The fixture always uses a Playwright route mock for `/bff/user`, regardless of whether `AdminEmail` / `AdminPassword` are set. Real OIDC login is not attempted because the Kestrel test server listens on a random port that cannot be pre-registered as a redirect URI in the Identity server. The mock returns a synthetic user:

```
{ type: "sub",   value: "e2e-user-id" }
{ type: "name",  value: "E2E Test User" }
{ type: "email", value: "e2e@test.invalid" }
{ type: "sid",   value: "e2e-session" }
```

**Smoke mode** (`SmokeBaseUrl` set — `Category=Smoke` in CI):

`PlaywrightFixture.LoginAsync` performs a real OIDC login against the Identity server during fixture initialization. `AdminEmail` and `AdminPassword` are required; their absence causes a hard failure (`InvalidOperationException`).

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
2. Backend unit tests with coverage (`dotnet coverlet … --filter-trait Category=Unit`, OpenCover → `coverage.opencover.xml`)
3. Frontend unit tests with coverage (`npx vitest run --coverage`)
4. Azure login (OIDC)
5. Cache + install Playwright Chromium
6. E2E tests with coverage (`dotnet-coverage collect … --filter-trait Category=E2E`)
7. Upload TRX artifacts (`Experience.Tests/bin/Release/net10.0/TestResults/`)
8. Upload test binaries artifact (`Experience.Tests/bin/Release/net10.0/`) — consumed by the smoke job
9. Publish app + SonarCloud analysis

### Smoke job (post-deploy, `main` only)

Runs after the deploy job. Downloads the pre-built `test-binaries` artifact from the build job (no source checkout, no rebuild). Sets `SmokeBaseUrl` to the deployed Azure App Service URL emitted by the deploy step, and `AdminEmail` / `AdminPassword` for real OIDC login. All `/manuals/api/**` calls are still intercepted by Playwright route mocks — no real Manuals service is contacted. No Azure CLI login is needed (no Key Vault, no `ExperienceWebApplicationFactory`).

1. Download `test-binaries` artifact
2. Set `SmokeBaseUrl`, `AdminEmail`, `AdminPassword`
3. Cache + install Playwright Chromium
4. Run `-trait "Category=Smoke"` (subset of E2E) via the compiled exe; write TRX via `-trx`
5. Upload TRX artifacts

### Playwright browser cache

Both the build and smoke jobs cache the Playwright Chromium binary keyed on the hash of `Experience.Tests/Experience.Tests.csproj`. The cache is stored at `~\AppData\Local\ms-playwright` on Windows runners.

### Playwright reporting

`Experience.Tests` records Playwright diagnostics for every E2E/smoke browser context, then keeps them only when the xUnit test fails. Retained failure folders are written under:

```text
Experience.Tests/bin/<Configuration>/net10.0/TestResults/PlaywrightArtifacts/<E2E|Smoke>/<test-name>/<context-id>/
```

Each retained folder contains:
- `screenshot.png`
- `trace.zip`
- Playwright `.webm` video files
- `browser-log.txt`
- `metadata.json`
- `failure.json`

CI uploads these artifacts separately from TRX:

| Job | Artifact |
|---|---|
| Build E2E | `experience-playwright-artifacts` |
| Post-deploy smoke | `experience-smoke-playwright-artifacts` |

CI also publishes the same TRX outcomes to Azure DevOps and Azure Monitor:

| Target | Configuration |
|---|---|
| Azure DevOps | `https://dev.azure.com/crgolden/`, project `Experience` — published inline by the CI workflow |
| Azure Monitor | Shared Application Insights `crgolden` — `PlaywrightTestRun`/`PlaywrightTestCase` customEvents posted inline by the CI workflow |

CI uses the `AZURE_DEVOPS_EXT_PAT` secret and the `PLAYWRIGHT_APPINSIGHTS_CONNECTION_STRING` variable (set both in the repo's Actions settings). The publish + telemetry logic is inline in the "Publish Playwright results" steps of `.github/workflows/main_crgolden-experience.yml` — there are no standalone scripts.

Provision or repair the workbook (from the Tools workspace):

```powershell
pwsh -NoProfile -File Tools\Azure\Monitor\Ensure-PlaywrightMonitor.ps1
```

The publish/telemetry steps run only in CI; there is no standalone local script to invoke. To inspect the logic, see the workflow YAML above. Do not run Git commands when implementing or verifying Playwright reporting changes.

---

## Local SonarCloud analysis

Generate coverage files first, then run from `Experience/`. Unit coverage is OpenCover (branch-bearing,
via `coverlet.console` pinned in `dotnet-tools.json` — restore with `dotnet tool restore`); E2E coverage
stays VS Coverage XML; the frontend emits LCOV. SonarCloud unions all three.

```powershell
# .NET unit (OpenCover) — the Experience.Server BFF surface is tiny; real client logic is Vitest/LCOV
dotnet build Experience.Tests --configuration Release /p:AngularConfiguration=ci
dotnet tool restore
dotnet coverlet Experience.Tests\bin\Release\net10.0 `
  --target "dotnet" `
  --targetargs "test --project Experience.Tests --no-build --configuration Release -- --filter-trait Category=Unit" `
  --format opencover --output "coverage.opencover.xml" `
  --skipautoprops --exclude-by-attribute GeneratedCodeAttribute `
  --exclude-by-file "**/obj/**" --exclude-by-file "**/Program.cs" `
  --does-not-return-attribute DoesNotReturnAttribute --include "[Experience.Server]*"

# E2E (VS Coverage XML) → coverage-e2e.xml, and frontend LCOV via `npx vitest run --coverage` — see CI.

$env:SONAR_TOKEN = "<token>"
& "$env:SystemDrive\sonar-scanner-8.0.1.6346-windows-x64\bin\sonar-scanner.bat" `
  "-Dsonar.projectKey=crgolden_Experience" `
  "-Dsonar.organization=crgolden" `
  "-Dsonar.sources=Experience.Server,experience.client/src" `
  "-Dsonar.tests=Experience.Tests" `
  "-Dsonar.exclusions=experience.client/aspnetcore-https.js,experience.client/start-os.js,**/bin/**,**/obj/**,**/node_modules/**,**/*.d.ts" `
  "-Dsonar.coverage.exclusions=experience.client/e2e/**,experience.client/src/test-setup.ts" `
  "-Dsonar.test.inclusions=**/*.spec.ts" `
  "-Dsonar.cs.opencover.reportsPaths=coverage.opencover.xml" `
  "-Dsonar.cs.vscoveragexml.reportsPaths=coverage-e2e.xml" `
  "-Dsonar.javascript.lcov.reportPaths=experience.client/coverage/lcov.info"
```

Required coverage files: `coverage.opencover.xml` (unit, OpenCover), `coverage-e2e.xml` (E2E, VS Coverage), `experience.client/coverage/lcov.info`.
