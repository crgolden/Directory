# Testing

The Experience test suite is split across three tiers: **backend unit tests** (xUnit v3), **frontend unit tests** (Vitest), and **browser-based E2E tests** (Playwright). Unit and E2E tests share the same `Experience.Tests` project; frontend tests live inside `experience.client/`.

## Test tiers

| Tier | Trait / tool | Project | Requires Azure? | Requires Angular build? | Runs in CI |
|------|-------------|---------|-----------------|------------------------|------------|
| Backend unit | `Category=Unit` | `Experience.Tests` | Yes (Key Vault at startup) | No | Every push/PR |
| Frontend unit | Vitest | `experience.client` | No | No | Every push/PR |
| E2E (regression) | `Category=E2E` | `Experience.Tests` | Yes (Key Vault at startup) | Yes | Every push/PR |
| E2E (smoke) | `Category=Smoke` | `Experience.Tests` | Yes (Key Vault at startup) | Yes | Post-deploy only |

Smoke tests are a tagged subset of E2E tests (`[Trait("Category", "Smoke")]` on top of `[Trait("Category", "E2E")]`). They compile into the same binary and run in the same way — CI just filters differently.

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

### E2E tests (smoke subset only)

```bash
export ASPNETCORE_ENVIRONMENT=Development

dotnet test --project Experience.Tests --no-build --configuration Release \
  -- --filter-trait "Category=Smoke"
```

### Run all tests in sequence

```bash
dotnet test --project Experience.Tests --configuration Release
cd experience.client && npx vitest run --coverage
```

---

## E2E test infrastructure

```
PlaywrightFixture (IAsyncLifetime)
  └── ExperienceWebApplicationFactory (WebApplicationFactory<Program>)
        ├── Kestrel host  ← Playwright browser talks to this
        │     web root = experience.client/dist/experience.client/browser/
        │     TestStaticFilesStartupFilter → UseStaticFiles() (serves Angular SPA)
        └── TestServer    ← HttpClient from CreateClient() uses this
```

### Authentication

The Duende BFF's `/bff/user` endpoint is mocked at the Playwright layer — `PlaywrightFixture.NewPageAsync` registers a route intercept that returns a synthetic authenticated user:

```
{ type: "sub",   value: "e2e-user-id" }
{ type: "name",  value: "E2E Test User" }
{ type: "email", value: "e2e@test.invalid" }
{ type: "sid",   value: "e2e-session" }
```

This means the Angular `AuthService` sees an authenticated session and the auth guard allows navigation to `/chat`, without any real OIDC or BFF session being established. No `/test/sign-in` endpoint is needed.

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
8. Publish app + SonarCloud analysis

### Smoke job (post-deploy, `main` only)

Runs after the deploy job. Uses the GitHub Actions **Production** environment (for OIDC secrets and approval gates) but the tests themselves boot a local `ExperienceWebApplicationFactory` Kestrel server — they do **not** connect to the deployed app. All Manuals API and `/bff/user` calls are intercepted by Playwright route mocks, exactly as in the E2E regression job.

1. Build `Experience.Tests`
2. Azure login
3. Cache + install Playwright Chromium
4. Run `--filter-trait Category=Smoke` (subset of E2E)
5. Upload TRX artifacts

### Playwright browser cache

Both the build and smoke jobs cache the Playwright Chromium binary keyed on the hash of `Experience.Tests/Experience.Tests.csproj`. The cache is stored at `~\AppData\Local\ms-playwright` on Windows runners.
