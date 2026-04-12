# Testing

The Experience test suite is split across three tiers: **backend unit tests** (xUnit v3), **frontend unit tests** (Vitest), and **browser-based E2E tests** (Playwright). Unit and E2E tests share the same `Experience.Tests` project; frontend tests live inside `experience.client/`.

## Test tiers

| Tier | Trait / tool | Project | Requires Azure? | Requires Angular build? | Runs in CI |
|------|-------------|---------|-----------------|------------------------|------------|
| Backend unit | `Category=Unit` | `Experience.Tests` | Yes (Key Vault at startup) | No | Every push/PR |
| Frontend unit | Vitest | `experience.client` | No | No | Every push/PR |
| E2E (regression) | `Category=E2E` | `Experience.Tests` | Yes (Key Vault at startup) | Yes | Every push/PR |
| E2E (smoke) | `Category=Smoke` | `Experience.Tests` | No — targets the deployed app directly | No | Post-deploy only |

Smoke tests are a tagged subset of E2E tests (`[Trait("Category", "Smoke")]` on top of `[Trait("Category", "E2E")]`). They compile into the same binary, but run in a different mode: when `SMOKE_BASE_URL` is set, `PlaywrightFixture` skips `ExperienceWebApplicationFactory` entirely and points Playwright straight at that URL. CI sets `SMOKE_BASE_URL` to the Azure App Service URL emitted by the deploy step.

---

## Running tests locally

### Prerequisites

```bash
az login                          # Azure CLI — required for Key Vault at startup
cd experience.client && npm ci    # install Angular dependencies (first time)
npm run build                     # build Angular SPA into dist/ (required for E2E)
```

The Angular build output must exist at `experience.client/dist/experience.client/browser/` before running E2E tests. The test factory sets the Kestrel web root to that directory at runtime; if it is absent the server still starts but serves no static files.

### Backend unit tests

```bash
dotnet test --project Experience.Tests --configuration Release \
  -- --filter-trait "Category=Unit"
```

### Frontend unit tests

```bash
cd experience.client
npx vitest run           # one-shot
npx vitest run --coverage  # with LCOV coverage report → coverage/lcov.info
```

### E2E tests (regression — full suite)

```bash
# ASPNETCORE_ENVIRONMENT=Development loads AzureCliCredential + VisualStudioCredential.
# Never use ASPNETCORE_ENVIRONMENT=CI locally — CI activates pipeline-only steps.
export ASPNETCORE_ENVIRONMENT=Development

dotnet test --project Experience.Tests --no-build --configuration Release \
  -- --filter-trait "Category=E2E"
```

### E2E tests (smoke subset only — against a deployed app)

Smoke tests require `SMOKE_BASE_URL` to point Playwright at a running instance. Pass any URL — production, a staging slot, a PR deployment, etc. No Azure CLI login and no Angular build are needed; `ExperienceWebApplicationFactory` is not started.

```bash
SMOKE_BASE_URL=https://crgolden-experience.azurewebsites.net \
TEST_USERNAME=<your-username> \
TEST_PASSWORD=<your-password> \
dotnet test --project Experience.Tests --no-build --configuration Release \
  -- --filter-trait "Category=Smoke"
```

`SMOKE_BASE_URL` is the same value that CI sets from `steps.deploy-to-webapp.outputs.webapp-url`. Credentials are required whenever `SMOKE_BASE_URL` is set — the fixture will throw if `TEST_USERNAME` or `TEST_PASSWORD` is absent.

### Run all tests in sequence

```bash
dotnet test --project Experience.Tests --configuration Release
cd experience.client && npx vitest run --coverage
```

---

## E2E test infrastructure

`PlaywrightFixture` operates in two modes depending on whether `SMOKE_BASE_URL` is set:

**Local / regression mode** (`SMOKE_BASE_URL` absent — used by `Category=E2E` and `Category=Smoke` without the env var):
```
PlaywrightFixture (IAsyncLifetime)
  └── ExperienceWebApplicationFactory (WebApplicationFactory<Program>)
        ├── Kestrel host  ← Playwright browser talks to this
        │     web root = experience.client/dist/experience.client/browser/
        │     TestStaticFilesStartupFilter → UseStaticFiles() (serves Angular SPA)
        └── TestServer    ← HttpClient from CreateClient() uses this
```

**Smoke / post-deploy mode** (`SMOKE_BASE_URL` set — used by `Category=Smoke` in CI):
```
PlaywrightFixture (IAsyncLifetime)
  └── (no ExperienceWebApplicationFactory)
  BaseAddress = SMOKE_BASE_URL  ← Playwright browser talks directly to the deployed app
```

In both modes, `/manuals/api/**` requests are intercepted by Playwright route mocks (backed by `InMemoryChatsStore`) before they reach the server.

### Authentication

When `TEST_USERNAME` and `TEST_PASSWORD` environment variables are set (always in CI, optionally locally), `PlaywrightFixture.LoginAsync` performs a real OIDC login against the Identity server during fixture initialization:

1. Navigates to `/bff/login?returnUrl=%2Fchat` — starts the Duende BFF OIDC challenge.
2. Fills the Identity server login form (`Input.Email` / `Input.Password`) and submits.
3. Waits for the BFF callback to redirect back to `/chat`.
4. Saves the authenticated browser storage state (cookies) to a temp file.

Each per-test context is then created with `StorageStatePath` set to this file, so every test starts with a real BFF session — the full OIDC flow, BFF session ticket, and auth guard are exercised.

When credentials are **not** set (local development without `TEST_USERNAME` / `TEST_PASSWORD`), the fixture falls back to a Playwright route mock that returns a synthetic `/bff/user` response:

```
{ type: "sub",   value: "e2e-user-id" }
{ type: "name",  value: "E2E Test User" }
{ type: "email", value: "e2e@test.invalid" }
{ type: "sid",   value: "e2e-session" }
```

In CI, missing credentials cause a hard failure (`InvalidOperationException`).

### Manuals API mocking

All `/manuals/api/**` requests are intercepted by Playwright before they reach the BFF proxy, backed by `InMemoryChatsStore` — a thread-safe in-memory store that mirrors the Manuals service data model. Each test calls `fixture.ChatStore.Clear()` before `NewPageAsync()` to ensure a clean state.

`InMemoryChatsStore` provides:
- `CreateChat()` — creates a new in-memory chat
- `CompleteMessage(chatId, input)` — stores user + assistant messages, sets auto-title on first message
- `CompleteStream(chatId, input)` — same as `CompleteMessage` but also returns an SSE body
- `GetMockResponse()` — returns the canned assistant response text used by both completion and stream routes

### Key Vault at startup

`Experience.Server/Program.cs` calls Azure Key Vault during startup (to fetch OIDC client secrets). The test factory replaces Data Protection with ephemeral keys but does not bypass Key Vault. The `az login` credential chain must be active when running E2E tests locally.

In CI, the build job runs `azure/login` before the E2E step and sets `ASPNETCORE_ENVIRONMENT=CI` so the server uses only `AzureCliCredential`.

---

## E2E test coverage

### `E2E/ChatCrudTests.cs` — `[Trait("Category", "E2E")]` + `[Trait("Category", "Smoke")]`

| Test | What it verifies |
|------|-----------------|
| `CanLoadChatPage` | Sidebar label "Chats" and "+" button are visible on `/chat` |
| `EmptyStateShowsNoChatMessage` | "No chats yet." message appears when the chat list is empty |
| `CanCreateChat` | Clicking "+" creates a chat and a `.chat-item` appears in the sidebar |
| `CanDeleteChat` | Deleting a chat and reloading the page shows "No chats yet." |

### `E2E/ChatMessagingTests.cs` — `[Trait("Category", "E2E")]`

| Test | What it verifies |
|------|-----------------|
| `CanSendMessageAndSeeResponse` *(Smoke)* | Sending a message displays the mock assistant reply |
| `AutoTitleSetsAfterFirstMessage` | After the first message, the sidebar item updates to show the input text as the title |
| `StreamingResponseAppearsAfterSend` | The SSE stream endpoint accumulates into the visible assistant bubble |
| `MessageHistoryReloadsOnResume` | Pre-seeded messages appear when selecting an existing chat |
| `MultipleChatsOrderedNewestFirst` | Sidebar orders three pre-seeded chats newest-first |

---

## CI pipeline

### Build job (every push / PR)

1. Build solution (`dotnet build --no-incremental --configuration Release`)
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
