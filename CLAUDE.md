# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Full-stack SPA: **Angular 21.2.0** frontend (`experience.client/`) + **ASP.NET Core 10.0** backend (`Experience.Server/`). Both projects are wired together via SPA proxy — the Angular dev server (port 50212) proxies API requests to the ASP.NET backend (port 7150).

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
- **Controller-based routing**: controllers in `Controllers/` with `[Route("[controller]")]`.
- In production, ASP.NET serves the Angular build output via `app.UseDefaultFiles()` + `app.MapStaticAssets()`.

### Frontend (`experience.client/src/`)
- **NgModule architecture** (non-standalone components): `AppModule` in `app/app-module.ts`, routing in `app/app-routing-module.ts`.
- HTTP calls use `HttpClient` injected directly into components (no separate service layer yet).
- TypeScript strict mode is enabled (`strict`, `noImplicitOverride`, `noImplicitReturns`, `noFallthroughCasesInSwitch`).

### Dev proxy
`experience.client/src/proxy.conf.js` routes `/weatherforecast` (and similar API paths) from the Angular dev server to `https://localhost:7150`.
