# match-results — Learnings & Conventions

## Codebase Patterns

### Backend (.NET 10)
- **Controller pattern**: `[ApiController]`, `[Authorize]`, `[Route("api/...")]`, primary constructor DI — see `PredictionsController.cs:12-15`
- **EF entity pattern**: simple POCO with auto-properties, Guid Id — see `Prediction.cs:3-13`
- **DbContext pattern**: `public DbSet<X> Xs => Set<X>()` — see `AppDbContext.cs:8-10`
- **Service registration**: minimal hosting `builder.Services.AddX(...)` — see `Program.cs`
- **Config reading**: `builder.Configuration["Section:Key"] ?? throw new InvalidOperationException(...)` — see `Program.cs:36-41`
- **Scope pattern for singletons**: use `IServiceScopeFactory`, not direct DbContext injection

### Frontend (React 19 + TypeScript)
- **API client**: `request<T>(path, options)` wrapper in `src/api/client.ts` — all API functions use this
- **Context pattern**: createContext → Provider with useEffect fetch → export hook — see `PredictionsContext.tsx`
- **Data flow**: static JSON at build time for match data, API calls for user-specific data
- **CSS**: CSS variables via `var(--color-...)` — NO Tailwind utility overrides to CSS vars

## Critical Constraints
- `MatchSchedule` is a readonly singleton — NEVER modify it
- `matches.json` structure MUST NOT change
- API key NEVER in source code — config only
- `IHttpClientFactory` REQUIRED for HTTP clients (not `new HttpClient()`)
- `IServiceScopeFactory` REQUIRED in BackgroundService for DbContext

## Match ID Mapping Strategy
- WC2026 API uses its own IDs, not 1-104
- Map via `(homeTeamCode, awayTeamCode, kickoffDate)` tuple
- MatchSchedule has all local matches with dates — use to find matching local ID

## Rate Limit Strategy
- 100 req/day total
- Poll every 10 minutes
- Smart polling: only call API if `match.Date + 2.5h < UtcNow` AND match not yet in DB
- One `GET /matches?status=completed` call returns ALL completed matches

## Scoring Model
- 2p: correct outcome (home win / draw / away win)
- 1p: correct home goals
- 1p: correct away goals
- Max 4p per match
- Points for correct goals even with wrong outcome
- Only 90-minute result counts (not ET/penalties)

## Task 2 Notes
- `ScoringService` is registered as a scoped service in `Program.cs`.
- Build is clean after the change.

## Task 7 Notes
- `ResultsContext` should mirror `PredictionsContext` structurally: provider + hook + `createContext(... | null)` guard.
- Public results and auth-gated points can be fetched together in one effect; points should be skipped when `user` is null.
- Wrapping `ResultsProvider` inside `PredictionsProvider` keeps both contexts available to the app tree.

## Task 6 Notes
- ResultsController follows the PredictionsController primary-constructor pattern and uses `GetAuthenticatedUserId()` copied from the existing controller.
- `/api/results/points` calculates per-match points on the fly from `MatchResults` + `Predictions`, so it stays independent of any future `Prediction.Points` field.
- `ResultResponse` and `PointsResponse` are simple DTOs with direct property mapping, matching the existing response DTO style.

## Task 5 Notes
- Added ResultFetcherService as a hosted BackgroundService using IServiceScopeFactory for scoped AppDbContext and ScoringService resolution.
- Smart polling skips API calls unless a scheduled match is past kickoff plus 2.5 hours and still missing from MatchResults.
- Prediction now stores nullable Points, with scoring updated when new completed match results are persisted.
- Used `useResults()` correctly in `MatchCard` to display match results and badge.
- When creating UI components tests that use global contexts, remember to update the test wrappers to include all required context providers (like `ResultsProvider`).

## Task 9 — E2E Verification (2026-04-11)

- Score is rendered as `{homeScore} – {awayScore}` (with em-dash and spaces) in `MatchCard.tsx` line 78
- The `getResults()` API call is unauthenticated (no JWT needed), returns 200 with empty array when no results in DB
- `getUserPoints()` requires JWT, returns 401 — but the catch in ResultsContext clears both results AND points on any error
- For E2E Playwright tests against this app: inject fake auth via `addInitScript` + intercept `/api/results/points` to prevent 401-induced state clear
- Both migrations (AddMatchResult, AddPredictionPoints) applied cleanly with `dotnet ef database update`
- DB file location: `api/WorldCup.Api/worldcup.db`
- All 8 existing tests pass after the new feature implementation
