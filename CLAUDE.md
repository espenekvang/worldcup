# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A FIFA World Cup 2026 match-prediction ("tipping") app: React frontend + .NET 10 API, deployed as a single Docker image to Azure Container Apps. UI text, domain terms, and many code comments are in **Norwegian** (liga = betting group/league, bette/tips = predict) — keep new user-facing text in Norwegian.

## Commands

### Frontend (repo root)
```bash
npm run dev                      # Vite dev server on :5173
npm run build                    # tsc -b && vite build
npm test                         # vitest run (all frontend tests)
npx vitest run src/__tests__/MatchCard.test.tsx   # single test file
npx vitest run -t "test name"    # single test by name
```
There is no lint script; `tsc -b` (via `npm run build`) is the type gate.

### Backend
```bash
dotnet build worldcup.sln
dotnet test                      # xunit tests in tests/WorldCup.Api.Tests (FluentAssertions, NSubstitute)
dotnet test --filter "FullyQualifiedName~TeamCodeMapperTests"   # single test class
cd api/WorldCup.Api && dotnet run   # API on http://localhost:5211
```
Local dev needs `api/WorldCup.Api/appsettings.Development.json` (copy from `.example`; requires `Jwt:Key` ≥32 chars, `Google:ClientId`, `Admin:Email`) and a root `.env` with `VITE_GOOGLE_CLIENT_ID`.

EF Core migrations live in `api/WorldCup.Api/Migrations/` and are **applied automatically at startup** by `DatabaseMigrationService` (registered before other hosted services on purpose — order matters). Create new ones with `dotnet ef migrations add <Name>` from `api/WorldCup.Api/`. `Program.cs` uses `UseSqlServer` (Azure SQL in production).

## Architecture

### Deployment shape
One container: the Vite build output is copied into the API's `wwwroot` and served via `UseStaticFiles` + `MapFallbackToFile("index.html")`. In dev they run separately (5173 → 5211, CORS policy "ViteClient"). Push to `main` triggers `.github/workflows/deploy.yml` (GitVersion → ACR build → Container App update with health polling). The Dockerfile deliberately uses the Debian (not Alpine) runtime — Azure SDK native deps segfault on Alpine.

### matches.json is shared, mutable state
`src/data/matches.json` is both imported by the frontend at build time and loaded by the backend at runtime (`MatchScheduleProvider`; path resolved via `MATCHES_JSON_PATH` env var, or copied into the image at `/publish/data/`). The backend **writes back to it** via `MatchFileWriter` as knockout brackets resolve (placeholders like "Vinner kamp 73" get replaced with real teams). Frontend match data can therefore be stale relative to the backend's copy in production.

### Result fetching
`ResultFetcherService` (BackgroundService) polls an external API (`Wc2026ApiClient`, api.wc2026api.com) with a hard **daily budget of ~90 calls** (provider limit 100/24h, tracked in `ApiCallLog`) — be careful not to add code paths that burn extra calls. It uses stage-aware buffers after kickoff, retries with backoff, fills knockout fixtures (refusing to place the same team on both sides), and maps team names to codes via `TeamCodeMapper` (manual overrides dictionary for names Wikipedia/FIFA spell differently).

### Multi-tenancy: betting groups ("ligaer")
Users belong to one or more `BettingGroup`s. The frontend sends the active group in the `X-Group-Id` header on every request (`src/api/client.ts`, from localStorage). Controllers must validate the caller's membership of that group before returning group-scoped data (predictions, chat, leaderboard) — this is the main authorization boundary beyond JWT auth.

### Auth flow
Google Sign-In → backend validates the Google JWT (`AuthController`) → issues an app JWT if the user is admin, an existing user, has a pending email invitation, or presents a valid invite-link token; otherwise 403 → Waiting page. SignalR chat (`ChatHub` at `/hubs/chat`) receives the JWT via the `access_token` query string.

### Feature flags
`Microsoft.FeatureManagement` with a custom `BettingGroupFilter` (enables a flag for specific group ids). Flags come from `appsettings.json`, or from Azure App Configuration when `APP_CONFIGURATION_ENDPOINT` is set (refreshed every 30s, managed identity — no secrets).

### Scoring
`ScoringService`: 2 points for correct outcome (win/draw/loss), +1 per team's exact goal count, max 4 per match. Rules text shown to users is `src/data/regler.md` — keep these in sync. Knockout predictions score against the result after extra time, not penalties.

### Frontend state
One React context per domain in `src/context/` (Auth, Matches, Predictions, Results, BettingGroup, Chat, FeatureFlags), all consuming the single fetch wrapper in `src/api/client.ts` (attaches JWT + `X-Group-Id`, clears auth on 401). Pages in `src/pages/`, presentational components in `src/components/`. Frontend tests live in `src/__tests__/` (Vitest + jsdom + Testing Library, setup in `src/test-setup.ts`).

### Data maintenance scripts
`scripts/refresh-team-stats.py` updates `api/WorldCup.Api/Data/teamStats.json` (FIFA rankings etc. from Wikipedia, idempotent); `TeamStatsService` merges this seed with an optional `IExternalTeamStatsClient` (currently a no-op).
