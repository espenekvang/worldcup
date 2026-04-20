# Learnings — fixture-updates

## [2026-04-11] Session start
- MatchEntry bruker `init` setters — ALDRI endre til `set`, lag nye instanser
- MatchSchedule er singleton-instans (ikke factory) — må wrappes med MatchScheduleProvider
- Frontend har runtime-validatorer i src/data/index.ts — behold som fallback
- teams.json format: `{ "MEX": { "code": "MEX", "name": "Mexico", "flag": "🇲🇽" } }`
- matches.json er ENESTE sannhetskilde for kampdata (ikke DB)
- Dockerfile kopierer matches.json inn i image — krever persistent volume i produksjon
- API base URL: https://api.wc2026api.com — API-nøkkel i appsettings.json
- MatchEntry må speile JSON-feltene nøyaktig; nye knockout-felter skal inn mellom AwayTeam og AreTeamsUndetermined
- `dotnet build api/WorldCup.Api/` feilet med eksisterende, urelatert `TeamCodeMapper`-mangel i Program.cs
- Testprosjektet for backend bruker net10.0 og xUnit med NSubstitute + FluentAssertions klar for videre testarbeid
- TeamCodeMapper laster `src/data/teams.json` dynamisk og bruker `StringComparer.OrdinalIgnoreCase` for navn-til-kode-oppslag
- Ukjente lagnavn skal logges som warning og returnere `null`, ikke kaste exception

## [2026-04-11] GetScheduledMatchesAsync + MapToLocalMatchIdByMatchNumber
- API confirmed via OpenAPI spec at https://api.wc2026api.com/docs/json: `?status=scheduled` is a valid status param (alongside `live`, `completed`)
- Round filter values: `group, R32, R16, QF, SF, 3rd, final`
- API key is NOT in appsettings.Development.json (only in appsettings.json as empty string) — API requires Bearer auth
- Local matches.json `id` field is sequential 1-based integer — corresponds 1:1 with API's `MatchNumber`
- `MatchSchedule.GetMatch(int matchId)` uses `_matchesById` dictionary keyed by `Id` — so `GetMatch(matchNumber)?.Id` correctly resolves API MatchNumber → local Id
- `MapToLocalMatchIdByMatchNumber` is a one-liner: `schedule.GetMatch(matchNumber)?.Id`
- `GetScheduledMatchesAsync` mirrors `GetCompletedMatchesAsync` exactly, just changes `?status=completed` → `?status=scheduled`
- No new DTOs needed — `Wc2026MatchDto` handles both completed and scheduled responses

## [2026-04-11] ResultFetcherService fixture polling
- `CheckForFixtureUpdatesAsync` should filter `scheduleProvider.Current.GetAllMatches()` to undetermined non-manual knockout matches by excluding stage `group` and keeping `AreTeamsUndetermined == true`.
- Fixture updates must preserve existing team codes when API mapping returns null; only overwrite fields with resolved `TeamCodeMapper.GetCode()` values.
- `MatchFileWriter.WriteAsync()` should be called once per poll with the full replaced match list after collecting all updates.

## [2026-04-11] Admin match override endpoint
- `TeamCodeMapper` only exposes `GetCode(name→code)` — added `IsValidCode(code)` method using `_codesByTeamName.Values.Contains(code, OrdinalIgnoreCase)` to validate incoming team codes
- Admin auth pattern: `[Authorize(Roles = "Admin")]` at method level (same as InvitationsController uses at class level)
- Route on PUT endpoint uses absolute path `/api/admin/matches/{id:int}` to override the `[Route("api/matches")]` class-level route
- `MatchEntry` init-only: must copy ALL fields manually when creating updated entry
- `MatchFileWriter.WriteAsync` takes `IReadOnlyList<MatchEntry>` — use `.ToList()` on the LINQ result (List<T> implements IReadOnlyList<T>)
- 403 for non-admin is handled automatically by ASP.NET Core's `[Authorize(Roles = "Admin")]` — no manual role check needed
- Added match override functionality using standard updateMatchTeams function with partial team updates.

## [2026-04-11] xUnit unit test patterns
- `TeamCodeMapper` accepts `TEAMS_JSON_PATH` env var as first-priority path override — use this in tests to inject a known JSON path without needing a full IWebHostEnvironment setup
- `MatchScheduleProvider` constructor requires a real JSON file path — create a temp file with valid JSON, then delete in Dispose()
- `MatchFileWriter` constructor calls `EnsureSeeded()` — provide a pre-existing JSON file so it skips the seed copy step silently
- All production services (`MatchFileWriter`, `Wc2026ApiClient`, `TeamCodeMapper`) are `sealed` concrete classes — NSubstitute cannot mock them; must use real instances with file-based fixtures
- `ResultFetcherService` uses `IServiceScopeFactory` for DB access — pass a substituted `IServiceScopeFactory` to avoid DB setup; the fixture-update path does not use the scope
- Testing `ResultFetcherService.CheckForFixtureUpdatesAsync` (private) is done by calling `StartAsync` with a short-lived `CancellationTokenSource` and observing side effects (file contents, API call counts)
- `FakeHttpMessageHandler` pattern: subclass `HttpMessageHandler`, override `SendAsync`, capture call count to assert "was not called"
- `ManualOverride = true` entries are excluded from `undeterminedMatchesById` in `CheckForFixtureUpdatesAsync` — confirmed by: callCount.Should().Be(0) when all undetermined matches have ManualOverride = true
- 26 new tests written across 5 test files; total test count = 28 (including 2 pre-existing smoke tests), all pass

## [2026-04-11] Frontend Vitest unit tests for MatchesContext + AdminPanel
- `@testing-library/user-event` is NOT installed in this project — use `fireEvent` from `@testing-library/react` instead
- `vi.stubGlobal('fetch', vi.fn().mockResolvedValue({...}))` is the correct pattern for mocking fetch; call `vi.restoreAllMocks()` / `vi.clearAllMocks()` in beforeEach/afterEach
- `MatchesContext` uses `getMatches()` from `../api/client` which calls `fetch` under the hood — mocking `fetch` globally is sufficient, no need to mock the module
- AdminPanel `<label>Kamp</label>` is NOT associated to its `<select>` via `htmlFor`/`id` — so `getByRole('combobox', { name: /kamp/i })` will fail; use `getAllByRole('combobox')[0]` instead
- `vi.mock('../api/client', ...)` with `importOriginal` works well for partially mocking the client module; `getInvitations` must be mocked or it will try to fetch on mount
- `MatchesProvider` wraps children and initialises with `staticMatches` — no flash of empty; test this with a never-resolving fetch mock
- `useMatches()` outside `<MatchesProvider>` throws `'useMatches must be used within a MatchesProvider'` — confirm by catching the render error
- Final test count: 16 total (8 pre-existing + 4 MatchesContext + 4 AdminMatchOverride), all passing

## [2026-04-11] F4 scope fidelity audit
- `MatchFileWriter` itself implements temp-file writes + reload correctly, but the required missing-file startup seed path is not fully satisfied because `Program.ResolveMatchesJsonPath()` only honors `MATCHES_JSON_PATH` when the file already exists.
- Backend test scope drift: `tests/WorldCup.Api.Tests/UnitTest1.cs` remains in the diff in addition to the 5 requested feature test files, so the test-file count does not match the spec exactly.

## [2026-04-12] T12 backend tests completion
- `MatchesController` admin PUT auth cannot be enforced in controller-unit tests; the meaningful coverage is a valid knockout override happy path that returns `OkObjectResult` and a `MatchResponse`.
- `MatchScheduleProvider` concurrency coverage is best expressed by concurrent `Reload()` calls against a temp-file-backed provider and asserting no exceptions plus a valid current schedule.
- `ResultFetcherService` fixture-update write-path testing is most reliable by invoking the private `CheckForFixtureUpdatesAsync` directly via reflection instead of relying on the background loop timing.
