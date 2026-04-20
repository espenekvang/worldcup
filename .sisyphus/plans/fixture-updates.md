# Automatisk oppdatering av sluttspillkamper

## TL;DR

> **Quick Summary**: Utvid appen til å automatisk hente lagoppdateringer for sluttspillkamper fra WC2026 API-et, oppdatere matches.json, og servere kampdata dynamisk til frontend via nytt API-endepunkt. Inkluderer admin-overstyring.
> 
> **Deliverables**:
> - Utvidet `Wc2026ApiClient` som henter kommende kamper med laginfo
> - `MatchScheduleProvider` wrapper med hot-reload og atomisk filskriving
> - `GET /api/matches` endepunkt for dynamisk kampdata
> - `PUT /api/admin/matches/{id}` for admin-overstyring av lag
> - Frontend refaktorert til å hente kampdata fra API
> - Admin UI for manuell lagoverstyring
> - xUnit testprosjekt for backend + nye Vitest-tester for frontend
> 
> **Estimated Effort**: Medium
> **Parallel Execution**: YES - 4 waves
> **Critical Path**: Task 1 → Task 2 → Task 3 → Task 5 → Task 8 → Task 10 → F1-F4

---

## Context

### Original Request
Brukeren ønsker at sluttspillkampene i VM 2026 tippespill-appen automatisk oppdateres med riktige lag etter hvert som kamper avgjøres. Samme WC2026 API som allerede brukes for resultater skal benyttes.

### Interview Summary
**Key Discussions**:
- Lagdata hentes fra WC2026 API (ikke lokal bracket-beregning)
- matches.json oppdateres som lagringskilde (ikke database)
- MatchSchedule hot-reloades i minnet
- Frontend migreres fra statisk import til nytt API-endepunkt
- Admin skal kunne overstyre lag manuelt

**Research Findings**:
- `MatchEntry` bruker `init` setters — er immutable etter konstruksjon. Må lage nye instanser ved oppdatering.
- `MatchEntry` mangler `homePlaceholder`, `awayPlaceholder`, `group`, `venueId` felt — backend dropper disse ved deserialisering.
- `MatchSchedule` er registrert som singleton-instans (`AddSingleton(MatchSchedule.LoadFromJson(path))`) — kan ikke bare "reloade", må byttes ut med en provider-wrapper.
- `Wc2026MatchDto` har `Home`/`Away` som lagnavn (f.eks. "Mexico"), men `matches.json` bruker 3-bokstavs koder ("MEX"). Trenger reverse lookup fra `teams.json`.
- Dockerfile kopierer matches.json inn i container-imaget — runtime-skriving krever persistent volume.
- Frontend `src/data/index.ts` har runtime-validatorer som kaster ved ugyldig data.

### Metis Review
**Identified Gaps** (addressed):
- Team name → code mapping mangler: Løst med reverse lookup fra teams.json
- `MatchEntry` immutability: Løst med nye instanser (ikke mutable setters)
- Singleton kan ikke reloades: Løst med `MatchScheduleProvider` wrapper
- Concurrent file writes: Løst med `SemaphoreSlim` + atomisk skriving
- Admin override kan overskrives av poll: Løst med `manualOverride` flagg
- Container ephemeral filesystem: Dokumentert krav om persistent volume
- "Taper kamp N" (losers) for bronsefinale: Håndteres i propageringslogikken
- Kickoff-time mapping kan matche feil kamp: Bruk `MatchNumber` som primærnøkkel

---

## Work Objectives

### Core Objective
Automatisk fylle inn lag i sluttspillkamper basert på data fra WC2026 API-et, med admin-overstyring som fallback.

### Concrete Deliverables
- `api/WorldCup.Api/Services/MatchScheduleProvider.cs` — thread-safe wrapper med reload
- `api/WorldCup.Api/Services/MatchFileWriter.cs` — atomisk filskriving med locking
- Utvidet `Wc2026ApiClient.cs` — ny metode `GetScheduledMatchesAsync()`
- Utvidet `ResultFetcherService.cs` — sjekk for lagoppdateringer etter resultat-polling
- `api/WorldCup.Api/Controllers/MatchesController.cs` — GET /api/matches + PUT admin endpoint
- Utvidet `MatchEntry` med manglende felt
- `src/api/client.ts` — ny `getMatches()` funksjon
- `src/context/MatchesContext.tsx` — dynamisk kampdata-provider
- Admin UI i eksisterende `AdminPanel.tsx`
- `tests/WorldCup.Api.Tests/` — xUnit testprosjekt
- Nye Vitest-tester for frontend

### Definition of Done
- [ ] `curl http://localhost:5211/api/matches | jq 'length'` → 104
- [ ] Sluttspillkamper som har fått lag fra API viser lagkoder (ikke null)
- [ ] Admin kan overstyre lag via PUT endepunkt
- [ ] Frontend laster kampdata fra API, ikke statisk import
- [ ] `npm test` → alle tester passerer
- [ ] `dotnet test` → alle tester passerer

### Must Have
- Atomisk filskriving (temp-fil + rename)
- Thread-safe MatchSchedule reload via provider
- Team name → code mapping (reverse lookup)
- Admin-overstyring som ikke overskrives av auto-poll
- `MatchNumber`-basert mapping (ikke bare kickoff-tid)

### Must NOT Have (Guardrails)
- IKKE beregn bracket-propagering lokalt fra resultater
- IKKE migrer til database-lagring for kampdata
- IKKE legg til WebSocket/SSE for sanntidsoppdateringer
- IKKE endre TypeScript `Match` interface-formen
- IKKE legg til nye data-fetching biblioteker (React Query, SWR)
- IKKE lag nye sider/routes — admin-UI i eksisterende `AdminPanel.tsx`
- IKKE endre `MatchEntry` til mutable (`set`) — lag nye instanser
- IKKE slett `src/data/index.ts` eller `matches.json` — behold som fallback

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** — ALL verifisering er agent-utført. Ingen unntak.

### Test Decision
- **Frontend infrastructure exists**: YES (Vitest + jsdom)
- **Backend infrastructure exists**: NO → Settes opp som del av planen
- **Automated tests**: YES (tests-after)
- **Frontend framework**: Vitest
- **Backend framework**: xUnit (nytt prosjekt)

### QA Policy
Hver oppgave MÅ inkludere agent-utførte QA-scenarioer.
Evidence lagres til `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **Backend API**: Bash (curl) — send requests, assert status + response
- **Frontend UI**: Playwright — navigér, interagér, assert DOM, screenshot
- **Filoperasjoner**: Bash — verifiser filinnhold med jq/cat

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start umiddelbart — fundament):
├── Task 1: Utvid MatchEntry med manglende felt [quick]
├── Task 2: MatchScheduleProvider wrapper [quick]
├── Task 3: MatchFileWriter — atomisk filskriving [quick]
├── Task 4: Team name→code reverse lookup [quick]
└── Task 5: xUnit testprosjekt oppsett [quick]

Wave 2 (Etter Wave 1 — kjerne backend):
├── Task 6: Utvid Wc2026ApiClient med GetScheduledMatchesAsync [unspecified-high]
├── Task 7: Utvid ResultFetcherService med fixture-oppdatering [deep]
└── Task 8: MatchesController — GET /api/matches [quick]

Wave 3 (Etter Wave 2 — admin + frontend):
├── Task 9: Admin override endpoint PUT /api/admin/matches/{id} [unspecified-high]
├── Task 10: Frontend getMatches() + MatchesContext [unspecified-high]
└── Task 11: Admin UI for lagoverstyring [visual-engineering]

Wave 4 (Etter Wave 3 — tester):
├── Task 12: Backend-tester (xUnit) [unspecified-high]
└── Task 13: Frontend-tester (Vitest) [unspecified-high]

Wave FINAL (Etter ALLE oppgaver — 4 parallelle reviews):
├── F1: Plan compliance audit (oracle)
├── F2: Code quality review (unspecified-high)
├── F3: Real manual QA (unspecified-high)
└── F4: Scope fidelity check (deep)
→ Presenter resultater → Få eksplisitt bruker-ok

Critical Path: Task 1 → Task 2 → Task 7 → Task 8 → Task 10 → Task 12 → F1-F4
Parallel Speedup: ~60% raskere enn sekvensiell
Max Concurrent: 5 (Wave 1)
```

### Dependency Matrix

| Task | Depends On | Blocks | Wave |
|------|-----------|--------|------|
| 1 | - | 2, 3, 6, 7, 8 | 1 |
| 2 | 1 | 7, 8, 9 | 1 |
| 3 | 1 | 7, 9 | 1 |
| 4 | - | 7 | 1 |
| 5 | - | 12 | 1 |
| 6 | 1 | 7 | 2 |
| 7 | 2, 3, 4, 6 | 9, 12 | 2 |
| 8 | 1, 2 | 10, 12 | 2 |
| 9 | 2, 3 | 11, 12 | 3 |
| 10 | 8 | 11, 13 | 3 |
| 11 | 9, 10 | 13 | 3 |
| 12 | 5, 7, 8, 9 | - | 4 |
| 13 | 10, 11 | - | 4 |

### Agent Dispatch Summary

- **Wave 1**: 5 tasks — T1-T4 → `quick`, T5 → `quick`
- **Wave 2**: 3 tasks — T6 → `unspecified-high`, T7 → `deep`, T8 → `quick`
- **Wave 3**: 3 tasks — T9 → `unspecified-high`, T10 → `unspecified-high`, T11 → `visual-engineering`
- **Wave 4**: 2 tasks — T12 → `unspecified-high`, T13 → `unspecified-high`
- **FINAL**: 4 tasks — F1 → `oracle`, F2 → `unspecified-high`, F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [x] 1. Utvid MatchEntry med manglende felt

  **What to do**:
  - Legg til `homePlaceholder`, `awayPlaceholder`, `group`, `venueId` properties med `[JsonPropertyName]` attributter til `MatchEntry`-klassen
  - Behold `init` setters — IKKE endre til `set`
  - Verifiser at `MatchSchedule.LoadFromJson()` nå deserialiserer alle felt fra matches.json
  - Oppdater `AreTeamsUndetermined` property om nødvendig

  **Must NOT do**:
  - IKKE endre eksisterende properties til mutable (`set`)
  - IKKE legg til felt som ikke finnes i matches.json

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (med Tasks 2, 3, 4, 5)
  - **Blocks**: Tasks 2, 3, 6, 7, 8
  - **Blocked By**: None

  **References**:
  - `api/WorldCup.Api/Services/MatchSchedule.cs:36-54` — Eksisterende `MatchEntry` klasse med `init` setters og `JsonPropertyName` attributter. Legg til nye felt etter samme mønster.
  - `src/types/index.ts:25-35` — Frontend `Match` interface som viser alle felt som backend-endepunktet må returnere: `homePlaceholder`, `awayPlaceholder`, `group`, `venueId`.
  - `src/data/matches.json` — Eksempel på knockout-kamp med alle felt: `id`, `date`, `homeTeam`, `awayTeam`, `homePlaceholder`, `awayPlaceholder`, `stage`, `venueId`.

  **Acceptance Criteria**:
  - [ ] `dotnet build` kompilerer uten feil
  - [ ] `MatchEntry` har properties: `HomePlaceholder`, `AwayPlaceholder`, `Group`, `VenueId`

  **QA Scenarios**:
  ```
  Scenario: MatchEntry deserialiserer alle felt fra matches.json
    Tool: Bash (dotnet run test snippet)
    Preconditions: API-prosjektet kompilerer
    Steps:
      1. Kjør `dotnet build` i api/WorldCup.Api/
      2. Skriv et lite test-script som laster matches.json via MatchSchedule.LoadFromJson og sjekker at kamp 89 har HomePlaceholder = "Vinner kamp 74"
    Expected Result: Alle felt deserialiseres korrekt, HomePlaceholder/AwayPlaceholder er ikke null for knockout-kamper
    Evidence: .sisyphus/evidence/task-1-matchentry-fields.txt

  Scenario: Gruppekamper har Group-felt satt
    Tool: Bash
    Steps:
      1. Last matches.json, filtrer kamper med stage="group"
      2. Verifiser at alle har Group != null
    Expected Result: Alle gruppekamper har Group-felt (f.eks. "A", "B")
    Evidence: .sisyphus/evidence/task-1-group-field.txt
  ```

  **Commit**: YES (gruppe med Tasks 2, 3, 4)
  - Message: `feat(api): add MatchScheduleProvider with hot-reload and atomic file writes`
  - Files: `api/WorldCup.Api/Services/MatchSchedule.cs`

- [x] 2. MatchScheduleProvider wrapper med hot-reload

  **What to do**:
  - Opprett `api/WorldCup.Api/Services/MatchScheduleProvider.cs`
  - Implementer en thread-safe wrapper rundt `MatchSchedule` med `volatile` eller `lock`-beskyttet referanse
  - Legg til `Reload(IReadOnlyList<MatchEntry> matches)` metode som atomisk bytter ut `MatchSchedule`-instansen
  - Legg til `Reload(string jsonPath)` overload som leser fil og bytter
  - Eksponer `MatchSchedule Current { get; }` for lesing
  - Oppdater `Program.cs`: bytt `AddSingleton(MatchSchedule.LoadFromJson(path))` til `AddSingleton<MatchScheduleProvider>()` som initialiseres med path
  - Oppdater alle referanser til `MatchSchedule` i `ResultFetcherService`, `PredictionsController` etc. til å bruke `MatchScheduleProvider.Current`

  **Must NOT do**:
  - IKKE endre `MatchSchedule` klassen selv — den forblir immutable
  - IKKE bruk `ConcurrentDictionary` — lag ny `MatchSchedule`-instans ved reload

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Tasks 3, 4, 5 — men trenger Task 1 ferdig)
  - **Parallel Group**: Wave 1
  - **Blocks**: Tasks 7, 8, 9
  - **Blocked By**: Task 1

  **References**:
  - `api/WorldCup.Api/Services/MatchSchedule.cs:6-34` — `MatchSchedule` klassen som wrapperen skal holde. Merk at konstruktøren tar `IReadOnlyList<MatchEntry>`.
  - `api/WorldCup.Api/Program.cs:14` — `builder.Services.AddSingleton(MatchSchedule.LoadFromJson(matchesJsonPath))` — denne linjen må erstattes med provider-registrering.
  - `api/WorldCup.Api/Services/ResultFetcherService.cs:9` — `MatchSchedule schedule` injisert i konstruktør — må endres til `MatchScheduleProvider`.
  - `api/WorldCup.Api/Controllers/PredictionsController.cs` — Bruker `MatchSchedule` for `IsStageLocked` — må oppdateres.

  **Acceptance Criteria**:
  - [ ] `dotnet build` kompilerer
  - [ ] `MatchScheduleProvider` har `Current` property og `Reload()` metode
  - [ ] Alle eksisterende referanser til `MatchSchedule` bruker nå `provider.Current`

  **QA Scenarios**:
  ```
  Scenario: Provider returnerer gyldig schedule ved oppstart
    Tool: Bash (curl)
    Preconditions: API kjører
    Steps:
      1. Start API med `dotnet run`
      2. `curl http://localhost:5211/api/results` — skal ikke feile (beviser at schedule fungerer)
    Expected Result: HTTP 200/401 (auth-avhengig), ikke 500
    Evidence: .sisyphus/evidence/task-2-provider-startup.txt

  Scenario: Eksisterende funksjonalitet er ubrutt
    Tool: Bash (curl)
    Steps:
      1. `curl http://localhost:5211/api/predictions` med gyldig JWT
      2. Verifiser at stage-locking fortsatt fungerer
    Expected Result: Predictions-endepunktet returnerer data uten 500-feil
    Evidence: .sisyphus/evidence/task-2-existing-functionality.txt
  ```

  **Commit**: YES (gruppe med Tasks 1, 3, 4)
  - Message: `feat(api): add MatchScheduleProvider with hot-reload and atomic file writes`
  - Files: `api/WorldCup.Api/Services/MatchScheduleProvider.cs`, `api/WorldCup.Api/Program.cs`, endrede controllere

- [x] 3. MatchFileWriter — atomisk filskriving med locking

  **What to do**:
  - Opprett `api/WorldCup.Api/Services/MatchFileWriter.cs`
  - Implementer `SemaphoreSlim(1, 1)` for å forhindre concurrent writes
  - Skriv-logikk: serialiser til JSON → skriv til `matches.json.tmp` → `File.Move(tmp, target, overwrite: true)`
  - Etter vellykket skriving: kall `MatchScheduleProvider.Reload()` for å oppdatere in-memory state
  - Legg til startup-logikk: hvis `MATCHES_JSON_PATH` peker til en ikke-eksisterende fil, kopier den innebygde default-filen dit (for container-deploys med persistent volume)

  **Must NOT do**:
  - IKKE skriv direkte til matches.json (alltid via temp-fil)
  - IKKE hold SemaphoreSlim mens du gjør nettverkskall

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Tasks 2, 4, 5)
  - **Parallel Group**: Wave 1
  - **Blocks**: Tasks 7, 9
  - **Blocked By**: Task 1

  **References**:
  - `api/WorldCup.Api/Program.cs:87-102` — `ResolveMatchesJsonPath` som løser filstien. `MatchFileWriter` trenger samme sti.
  - `api/WorldCup.Api/Services/MatchSchedule.cs:27-33` — `LoadFromJson` viser JSON-serialiseringsformatet. `MatchFileWriter` må skrive kompatibelt JSON.
  - `src/data/matches.json` — Målfilen som skrives til. Inspiser formatet for korrekt serialisering (camelCase, indentert).

  **Acceptance Criteria**:
  - [ ] `dotnet build` kompilerer
  - [ ] `MatchFileWriter` bruker `SemaphoreSlim` og atomisk skriving (tmp + move)
  - [ ] Startup seed-logikk kopierer default fil hvis target ikke finnes

  **QA Scenarios**:
  ```
  Scenario: Atomisk skriving oppdaterer fil og in-memory schedule
    Tool: Bash
    Steps:
      1. Les nåværende matches.json og noter antall kamper med homeTeam != null
      2. Kall MatchFileWriter (via test eller admin-endepunkt) for å oppdatere en kamp
      3. Les matches.json igjen og verifiser at endringen er persistert
      4. Verifiser via API at in-memory schedule også er oppdatert
    Expected Result: Fil og in-memory state er synkronisert
    Evidence: .sisyphus/evidence/task-3-atomic-write.txt

  Scenario: Corrupt write scenario — temp-fil ryddes opp
    Tool: Bash
    Steps:
      1. Verifiser at ingen .tmp-filer ligger igjen etter vellykket skriving
    Expected Result: Ingen orphaned .tmp-filer
    Evidence: .sisyphus/evidence/task-3-no-temp-files.txt
  ```

  **Commit**: YES (gruppe med Tasks 1, 2, 4)
  - Message: `feat(api): add MatchScheduleProvider with hot-reload and atomic file writes`
  - Files: `api/WorldCup.Api/Services/MatchFileWriter.cs`

- [x] 4. Team name → code reverse lookup

  **What to do**:
  - Opprett `api/WorldCup.Api/Services/TeamCodeMapper.cs`
  - Last `teams.json` (som har format `{ "MEX": { "code": "MEX", "name": "Mexico", "flag": "🇲🇽" } }`)
  - Bygg en `Dictionary<string, string>` som mapper lagnavn → lagkode (f.eks. "Mexico" → "MEX")
  - Håndter case-insensitive matching
  - Håndter kjente varianter (f.eks. "Korea Republic" → "KOR", "Côte d'Ivoire" → "CIV") — legg til manuelle overrides for kjente avvik
  - Logg warning hvis et lagnavn ikke kan mappes
  - Registrer som singleton i `Program.cs`

  **Must NOT do**:
  - IKKE endre teams.json
  - IKKE hardkod alle mappinger — bygg dynamisk fra teams.json med noen manuelle overrides

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Tasks 1, 2, 3, 5)
  - **Parallel Group**: Wave 1
  - **Blocks**: Task 7
  - **Blocked By**: None

  **References**:
  - `src/data/teams.json` — Kildefilen med team data. Format: `{ "MEX": { "code": "MEX", "name": "Mexico", "flag": "🇲🇽" } }`. Bruk `name`-feltet for reverse lookup.
  - `api/WorldCup.Api/Services/Wc2026ApiClient.cs:84-85` — `Wc2026MatchDto.Home`/`.Away` er lagnavn-strenger (f.eks. "Mexico") som må mappes til koder.

  **Acceptance Criteria**:
  - [ ] `TeamCodeMapper.GetCode("Mexico")` returnerer `"MEX"`
  - [ ] Case-insensitive: `GetCode("mexico")` → `"MEX"`
  - [ ] Ukjent navn returnerer `null` og logger warning

  **QA Scenarios**:
  ```
  Scenario: Mapper kjente lagnavn til korrekte koder
    Tool: Bash
    Steps:
      1. Last TeamCodeMapper med teams.json
      2. Test mapping for 5+ lag: Mexico→MEX, Brazil→BRA, Germany→GER, Japan→JPN, USA→USA
    Expected Result: Alle returnerer korrekt 3-bokstavs kode
    Evidence: .sisyphus/evidence/task-4-team-mapping.txt

  Scenario: Ukjent lagnavn returnerer null
    Tool: Bash
    Steps:
      1. Kall GetCode("NonExistentCountry")
    Expected Result: Returnerer null, logger warning
    Evidence: .sisyphus/evidence/task-4-unknown-team.txt
  ```

  **Commit**: YES (gruppe med Tasks 1, 2, 3)
  - Message: `feat(api): add MatchScheduleProvider with hot-reload and atomic file writes`
  - Files: `api/WorldCup.Api/Services/TeamCodeMapper.cs`

- [x] 5. xUnit testprosjekt oppsett

  **What to do**:
  - Opprett `tests/WorldCup.Api.Tests/` med `dotnet new xunit`
  - Legg til prosjektreferanse til `api/WorldCup.Api/WorldCup.Api.csproj`
  - Legg til testprosjektet i `worldcup.sln`
  - Legg til en enkel smoke test som verifiserer at prosjektet kompilerer og kan kjøre
  - Installer nødvendige pakker: `Moq` eller `NSubstitute` for mocking, `FluentAssertions` (valgfritt)

  **Must NOT do**:
  - IKKE skriv faktiske feature-tester her — kun prosjektoppsett + smoke test

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Tasks 1, 2, 3, 4)
  - **Parallel Group**: Wave 1
  - **Blocks**: Task 12
  - **Blocked By**: None

  **References**:
  - `worldcup.sln` — Solution-filen som testprosjektet legges til i.
  - `api/WorldCup.Api/WorldCup.Api.csproj` — API-prosjektet som refereres.

  **Acceptance Criteria**:
  - [ ] `dotnet test` kjører og passerer smoke test
  - [ ] Testprosjektet er i solution

  **QA Scenarios**:
  ```
  Scenario: dotnet test kjører vellykket
    Tool: Bash
    Steps:
      1. `dotnet test` fra rotmappa
    Expected Result: 1 test passed, 0 failed
    Evidence: .sisyphus/evidence/task-5-dotnet-test.txt
  ```

  **Commit**: YES
  - Message: `chore(test): set up xUnit test project for backend`
  - Files: `tests/WorldCup.Api.Tests/`

- [x] 6. Utvid Wc2026ApiClient med GetScheduledMatchesAsync

  **What to do**:
  - Legg til ny metode `GetScheduledMatchesAsync(CancellationToken ct)` i `Wc2026ApiClient`
  - Kall `GET /matches?status=scheduled` (eller tilsvarende — utforsk API-et først)
  - Returner `List<Wc2026MatchDto>` — samme DTO fungerer (den har allerede `Home`, `Away`, `MatchNumber`)
  - Bruk samme feilhåndtering som `GetCompletedMatchesAsync` (rate limit, timeout, generell exception)
  - Legg til `MapToLocalMatchIdByMatchNumber(int matchNumber, MatchSchedule schedule)` metode som bruker `MatchNumber` for mapping i stedet for kickoff-tid

  **Must NOT do**:
  - IKKE endre eksisterende `GetCompletedMatchesAsync` — legg til ny metode ved siden av
  - IKKE fjern `MapToLocalMatchId` (kickoff-tid mapping) — behold som fallback

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Tasks 7 og 8 etter Task 1 er ferdig, men T7 avhenger av T6)
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 7
  - **Blocked By**: Task 1

  **References**:
  - `api/WorldCup.Api/Services/Wc2026ApiClient.cs:41-73` — Eksisterende `GetCompletedMatchesAsync` metode. Ny metode følger nøyaktig samme mønster (try/catch, rate limit, logging).
  - `api/WorldCup.Api/Services/Wc2026ApiClient.cs:75-78` — Eksisterende `MapToLocalMatchId` med kickoff-tid. Ny `MapToLocalMatchIdByMatchNumber` bruker `MatchNumber` i stedet.
  - `api/WorldCup.Api/Services/Wc2026ApiClient.cs:81-88` — `Wc2026MatchDto` har allerede `MatchNumber`, `Home`, `Away` felt som trengs.

  **Acceptance Criteria**:
  - [ ] `dotnet build` kompilerer
  - [ ] `GetScheduledMatchesAsync` returnerer liste av DTOs
  - [ ] `MapToLocalMatchIdByMatchNumber` mapper MatchNumber til lokal match ID

  **QA Scenarios**:
  ```
  Scenario: GetScheduledMatchesAsync returnerer data (eller tom liste ved feil)
    Tool: Bash (curl mot WC2026 API direkte for å verifisere endepunktet)
    Steps:
      1. Kall API med `curl "https://api.wc2026api.com/matches?status=scheduled"` (eller utforsk tilgjengelige statuser)
      2. Inspiser responsen — noter felt og format
      3. Verifiser at metoden håndterer respons-formatet korrekt
    Expected Result: Returnerer liste av kamper eller tom liste ved feil
    Evidence: .sisyphus/evidence/task-6-scheduled-matches.txt

  Scenario: MapToLocalMatchIdByMatchNumber finner riktig kamp
    Tool: Bash
    Steps:
      1. Verifiser at MatchNumber fra API korrelerer med match ID i matches.json
      2. Test mapping for 3+ kjente kamper
    Expected Result: Korrekt mapping mellom MatchNumber og lokal ID
    Evidence: .sisyphus/evidence/task-6-match-number-mapping.txt
  ```

  **Commit**: YES (gruppe med Tasks 7, 8)
  - Message: `feat(api): add fixture update polling and GET /api/matches endpoint`
  - Files: `api/WorldCup.Api/Services/Wc2026ApiClient.cs`

- [x] 7. Utvid ResultFetcherService med fixture-oppdatering

  **What to do**:
  - Legg til ny metode `CheckForFixtureUpdatesAsync(CancellationToken ct)` i `ResultFetcherService`
  - Etter `CheckForCompletedMatchesAsync`, kall `CheckForFixtureUpdatesAsync`
  - I `CheckForFixtureUpdatesAsync`:
    1. Finn alle sluttspillkamper med `AreTeamsUndetermined == true`
    2. Kall `apiClient.GetScheduledMatchesAsync()` for å hente kamper med laginfo
    3. For hver kamp som har `Home`/`Away` satt: bruk `TeamCodeMapper` for å konvertere til koder
    4. Bruk `MapToLocalMatchIdByMatchNumber` for å finne lokal kamp-ID
    5. Sjekk om kampen har `manualOverride` flagg — i så fall IKKE overskriv
    6. Oppdater matches.json via `MatchFileWriter`
  - Inject `MatchScheduleProvider` (erstatter direkte `MatchSchedule`), `TeamCodeMapper`, og `MatchFileWriter`
  - Oppdater DI-registrering i `Program.cs`
  - VIKTIG: Håndter "Taper kamp N" placeholder for bronsefinale — API-data for tapende lag

  **Must NOT do**:
  - IKKE endre polling-intervall (behold 10 min)
  - IKKE fjern eksisterende resultat-hentingslogikk
  - IKKE overskriv kamper som har `manualOverride: true`
  - IKKE sett team til en kode som ikke finnes i teams.json

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO (avhenger av Tasks 2, 3, 4, 6)
  - **Parallel Group**: Wave 2 (sekvensiell etter dependencies)
  - **Blocks**: Tasks 9, 12
  - **Blocked By**: Tasks 2, 3, 4, 6

  **References**:
  - `api/WorldCup.Api/Services/ResultFetcherService.cs:1-121` — Hele filen. `ExecuteAsync` loop på linje 17-49, `CheckForCompletedMatchesAsync` på linje 51-120. Ny metode legges til etter linje 120 og kalles fra `ExecuteAsync` etter linje 29.
  - `api/WorldCup.Api/Services/MatchSchedule.cs:53` — `AreTeamsUndetermined` property for å finne kamper som trenger oppdatering.
  - `api/WorldCup.Api/Services/Wc2026ApiClient.cs:81-88` — `Wc2026MatchDto` med `Home`, `Away`, `MatchNumber`.
  - `src/data/matches.json` — Merk "Taper kamp 101"/"Taper kamp 102" plassholdere for bronsefinalen — trenger spesialhåndtering for tapende lag.

  **Acceptance Criteria**:
  - [ ] `dotnet build` kompilerer
  - [ ] `CheckForFixtureUpdatesAsync` kalles i poll-loopen
  - [ ] Kamper med laginfo fra API oppdateres i matches.json
  - [ ] `manualOverride` kamper overskrives IKKE
  - [ ] Team names konverteres korrekt til koder

  **QA Scenarios**:
  ```
  Scenario: Fixture-oppdatering fyller inn lag for sluttspillkamper
    Tool: Bash (curl)
    Preconditions: API kjører, WC2026 API har data om kommende kamper
    Steps:
      1. `curl http://localhost:5211/api/matches | jq '.[] | select(.id == 73)'` — noter homeTeam
      2. Vent på poll-syklus (eller trigger manuelt)
      3. Gjenta curl — sjekk om homeTeam er oppdatert
    Expected Result: homeTeam endret fra null til en gyldig lagkode (f.eks. "BRA")
    Evidence: .sisyphus/evidence/task-7-fixture-update.txt

  Scenario: manualOverride forhindrer overskriving
    Tool: Bash (curl)
    Steps:
      1. Sett en kamp med admin override (PUT /api/admin/matches/73)
      2. Trigger poll
      3. Sjekk at kampen beholder admin-satte verdier
    Expected Result: Admin-verdier beholdes etter poll
    Evidence: .sisyphus/evidence/task-7-manual-override-preserved.txt
  ```

  **Commit**: YES (gruppe med Tasks 6, 8)
  - Message: `feat(api): add fixture update polling and GET /api/matches endpoint`
  - Files: `api/WorldCup.Api/Services/ResultFetcherService.cs`

- [x] 8. MatchesController — GET /api/matches

  **What to do**:
  - Opprett `api/WorldCup.Api/Controllers/MatchesController.cs`
  - `GET /api/matches` — returnerer alle kamper fra `MatchScheduleProvider.Current.GetAllMatches()`
  - Returnerer `List<MatchDto>` med felt som matcher frontend `Match` interface: `id`, `date`, `homeTeam`, `awayTeam`, `homePlaceholder`, `awayPlaceholder`, `group`, `stage`, `venueId`
  - Endepunktet skal være `[AllowAnonymous]` eller same auth-nivå som resten
  - Valgfritt: `GET /api/matches/{id}` for enkelt-kamp

  **Must NOT do**:
  - IKKE legg admin-logikk i denne controlleren (det er Task 9)
  - IKKE returner data som ikke matcher frontend Match-typen

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Task 7 etter Task 1, 2 er ferdige)
  - **Parallel Group**: Wave 2
  - **Blocks**: Tasks 10, 12
  - **Blocked By**: Tasks 1, 2

  **References**:
  - `api/WorldCup.Api/Controllers/ResultsController.cs` — Eksisterende controller-mønster å følge for auth, routing, DI injection.
  - `src/types/index.ts:25-35` — Frontend `Match` interface — responsen MÅ matche dette formatet (camelCase JSON).
  - `api/WorldCup.Api/Services/MatchSchedule.cs:19-22` — `GetMatch(id)` og `GetAllMatches()` metoder å bruke.

  **Acceptance Criteria**:
  - [ ] `curl http://localhost:5211/api/matches | jq 'length'` → 104
  - [ ] Hvert objekt har alle felt: id, date, homeTeam, awayTeam, homePlaceholder, awayPlaceholder, group, stage, venueId
  - [ ] camelCase JSON-formatering

  **QA Scenarios**:
  ```
  Scenario: GET /api/matches returnerer alle 104 kamper
    Tool: Bash (curl)
    Steps:
      1. `curl -s http://localhost:5211/api/matches | jq 'length'`
    Expected Result: 104
    Evidence: .sisyphus/evidence/task-8-matches-count.txt

  Scenario: Kamp-objekt har korrekt form
    Tool: Bash (curl)
    Steps:
      1. `curl -s http://localhost:5211/api/matches | jq '.[0] | keys'`
      2. Verifiser at nøklene inkluderer: awayPlaceholder, awayTeam, date, group, homePlaceholder, homeTeam, id, stage, venueId
    Expected Result: Alle forventede felt er til stede
    Evidence: .sisyphus/evidence/task-8-match-shape.txt

  Scenario: Knockout-kamp med plassholder
    Tool: Bash (curl)
    Steps:
      1. `curl -s http://localhost:5211/api/matches | jq '.[] | select(.stage == "round-of-16") | {id, homeTeam, homePlaceholder}'`
    Expected Result: Kamper med homeTeam=null har homePlaceholder-tekst
    Evidence: .sisyphus/evidence/task-8-placeholder.txt
  ```

  **Commit**: YES (gruppe med Tasks 6, 7)
  - Message: `feat(api): add fixture update polling and GET /api/matches endpoint`
  - Files: `api/WorldCup.Api/Controllers/MatchesController.cs`

- [x] 9. Admin override endpoint — PUT /api/admin/matches/{id}

  **What to do**:
  - Legg til `PUT /api/admin/matches/{id}` i `MatchesController` (eller separat admin-controller)
  - `[Authorize]` med admin-sjekk (følg eksisterende admin-auth mønster i appen)
  - Aksepterer body: `{ "homeTeam": "BRA", "awayTeam": "ARG" }` — begge er valgfrie
  - Validering: kun knockout-kamper (stage != "group"), team-koder må eksistere i teams.json
  - Sett `manualOverride: true` flagg på kampen i matches.json (nytt felt)
  - Skriv via `MatchFileWriter` (atomisk + reload)
  - Returner oppdatert kamp-objekt

  **Must NOT do**:
  - IKKE tillat overstyring av gruppekamper
  - IKKE tillat ugyldige lagkoder
  - IKKE tillat at ikke-admin brukere kaller endepunktet

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Task 10)
  - **Parallel Group**: Wave 3
  - **Blocks**: Tasks 11, 12
  - **Blocked By**: Tasks 2, 3

  **References**:
  - `api/WorldCup.Api/Controllers/PredictionsController.cs` — Admin-auth mønster. Se hvordan admin-sjekk gjøres (claims/roles).
  - `api/WorldCup.Api/Services/MatchFileWriter.cs` (fra Task 3) — Brukes for å skrive oppdateringer.
  - `src/data/teams.json` — For validering av lagkoder.

  **Acceptance Criteria**:
  - [ ] Admin kan sette lag: `curl -X PUT .../api/admin/matches/73 -d '{"homeTeam":"BRA"}'` → 200
  - [ ] Ikke-admin får 403
  - [ ] Gruppekamp-overstyring gir 400
  - [ ] Ugyldig lagkode gir 400
  - [ ] `manualOverride` flagg settes i matches.json

  **QA Scenarios**:
  ```
  Scenario: Admin setter lag på knockout-kamp
    Tool: Bash (curl)
    Steps:
      1. `curl -X PUT http://localhost:5211/api/admin/matches/73 -H "Authorization: Bearer $ADMIN_JWT" -H "Content-Type: application/json" -d '{"homeTeam":"BRA","awayTeam":"ARG"}'`
      2. `curl http://localhost:5211/api/matches | jq '.[] | select(.id == 73)'`
    Expected Result: PUT → 200, GET viser homeTeam="BRA", awayTeam="ARG"
    Evidence: .sisyphus/evidence/task-9-admin-override.txt

  Scenario: Ikke-admin får 403
    Tool: Bash (curl)
    Steps:
      1. `curl -X PUT http://localhost:5211/api/admin/matches/73 -H "Authorization: Bearer $USER_JWT" -H "Content-Type: application/json" -d '{"homeTeam":"BRA"}'`
    Expected Result: HTTP 403 Forbidden
    Evidence: .sisyphus/evidence/task-9-non-admin-forbidden.txt

  Scenario: Ugyldig lagkode avvises
    Tool: Bash (curl)
    Steps:
      1. `curl -X PUT http://localhost:5211/api/admin/matches/73 -H "Authorization: Bearer $ADMIN_JWT" -H "Content-Type: application/json" -d '{"homeTeam":"INVALID"}'`
    Expected Result: HTTP 400 Bad Request
    Evidence: .sisyphus/evidence/task-9-invalid-team.txt
  ```

  **Commit**: YES
  - Message: `feat(api): add admin match override endpoint`
  - Files: `api/WorldCup.Api/Controllers/MatchesController.cs`

- [x] 10. Frontend — getMatches() + MatchesContext

  **What to do**:
  - Legg til `getMatches()` funksjon i `src/api/client.ts` som kaller `GET /api/matches`
  - Returtype: `Match[]` (bruk eksisterende `Match` interface fra `src/types/index.ts`)
  - Opprett `src/context/MatchesContext.tsx` etter mønsteret i `ResultsContext.tsx`/`PredictionsContext.tsx`
  - Provider henter kampdata ved mount, lagrer i state, eksponerer via context
  - Refaktorer `App.tsx` og komponenter som bruker `matches` fra `src/data/index.ts` til å bruke `MatchesContext`
  - Behold `src/data/index.ts` og `matches.json` som fallback — hvis API-kallet feiler, bruk statisk data
  - Behold `teams` og `venues` som statisk import (disse endres ikke)

  **Must NOT do**:
  - IKKE slett `src/data/index.ts` eller `src/data/matches.json`
  - IKKE endre `Match` TypeScript interface
  - IKKE legg til React Query, SWR, eller andre data-fetching biblioteker
  - IKKE endre teams/venues til å hentes fra API

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Task 9)
  - **Parallel Group**: Wave 3
  - **Blocks**: Tasks 11, 13
  - **Blocked By**: Task 8

  **References**:
  - `src/api/client.ts:86-88` — Eksisterende `getResults()` funksjon som mønster for `getMatches()`.
  - `src/context/ResultsContext.tsx` — Mønster for context provider med fetch ved mount, error handling.
  - `src/context/PredictionsContext.tsx` — Alternativt context-mønster.
  - `src/data/index.ts:95-103` — Nåværende statisk match-eksport som erstattes av context. Behold som fallback.
  - `src/types/index.ts:25-35` — `Match` interface som API-responsen må matche.

  **Acceptance Criteria**:
  - [ ] `getMatches()` finnes i client.ts og returnerer `Match[]`
  - [ ] `MatchesContext` provider finnes og fungerer
  - [ ] Komponenter bruker context i stedet for statisk import for matches
  - [ ] Fallback til statisk data hvis API feiler
  - [ ] `npm run build` kompilerer uten feil

  **QA Scenarios**:
  ```
  Scenario: Frontend laster kamper fra API
    Tool: Playwright
    Steps:
      1. Åpne appen i browser: `http://localhost:5173`
      2. Åpne Network-tab, filtrer på `/api/matches`
      3. Verifiser at en GET request sendes ved sidelasting
      4. Verifiser at kampkort vises korrekt
    Expected Result: Network request til /api/matches, kampdata vises
    Evidence: .sisyphus/evidence/task-10-api-fetch.png

  Scenario: Fallback til statisk data ved API-feil
    Tool: Playwright
    Steps:
      1. Stopp backend (kill API-prosess)
      2. Refresh frontend
      3. Verifiser at appen fortsatt viser kampdata (fra statisk fallback)
    Expected Result: Kampdata vises selv uten API (degradert modus)
    Evidence: .sisyphus/evidence/task-10-fallback.png
  ```

  **Commit**: YES (gruppe med Task 11)
  - Message: `feat(ui): migrate to dynamic match data from API with admin override UI`
  - Files: `src/api/client.ts`, `src/context/MatchesContext.tsx`, `src/App.tsx`

- [x] 11. Admin UI for lagoverstyring i AdminPanel

  **What to do**:
  - Utvid eksisterende `src/components/AdminPanel.tsx` med en ny seksjon for kamp-overstyring
  - Vis kun sluttspillkamper (stage != "group") i en liste/dropdown
  - For hver kamp: vis nåværende homeTeam/awayTeam (eller placeholder)
  - Lag-velger: dropdown med alle lag fra teams.json (bruk statisk `teams` data)
  - "Lagre"-knapp som kaller `PUT /api/admin/matches/{id}`
  - Legg til `updateMatchTeams(matchId, homeTeam?, awayTeam?)` i `src/api/client.ts`
  - Vis suksess/feil-feedback

  **Must NOT do**:
  - IKKE lag en ny side/route — legg til i eksisterende AdminPanel
  - IKKE vis gruppekamper i listen
  - IKKE endre eksisterende admin-funksjonalitet (invitasjoner)

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO (trenger Tasks 9 og 10)
  - **Parallel Group**: Wave 3 (etter 9, 10)
  - **Blocks**: Task 13
  - **Blocked By**: Tasks 9, 10

  **References**:
  - `src/components/AdminPanel.tsx` — Eksisterende admin-panel komponent. Legg til ny seksjon etter eksisterende invitasjons-UI.
  - `src/data/index.ts:96` — `teams` eksport for lag-dropdown (statisk, endres ikke).
  - `src/api/client.ts:111-122` — `createInvitation`/`deleteInvitation` som mønster for admin API-kall.

  **Acceptance Criteria**:
  - [ ] Admin-panel har ny seksjon for kamp-overstyring
  - [ ] Kun sluttspillkamper vises
  - [ ] Lag kan velges fra dropdown og lagres
  - [ ] Suksess/feil-feedback vises

  **QA Scenarios**:
  ```
  Scenario: Admin kan overstyre lag via UI
    Tool: Playwright
    Steps:
      1. Logg inn som admin
      2. Åpne admin-panelet
      3. Finn kamp-overstyring seksjonen
      4. Velg en sluttspillkamp
      5. Velg homeTeam fra dropdown (f.eks. "Brazil")
      6. Klikk "Lagre"
      7. Verifiser suksess-melding
      8. Naviger til kampvisningen og verifiser at laget vises
    Expected Result: Lag oppdatert i UI og persistert
    Evidence: .sisyphus/evidence/task-11-admin-override-ui.png

  Scenario: Gruppekamper vises IKKE i overstyring
    Tool: Playwright
    Steps:
      1. Åpne admin-panel kamp-overstyring
      2. Søk gjennom listen etter gruppekamper
    Expected Result: Ingen gruppekamper i listen
    Evidence: .sisyphus/evidence/task-11-no-group-matches.png
  ```

  **Commit**: YES (gruppe med Task 10)
  - Message: `feat(ui): migrate to dynamic match data from API with admin override UI`
  - Files: `src/components/AdminPanel.tsx`, `src/api/client.ts`

- [x] 12. Backend-tester (xUnit)

  **What to do**:
  - Skriv enhetstester i `tests/WorldCup.Api.Tests/`:
    - `TeamCodeMapperTests.cs` — test mapping, case-insensitivity, ukjent team
    - `MatchFileWriterTests.cs` — test atomisk skriving, SemaphoreSlim, reload
    - `MatchScheduleProviderTests.cs` — test reload, thread-safety, Current property
    - `MatchesControllerTests.cs` — test GET /api/matches response shape, admin PUT auth, validering
    - `ResultFetcherServiceTests.cs` — test fixture-oppdatering logikk, manualOverride respekt
  - Mock eksterne avhengigheter (HttpClient, ILogger, etc.)

  **Must NOT do**:
  - IKKE skriv integrasjonstester som krever ekstern API
  - IKKE endre produksjonskode for å gjøre den testbar (innen rimelighetens grenser)

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Task 13)
  - **Parallel Group**: Wave 4
  - **Blocks**: None
  - **Blocked By**: Tasks 5, 7, 8, 9

  **References**:
  - Alle service-filer fra Tasks 1-9 som skal testes.
  - `tests/WorldCup.Api.Tests/` (fra Task 5) — Testprosjekt-oppsett.

  **Acceptance Criteria**:
  - [ ] `dotnet test` → alle tester passerer
  - [ ] Minimum 10 tester dekker: mapping, filskriving, reload, auth, validering

  **QA Scenarios**:
  ```
  Scenario: Alle backend-tester passerer
    Tool: Bash
    Steps:
      1. `dotnet test --verbosity normal`
    Expected Result: Alle tester grønne, 0 failures
    Evidence: .sisyphus/evidence/task-12-dotnet-test.txt
  ```

  **Commit**: YES (gruppe med Task 13)
  - Message: `test: add backend and frontend tests for fixture updates`
  - Files: `tests/WorldCup.Api.Tests/`

- [x] 13. Frontend-tester (Vitest)

  **What to do**:
  - Skriv tester i `src/__tests__/`:
    - `MatchesContext.test.tsx` — test at context henter fra API, fallback til statisk data, error handling
    - `AdminMatchOverride.test.tsx` — test admin-UI for lagoverstyring (render, submit, validering)
  - Mock `fetch` for API-kall (følg eksisterende test-mønster)
  - Verifiser at eksisterende tester fortsatt passerer

  **Must NOT do**:
  - IKKE endre eksisterende tester
  - IKKE legg til nye test-dependencies

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (med Task 12)
  - **Parallel Group**: Wave 4
  - **Blocks**: None
  - **Blocked By**: Tasks 10, 11

  **References**:
  - `src/__tests__/MatchCard.test.tsx` — Eksisterende test-mønster med Vitest + testing-library.
  - `src/test-setup.ts` — Test setup med @testing-library/jest-dom.
  - `vite.config.ts` — Vitest konfigurasjon (jsdom, globals).

  **Acceptance Criteria**:
  - [ ] `npm test` → alle tester passerer (gamle + nye)
  - [ ] Minimum 4 nye tester: API fetch, fallback, admin render, admin submit

  **QA Scenarios**:
  ```
  Scenario: Alle frontend-tester passerer
    Tool: Bash
    Steps:
      1. `npm test`
    Expected Result: Alle tester grønne, 0 failures
    Evidence: .sisyphus/evidence/task-13-npm-test.txt
  ```

  **Commit**: YES (gruppe med Task 12)
  - Message: `test: add backend and frontend tests for fixture updates`
  - Files: `src/__tests__/`

---

## Final Verification Wave

> 4 review-agenter kjører PARALLELT. ALLE må GODKJENNE. Presenter konsoliderte resultater og få eksplisitt "ok" fra bruker.

- [x] F1. **Plan Compliance Audit** — `oracle`
  Les planen fra start til slutt. For hvert "Must Have": verifiser at implementasjon finnes (les fil, curl endepunkt, kjør kommando). For hvert "Must NOT Have": søk i kodebasen etter forbudte mønstre — avvis med fil:linje hvis funnet. Sjekk at evidence-filer finnes i .sisyphus/evidence/. Sammenlign leveranser mot plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [x] F2. **Code Quality Review** — `unspecified-high`
  Kjør `dotnet build` + `npm test` + `dotnet test`. Gjennomgå alle endrede filer for: `as any`/`@ts-ignore`, tomme catches, console.log i prod, utkommentert kode, ubrukte imports. Sjekk AI-slop: overdrevne kommentarer, over-abstraksjon, generiske navn.
  Output: `Build [PASS/FAIL] | Tests [N pass/N fail] | Files [N clean/N issues] | VERDICT`

- [x] F3. **Real Manual QA** — `unspecified-high` (+ `playwright` skill for UI)
  Start fra clean state. Utfør HVERT QA-scenario fra HVER oppgave — følg eksakte steg, ta evidence. Test cross-task integrasjon (features som jobber sammen). Test edge cases: tom state, ugyldig input, raske handlinger. Lagre til `.sisyphus/evidence/final-qa/`.
  Output: `Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT`

- [x] F4. **Scope Fidelity Check** — `deep`
  For hver oppgave: les "What to do", les faktisk diff (git log/diff). Verifiser 1:1 — alt i spec ble bygget (ingenting mangler), ingenting utover spec ble bygget (ingen scope creep). Sjekk "Must NOT do" compliance. Flagg uventede endringer.
  Output: `Tasks [N/N compliant] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

| Etter | Commit message | Filer | Pre-commit |
|-------|---------------|-------|------------|
| Task 1-4 | `feat(api): add MatchScheduleProvider with hot-reload and atomic file writes` | MatchEntry, MatchScheduleProvider, MatchFileWriter, TeamCodeMapper | `dotnet build` |
| Task 5 | `chore(test): set up xUnit test project for backend` | tests/ | `dotnet test` |
| Task 6-8 | `feat(api): add fixture update polling and GET /api/matches endpoint` | Wc2026ApiClient, ResultFetcherService, MatchesController, Program.cs | `dotnet build` |
| Task 9 | `feat(api): add admin match override endpoint` | MatchesController | `dotnet build` |
| Task 10-11 | `feat(ui): migrate to dynamic match data from API with admin override UI` | client.ts, MatchesContext, App.tsx, AdminPanel.tsx | `npm test` |
| Task 12-13 | `test: add backend and frontend tests for fixture updates` | tests/, src/__tests__/ | `dotnet test && npm test` |

---

## Success Criteria

### Verification Commands
```bash
# API returnerer alle 104 kamper
curl http://localhost:5211/api/matches | jq 'length'  # Expected: 104

# Kamper med lag viser lagkoder
curl http://localhost:5211/api/matches | jq '[.[] | select(.homeTeam != null and .stage != "group")] | length'  # Expected: > 0 (etter API har data)

# Admin kan overstyre
curl -X PUT http://localhost:5211/api/admin/matches/73 -H "Authorization: Bearer $ADMIN_JWT" -H "Content-Type: application/json" -d '{"homeTeam":"BRA","awayTeam":"ARG"}' # Expected: 200

# Tester passerer
dotnet test  # Expected: all green
npm test     # Expected: all green
```

### Final Checklist
- [ ] Alle "Must Have" er implementert
- [ ] Alle "Must NOT Have" er fraværende
- [ ] Alle tester passerer
- [ ] Frontend henter kampdata fra API (ikke statisk import)
- [ ] Admin kan overstyre lag
- [ ] matches.json oppdateres atomisk
- [ ] MatchSchedule reloades uten restart
