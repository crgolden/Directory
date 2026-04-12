# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Full-stack SPA: **Angular 21.2.0** frontend (`experience.client/`) + **ASP.NET Core 10.0** BFF backend (`Experience.Server/`). Both projects are wired together via SPA proxy — the Angular dev server (port 50212) proxies API and BFF requests to the ASP.NET backend (port 7150).

## Common Commands

### Backend (run from repo root or `Experience.Server/`)
```bash
dotnet run --project Experience.Server         # Start backend (https://localhost:7150)
dotnet build                                   # Build solution
dotnet test                                    # Run backend tests
```

### Frontend (run from `experience.client/`)
```bash
npm start          # Run Angular dev server (http://localhost:50212)
npm run build      # Production build → dist/experience.client/browser/
npm test           # Run unit tests (Vitest)
```

### Running a single frontend test
```bash
npx vitest run src/app/some.component.spec.ts
```

### Full-stack development
Open the solution in Visual Studio and run with the `https` launch profile, which starts both the ASP.NET backend and the Angular dev server together via the SPA proxy.

## Architecture

### Backend (`Experience.Server/`)
- **Minimal API** pattern: all configuration in `Program.cs`, no `Startup` class.
- **Duende BFF**: authentication uses `AddBff()` + `AddRemoteApis()`. The session cookie is issued by the BFF; the Angular SPA never holds tokens directly.
- **Manuals proxy**: `app.MapRemoteBffApiEndpoint("/manuals", manualsApiAddress).WithAccessToken()` forwards all `/manuals/**` requests to the Manuals service, automatically attaching the user's access token. Covers all `/manuals/api/chats/**` routes (see BFF proxy coverage below).
- In production, ASP.NET serves the Angular build output via `app.UseDefaultFiles()` + `app.MapStaticAssets()`.

### Frontend (`experience.client/src/`)
- **Standalone components** (Angular 21 pattern): no NgModules. Each component declares its `imports` array directly.
- **Zoneless change detection**: `provideZonelessChangeDetection()` is used in `app.config.ts`. Reactivity is driven by Angular signals (`signal`, `computed`). Always use `fixture.detectChanges()` manually in tests.
- **HTTP interceptor** (`app.interceptor.ts`): adds `X-CSRF: 1` header and `withCredentials: true` to every request.
- TypeScript strict mode is enabled (`strict`, `noImplicitOverride`, `noImplicitReturns`, `noFallthroughCasesInSwitch`).

### Dev proxy
`experience.client/src/proxy.conf.json` routes `/bff/**` and `/manuals/**` from the Angular dev server to `https://localhost:7150`.

## Feature Overview

### Home (`/`)
`src/home/` — public landing page with hero section and six benefit cards. CTA adapts based on auth state (login link vs. "View My Products").

### Products (`/products/**`)
`src/products/` — lazy-loaded product inventory feature. **Service calls are currently mocked** (`of(...)`) to unblock UI development while the real Products API is being built. When the API ships, replace the `of(...)` calls in `product.service.ts` with `HttpClient` calls to `/api/products`.

| Path | Component |
|------|-----------|
| `/products` | `ProductListComponent` — table with inline delete confirmation |
| `/products/new` | `ProductFormComponent` (create mode) |
| `/products/:id` | `ProductDetailComponent` — includes "Find Manual" button that pre-populates the Chat |
| `/products/:id/edit` | `ProductFormComponent` (edit mode) |

### Chat (`/chat`)
`src/chat/` — AI-assisted product manual lookup via the Manuals service.

**Chat lifecycle** (managed by `ChatService`):
1. On init: `GET /manuals/api/chats` loads all existing chats for the user into the sidebar (ordered newest first). Each chat shows its `title` if set, or a truncated `chatId` as fallback.
2. "+" button: `POST /manuals/api/chats` creates a new chat; it is prepended to the sidebar and selected automatically.
3. Selecting a sidebar item: `GET /manuals/api/chats/{id}/messages` fetches the full message history. Items with `role: null` are filtered out; `text ?? ''` guards null text.
4. `POST /manuals/api/chats/{id}/messages/stream` streams responses as SSE deltas; the component appends each delta to the last assistant message in real time. After streaming completes, `GET /manuals/api/chats/{id}` refreshes the chat to pick up the auto-generated title.
5. `PATCH /manuals/api/chats/{id}` (Content-Type: `application/merge-patch+json`) — updates the chat title.
6. `DELETE /manuals/api/chats/{id}` — explicit discard only. Chats are **not** deleted on component destroy — they persist in Redis.

**SSE streaming**: uses the Fetch API (not `EventSource`) because the stream endpoint requires a POST body. The `streamMessage()` method in `ChatService` reads `response.body` as a `ReadableStream`, parses `data: {...}\n\n` SSE lines, and emits each `delta.content` string as an `Observable<string>`.

**BFF proxy coverage**: `app.MapRemoteBffApiEndpoint("/manuals", manualsApiAddress).WithAccessToken()` covers all `/manuals/api/chats/**` routes.

**Query param pre-population**: navigating to `/chat?q=...` pre-fills the input box. `ProductDetailComponent` uses this to pass "Help me find the manual for [Name] [Brand] [Model]" when the user clicks "Find Manual".

## Auth Guard

`src/auth/auth.guard.ts` — functional `CanActivateFn` that reads `AuthService.isAuthenticated()` signal. Redirects to `/bff/login` via `window.location.href` if the user is anonymous. Applied to `/products/**` and `/chat`.

## Testing

Three tiers — see [TESTING.md](TESTING.md) for full commands, CI pipeline details, and infrastructure diagrams.

| Tier | Tool | Trait |
|------|------|-------|
| Backend unit | xUnit v3 | `Category=Unit` |
| Frontend unit | Vitest | — |
| E2E regression | Playwright | `Category=E2E` |
| E2E smoke (post-deploy CI only) | Playwright | `Category=Smoke` |

**Frontend unit tests** (`experience.client/`):
- **Framework**: Vitest with `globals: true`, `environment: jsdom`, setup via `src/test-setup.ts`.
- **Pattern**: `TestBed.configureTestingModule` with `AuthService` stubs (using `signal()`), `provideRouter(testRoutes)` with a local `DummyComponent`, and `vi.fn()` for service mocks.
- **Async**: use `firstValueFrom` from `rxjs` to await observables in tests.
- **Streaming tests**: mock `globalThis.fetch` with `vi.spyOn` and a `ReadableStream` that emits pre-encoded SSE chunks.

**E2E / Smoke tests** (`Experience.Tests/`):
- `PlaywrightFixture` boots a real Kestrel server via `ExperienceWebApplicationFactory` on a random local HTTPS port. Playwright points its browser at that local server — not any deployed Azure endpoint.
- When `TEST_USERNAME` and `TEST_PASSWORD` are set (always in CI), `PlaywrightFixture.LoginAsync` performs a real OIDC login against the Identity server and saves the session cookies for reuse across tests. When credentials are absent (local dev), a synthetic `/bff/user` Playwright route mock is used as a fallback.
- All `/manuals/api/**` calls are intercepted by Playwright route mocks (`InMemoryChatsStore`). No real Manuals service is contacted.
- Azure Key Vault **is** contacted at server startup — `az login` required locally.
- Always use `ASPNETCORE_ENVIRONMENT=Development` locally. Never use `CI` outside of an actual CI pipeline.
