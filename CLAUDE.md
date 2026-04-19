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
`experience.client/src/proxy.conf.json` routes `/bff/**`, `/manuals/**`, and `/products/**` from the Angular dev server to `https://localhost:7150`.

## Feature Overview

### Home (`/`)
`src/home/` — public landing page with hero section and six benefit cards. CTA adapts based on auth state (login link vs. "View My Products").

### Products (`/products/**`)
`src/products/` — lazy-loaded product inventory feature. Backed by the live Products API (OData / MongoDB) proxied through the BFF.

**OData endpoint paths** (BFF strips `/products/api` prefix, forwards to Products API):

| Angular calls | Forwards to Products API |
|---|---|
| `GET /products/api/odata/Products` | `GET /odata/Products` |
| `GET /products/api/odata/Products(guid)` | `GET /odata/Products(guid)` |
| `POST /products/api/odata/Products` | `POST /odata/Products` |
| `PUT /products/api/odata/Products(guid)` | `PUT /odata/Products(guid)` |
| `PATCH /products/api/odata/Products(guid)` | `PATCH /odata/Products(guid)` |
| `DELETE /products/api/odata/Products(guid)` | `DELETE /odata/Products(guid)` |

**OData query parameters**: `odata-query` npm package builds `$filter`, `$orderby`, `$top`, `$skip`. The list endpoint always sends `$orderby=Name`. Search by name uses `contains(tolower(Name), tolower('term'))`.

**Product model** (`product.model.ts`): `id`, `name`, `price`, `brand`, `modelNumber`, `serialNumber`, `purchaseDate` (ISO date string), `category`, `description`, `manualUrl`, `createdAt`, `updatedAt`. All fields except `id` and `createdAt` are nullable. The list response is an OData envelope `{ value: Product[] }` unwrapped by `ProductService.getAll()`.

**`manualUrl` field**: populated via the embedded Manual Chat Panel on the product create/edit form (see below). Rendered on `ProductDetailComponent` as a "View Manual" external link when set.

| Path | Component |
|------|-----------|
| `/products` | `ProductListComponent` — searchable table with inline delete confirmation |
| `/products/new` | `ProductFormComponent` (create mode, POSTs to OData collection) — embeds `ManualChatPanelComponent` |
| `/products/:id` | `ProductDetailComponent` — 404 navigates to `/products/not-found` |
| `/products/:id/edit` | `ProductFormComponent` (edit mode, PUTs to OData keyed entity) — embeds `ManualChatPanelComponent` |
| `/products/not-found` | `ProductNotFoundComponent` — user-friendly 404 page with "Back to My Products" |

#### Manual Chat Panel (`src/products/manual-chat/`)
A retractable, AI-assisted manual-finder embedded in `ProductFormComponent` (create + edit). It is the only surface for the Manuals chat feature — there is no standalone `/chat` route.

**UI pattern**: collapsed by default as a fixed-position tab button (`.manual-chat-toggle`) anchored to the right edge. Clicking opens a 420px side panel on desktop (≥768px) or a full-screen overlay on mobile (<768px, detected via `window.matchMedia('(max-width: 767px)')`). A close button returns to the collapsed state without unmounting form state.

**Chat lifecycle** (managed by `ChatService` in `src/products/manual-chat/chat.service.ts`):
1. On first user message, if no `chatId` yet, `POST /manuals/api/chats` creates a new chat, then `PATCH /manuals/api/chats/{id}` sets the title to `"Manual: {name} {brand} {modelNumber}"` (truncated to 60 chars) so the chat is recognizable when revisited via the Manuals service.
2. `POST /manuals/api/chats/{id}/messages/stream` streams assistant responses as SSE deltas; each delta is appended to the last assistant message in real time.
3. After each assistant reply lands, a URL regex (`/\bhttps?:\/\/[^\s)>\]"']+/g`) extracts any URLs and renders them as "Use this URL" chip buttons beneath the message. Clicking a chip emits `manualUrlSelected` up to `ProductFormComponent`, which patches `form.controls.manualUrl` and marks it dirty.
4. Each form session starts a fresh chat — there is no product-id index on the Manuals side yet to link existing chats back. Chats still persist in the Manuals API so users can revisit them from that service if needed.

**SSE streaming**: uses the Fetch API (not `EventSource`) because the stream endpoint requires a POST body. `ChatService.streamMessage()` reads `response.body` as a `ReadableStream`, parses `data: {...}\n\n` SSE lines, and emits each `delta.content` string as an `Observable<string>`.

**BFF proxy coverage**: `app.MapRemoteBffApiEndpoint("/manuals", manualsApiAddress).WithAccessToken()` covers all `/manuals/api/chats/**` routes.

## Auth Guard

`src/auth/auth.guard.ts` — functional `CanActivateFn` that reads `AuthService.isAuthenticated()` signal. Redirects to `/bff/login` via `window.location.href` if the user is anonymous. Applied to `/products/**`.

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
- All `/products/api/odata/**` calls are intercepted by Playwright route mocks (`InMemoryProductsStore`). No real Products service is contacted. Use `_fixture.ProductStore.Clear()` + seed calls before each test, then call `_fixture.NewProductsPageAsync()` to navigate to `/products`.
- When `TEST_USERNAME` and `TEST_PASSWORD` are set (always in CI), `PlaywrightFixture.LoginAsync` performs a real OIDC login against the Identity server and saves the session cookies for reuse across tests. When credentials are absent (local dev), a synthetic `/bff/user` Playwright route mock is used as a fallback.
- All `/manuals/api/**` calls are intercepted by Playwright route mocks (`InMemoryChatsStore`). No real Manuals service is contacted.
- Azure Key Vault **is** contacted at server startup — `az login` required locally.
- Always use `ASPNETCORE_ENVIRONMENT=Development` locally. Never use `CI` outside of an actual CI pipeline.
