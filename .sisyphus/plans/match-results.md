# Automatisk henting av kampresultater og poengberegning

## TL;DR

> **Quick Summary**: Integrer WC2026 API for å automatisk hente sluttresultater etter kamper, beregne poeng basert på brukernes tippinger, og vise resultater + poeng integrert i eksisterende kampkort.
> 
> **Deliverables**:
> - `MatchResult` EF-entitet + database-migrasjon
> - `ScoringService` for poengberegning (2p utfall + 1p hjemmemål + 1p bortemål)
> - `Wc2026ApiClient` HTTP-klient for API-integrasjon
> - `ResultFetcherService` bakgrunnstjeneste (poll hvert 10. minutt)
> - `ResultsController` API-endepunkt for resultater + poeng
> - Frontend: `ResultsContext` + oppdatert `MatchCard` med resultat/poeng-visning
> 
> **Estimated Effort**: Medium
> **Parallel Execution**: YES - 4 waves
> **Critical Path**: Task 1 → Task 2 → Task 5 → Task 7 → Task 8 → Task 9

---

## Context

### Original Request
Brukeren ønsker å automatisk hente kampresultater fra VM 2026 etter kampslutt, og vise disse i appen sammen med poengberegning for brukernes tippinger.

### Interview Summary
**Key Discussions**:
- **Datakilde**: WC2026 API (wc2026api.com) — gratis, 100 req/dag, JSON
- **Kun sluttresultat**: Ingen live-score — henter bare etter kampslutt
- **Helautomatisk**: Backend-polling uten manuell intervensjon
- **Poengmodell**: 2p riktig utfall + 1p riktig hjemmemål + 1p riktig bortemål = maks 4p/kamp. Poeng for riktige mål selv med feil utfall.
- **Kun 90-min resultat**: Ekstraomganger og straffespark påvirker ikke tipping
- **Visning**: Integrert i eksisterende kampkort — resultat + tipp + poeng

### Research Findings
- WC2026 API: 100 req/dag, sub-100ms, alle 104 kamper, filter per status
- OpenFootball (GitHub): Tilgjengelig som fremtidig fallback
- Match-modellen har i dag INGEN score-felter
- `MatchSchedule` er en readonly singleton som laster fra JSON
- `Prediction` har allerede `HomeScore`/`AwayScore`

### Metis Review
**Identified Gaps** (addressed):
- **Rate limit**: 5 min polling = 288 req/dag > 100 grense → Justert til 10 min + smart polling (kun på kampdager)
- **Match ID-mapping**: API-et bruker egne ID-er → Mapper via `(homeTeam, awayTeam, date)` tuple
- **BackgroundService + DbContext**: Singleton vs scoped → Bruk `IServiceScopeFactory`
- **0-0 vs ingen resultat**: Bruk `int?` (nullable) for å skille
- **IHttpClientFactory**: Påkrevd for connection pooling i .NET

---

## Work Objectives

### Core Objective
Automatisk hente kampresultater fra WC2026 API og beregne tipping-poeng for alle brukere.

### Concrete Deliverables
- `api/WorldCup.Api/Models/MatchResult.cs` — EF-entitet
- `api/WorldCup.Api/Services/ScoringService.cs` — poengberegning
- `api/WorldCup.Api/Services/Wc2026ApiClient.cs` — HTTP-klient
- `api/WorldCup.Api/Services/ResultFetcherService.cs` — bakgrunnstjeneste
- `api/WorldCup.Api/Controllers/ResultsController.cs` — API-endepunkt
- `api/WorldCup.Api/DTOs/ResultResponse.cs` — respons-DTO
- `src/api/client.ts` — nye funksjoner for resultater
- `src/context/ResultsContext.tsx` — React context
- `src/components/MatchCard.tsx` — oppdatert med resultat + poeng

### Definition of Done
- [ ] `dotnet build` kompilerer uten feil
- [ ] `dotnet ef migrations list` viser ny migrasjon for MatchResult
- [ ] `curl /api/results` returnerer JSON med resultater
- [ ] `npm test` passerer uten feil
- [ ] MatchCard viser resultat + tipp + poeng for ferdige kamper
- [ ] Bakgrunnstjeneste logger polling-aktivitet

### Must Have
- Poengmodell: 2p utfall + 1p hjemmemål + 1p bortemål (maks 4p)
- Kun 90-minutters resultat (ikke ekstraomganger)
- Helautomatisk polling — ingen manuell trigger
- Graceful håndtering av API-feil (429, timeout, nedetid)
- API-nøkkel lagret via konfigurasjon, aldri i kildekode

### Must NOT Have (Guardrails)
- ❌ Live-scores eller in-progress kampsporing
- ❌ Push-notifikasjoner
- ❌ Leaderboard / rangerings-side (kun poeng per kamp)
- ❌ Admin-panel for manuell resultat-inntasting
- ❌ Egen resultatside — kun integrert i kampkort
- ❌ Modifisering av `MatchSchedule.cs` (readonly singleton)
- ❌ Endring av statisk `matches.json`-struktur
- ❌ API-nøkkel hardkodet i kildekode
- ❌ Fallback til alternative API-er (kun WC2026 API, fallback er DEFER)
- ❌ Automatisk oppdatering av knockout-lag fra API (separat feature)
- ❌ Over-abstraherte interfaces — hold det enkelt og direkte

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: YES — Vitest + jsdom (frontend), .NET build (backend)
- **Automated tests**: Tests-after — unit tests for scoring logic, integration tests for API endpoints
- **Framework**: Vitest (frontend), dotnet test kan legges til om behov

### QA Policy
Every task MUST include agent-executed QA scenarios.
Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **Backend API**: Use Bash (curl) — Send requests, assert status + response fields
- **Frontend/UI**: Use Playwright (playwright skill) — Navigate, interact, assert DOM, screenshot
- **Scoring Logic**: Use Bash (dotnet run / test) — Verify calculations

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation — start immediately):
├── Task 1: MatchResult EF entity + migration + config [quick]
├── Task 2: ScoringService — poengberegning [quick]
├── Task 3: Wc2026ApiClient — HTTP-klient [quick]
└── Task 4: Frontend ResultResponse-type + API client functions [quick]

Wave 2 (Core services — after Wave 1):
├── Task 5: ResultFetcherService — bakgrunnstjeneste (depends: 1, 3) [deep]
├── Task 6: ResultsController — API-endepunkt (depends: 1, 2) [quick]
└── Task 7: ResultsContext — React context (depends: 4) [quick]

Wave 3 (Integration — after Wave 2):
├── Task 8: MatchCard oppdatering med resultat + poeng (depends: 7) [visual-engineering]
└── Task 9: Ende-til-ende integrasjonstest (depends: 5, 6, 8) [unspecified-high]

Wave FINAL (Review — after ALL tasks):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Real manual QA (unspecified-high)
└── Task F4: Scope fidelity check (deep)
→ Present results → Get explicit user okay

Critical Path: Task 1 → Task 5 → Task 6 → Task 9 → F1-F4 → user okay
Parallel Speedup: ~60% faster than sequential
Max Concurrent: 4 (Wave 1)
```

### Dependency Matrix

| Task | Depends On | Blocks | Wave |
|------|-----------|--------|------|
| 1    | — | 5, 6 | 1 |
| 2    | — | 6 | 1 |
| 3    | — | 5 | 1 |
| 4    | — | 7 | 1 |
| 5    | 1, 3 | 9 | 2 |
| 6    | 1, 2 | 9 | 2 |
| 7    | 4 | 8 | 2 |
| 8    | 7 | 9 | 3 |
| 9    | 5, 6, 8 | F1-F4 | 3 |

### Agent Dispatch Summary

- **Wave 1**: **4 tasks** — T1 → `quick`, T2 → `quick`, T3 → `quick`, T4 → `quick`
- **Wave 2**: **3 tasks** — T5 → `deep`, T6 → `quick`, T7 → `quick`
- **Wave 3**: **2 tasks** — T8 → `visual-engineering`, T9 → `unspecified-high`
- **FINAL**: **4 tasks** — F1 → `oracle`, F2 → `unspecified-high`, F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [x] 1. MatchResult EF-entitet + database-migrasjon + konfigurasjon

  **What to do**:
  - Opprett `api/WorldCup.Api/Models/MatchResult.cs` med: `Id` (Guid), `MatchId` (int, unique), `HomeScore` (int), `AwayScore` (int), `FetchedAt` (DateTime)
  - Legg til `DbSet<MatchResult>` i `AppDbContext.cs` — følg eksakt samme mønster som linje 8-10
  - Konfigurer i `OnModelCreating`: unique index på `MatchId`, følg mønster fra linje 20-22
  - Kjør `dotnet ef migrations add AddMatchResult` for å generere migrasjon
  - Legg til `"Wc2026Api": { "ApiKey": "", "BaseUrl": "https://api.wc2026api.com" }` i `appsettings.json`
  - IKKE legg inn en ekte API-nøkkel — kun placeholder

  **Must NOT do**:
  - Endre eksisterende modeller (User, Prediction, Invitation)
  - Legge API-nøkkel i kildekode
  - Modifisere MatchSchedule.cs

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Enkel EF-entitet som følger etablert mønster, minimal logikk
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2, 3, 4)
  - **Blocks**: Tasks 5, 6
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `api/WorldCup.Api/Models/Prediction.cs:3-13` — EF entity pattern: simple POCO med auto-properties, navigation property. Kopier dette mønsteret for MatchResult.
  - `api/WorldCup.Api/Data/AppDbContext.cs:8-10` — DbSet registrering: `public DbSet<X> Xs => Set<X>()` mønsteret.
  - `api/WorldCup.Api/Data/AppDbContext.cs:20-22` — Unique index konfigurasjon: `.HasIndex(x => x.Field).IsUnique()` mønsteret.

  **API/Type References**:
  - `api/WorldCup.Api/Models/Prediction.cs:5-6` — `Guid Id` + `int MatchId` typer — bruk samme for MatchResult

  **External References**:
  - EF Core migration docs: `dotnet ef migrations add <Name>` kommandoen

  **Acceptance Criteria**:

  - [ ] `api/WorldCup.Api/Models/MatchResult.cs` eksisterer med riktige properties
  - [ ] `dotnet build` kompilerer uten feil
  - [ ] `dotnet ef migrations list` viser `AddMatchResult` migrasjon
  - [ ] `grep -r "Wc2026Api" api/WorldCup.Api/appsettings.json` returnerer config-seksjon
  - [ ] `grep -r "wc2026_" api/` returnerer NULL (ingen ekte nøkkel i kildekode)

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: MatchResult tabell opprettes etter migrasjon
    Tool: Bash
    Preconditions: API-prosjektet kompilerer
    Steps:
      1. cd api/WorldCup.Api && dotnet build
      2. dotnet ef database update
      3. sqlite3 worldcup.db ".tables" — sjekk at MatchResults finnes
      4. sqlite3 worldcup.db ".schema MatchResults" — verifiser kolonner: Id, MatchId, HomeScore, AwayScore, FetchedAt
    Expected Result: Tabellen MatchResults eksisterer med alle kolonner, MatchId har unique index
    Failure Indicators: Tabellen mangler, feil kolonnetyper, ingen unique constraint
    Evidence: .sisyphus/evidence/task-1-matchresult-migration.txt

  Scenario: Manglende API-nøkkel i kildekode
    Tool: Bash
    Preconditions: Konfigurasjon lagt til
    Steps:
      1. grep -r "wc2026_" api/ — søk etter faktisk API-nøkkel
      2. grep -r "Wc2026Api" api/WorldCup.Api/appsettings.json — verifiser config finnes
    Expected Result: Ingen hardkodet nøkkel, men config-seksjon med tom ApiKey
    Failure Indicators: Reell API-nøkkel funnet i kildekode
    Evidence: .sisyphus/evidence/task-1-no-api-key-in-source.txt
  ```

  **Commit**: YES (groups with 2, 3)
  - Message: `feat(api): add MatchResult entity, scoring service, and WC2026 API client`
  - Files: `Models/MatchResult.cs`, `Data/AppDbContext.cs`, `Migrations/*`, `appsettings.json`
  - Pre-commit: `dotnet build`

- [x] 2. ScoringService — poengberegning

  **What to do**:
  - Opprett `api/WorldCup.Api/Services/ScoringService.cs`
  - Implementer `CalculatePoints(int predictedHome, int predictedAway, int actualHome, int actualAway) → int`
  - Poenglogikk:
    - 2 poeng for riktig utfall (hjemmeseier/uavgjort/borteseier)
    - 1 poeng for riktig antall hjemmemål
    - 1 poeng for riktig antall bortemål
    - Maks 4 poeng per kamp
    - Poeng for riktige mål SELV MED feil utfall
  - Utfall-beregning: `predictedHome > predictedAway` = hjemmeseier, `==` = uavgjort, `<` = borteseier. Sammenlign med actual.
  - Registrer som scoped service i `Program.cs`

  **Must NOT do**:
  - Komplisere med bonus-poeng eller vekting som ikke er spesifisert
  - Legge scoring-logikk i controller eller background worker
  - Over-abstrahere med interfaces — én konkret klasse er nok

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Ren matematisk logikk, én fil, klare regler
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3, 4)
  - **Blocks**: Task 6
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `api/WorldCup.Api/Services/MatchSchedule.cs:6-32` — Service-klasse mønster: sealed class, ingen DI-avhengigheter for ren logikk

  **API/Type References**:
  - `api/WorldCup.Api/Models/Prediction.cs:8-9` — `HomeScore` og `AwayScore` int-typer som input

  **Acceptance Criteria**:

  - [ ] `api/WorldCup.Api/Services/ScoringService.cs` eksisterer
  - [ ] `dotnet build` kompilerer uten feil
  - [ ] Scoring-metode gir korrekte resultater for ALLE testcases:
    - Tipp `3-1` vs Resultat `3-1` → **4 poeng** (utfall ✓, hjemme ✓, borte ✓)
    - Tipp `2-0` vs Resultat `3-1` → **2 poeng** (utfall ✓ hjemmeseier, hjemme ✗, borte ✗)
    - Tipp `1-1` vs Resultat `0-0` → **2 poeng** (utfall ✓ uavgjort, hjemme ✗, borte ✗)
    - Tipp `2-1` vs Resultat `0-3` → **0 poeng** (utfall ✗, hjemme ✗, borte ✗)
    - Tipp `2-0` vs Resultat `3-0` → **3 poeng** (utfall ✓, hjemme ✗, borte ✓)
    - Tipp `1-3` vs Resultat `2-3` → **1 poeng** (utfall ✗, hjemme ✗, borte ✓)
    - Tipp `0-0` vs Resultat `0-0` → **4 poeng** (utfall ✓, hjemme ✓, borte ✓)
    - Tipp `0-1` vs Resultat `2-1` → **1 poeng** (utfall ✗, hjemme ✗, borte ✓)

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Poengberegning returnerer korrekte verdier
    Tool: Bash (dotnet script eller inline test)
    Preconditions: ScoringService eksisterer og kompilerer
    Steps:
      1. Skriv en enkel konsoll-test eller unit test som kaller CalculatePoints med alle 8 testcases over
      2. Kjør testen
      3. Verifiser at hvert kall returnerer forventet poengverdi
    Expected Result: Alle 8 testcases returnerer korrekt poengverdi (4, 2, 2, 0, 3, 1, 4, 1)
    Failure Indicators: Feil poengverdi for noen testcase
    Evidence: .sisyphus/evidence/task-2-scoring-tests.txt

  Scenario: Edge case — høye scoringer
    Tool: Bash
    Preconditions: ScoringService eksisterer
    Steps:
      1. Test med Tipp `5-5` vs Resultat `5-5` → 4 poeng
      2. Test med Tipp `0-0` vs Resultat `7-1` → 0 poeng
    Expected Result: Korrekte poeng for ekstreme verdier
    Failure Indicators: Crash eller feil beregning
    Evidence: .sisyphus/evidence/task-2-scoring-edge-cases.txt
  ```

  **Commit**: YES (groups with 1, 3)
  - Message: `feat(api): add MatchResult entity, scoring service, and WC2026 API client`
  - Files: `Services/ScoringService.cs`, `Program.cs`
  - Pre-commit: `dotnet build`

- [x] 3. Wc2026ApiClient — HTTP-klient for WC2026 API

  **What to do**:
  - Opprett `api/WorldCup.Api/Services/Wc2026ApiClient.cs`
  - Bruk `IHttpClientFactory` — registrer via `builder.Services.AddHttpClient<Wc2026ApiClient>()` i `Program.cs`
  - Les API-nøkkel og base URL fra `IConfiguration` (seksjon `Wc2026Api:ApiKey` og `Wc2026Api:BaseUrl`)
  - Implementer `GetCompletedMatchesAsync() → List<Wc2026MatchDto>` som kaller `GET /matches?status=completed`
  - Sett `Authorization: Bearer {apiKey}` header
  - Deserialisér responsen til en DTO med: `matchNumber`, `home` (teamkode), `away` (teamkode), `kickoffAt`, `score.ft` (array `[homeGoals, awayGoals]`)
  - Håndter feil gracefully: HTTP 429 → logg warning, returner tom liste. Timeout → logg, returner tom liste. Andre feil → logg error, returner tom liste.
  - **Match-mapping**: Inkluder en metode `MapToLocalMatchId(string homeTeam, string awayTeam, DateTime date, MatchSchedule schedule) → int?` som mapper API-resultater til lokale match-ID-er basert på lagkoder + dato

  **Must NOT do**:
  - Bruke rå `HttpClient` (new HttpClient()) — bruk `IHttpClientFactory`
  - Kaste exceptions ved API-feil — returner tom liste
  - Lagre API-nøkkel hardkodet
  - Implementere caching eller retry-logikk utover enkel feilhåndtering

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Standard HTTP-klient med konfigurasjon, klart mønster
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2, 4)
  - **Blocks**: Task 5
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `api/WorldCup.Api/Program.cs:13-14` — Service-registrering mønster i minimal hosting
  - `api/WorldCup.Api/Program.cs:36-41` — IConfiguration lesing med null-sjekk mønster

  **API/Type References**:
  - `api/WorldCup.Api/Services/MatchSchedule.cs:34-44` — `MatchEntry` med `Id`, `Date`, `Stage` — brukes for mapping
  - `api/WorldCup.Api/Services/MatchSchedule.cs:19-20` — `GetMatch(int matchId)` for oppslag

  **External References**:
  - WC2026 API docs: `https://wc2026api.com/` — endepunkter, autentisering, responsformat
  - .NET IHttpClientFactory: Typed clients pattern

  **Acceptance Criteria**:

  - [ ] `api/WorldCup.Api/Services/Wc2026ApiClient.cs` eksisterer med `GetCompletedMatchesAsync()`
  - [ ] `dotnet build` kompilerer uten feil
  - [ ] `Wc2026ApiClient` registrert via `AddHttpClient<T>()` i Program.cs
  - [ ] API-nøkkel leses fra konfigurasjon, ikke hardkodet

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Klient kompilerer og er registrert
    Tool: Bash
    Preconditions: Klient opprettet
    Steps:
      1. cd api/WorldCup.Api && dotnet build
      2. grep "AddHttpClient" Program.cs — verifiser registrering
      3. grep "Wc2026Api:ApiKey" Services/Wc2026ApiClient.cs — verifiser config-lesing
    Expected Result: Build OK, AddHttpClient registrering finnes, config-lesing finnes
    Failure Indicators: Build feil, manglende registrering
    Evidence: .sisyphus/evidence/task-3-api-client-build.txt

  Scenario: Feilhåndtering — API returnerer 429
    Tool: Bash
    Preconditions: Klient implementert
    Steps:
      1. Verifiser at koden har try/catch rundt HTTP-kallet
      2. Verifiser at 429-statusen logges som warning (ikke error/exception)
      3. Verifiser at metoden returnerer tom liste (ikke kaster)
    Expected Result: Graceful degradation — tom liste, ingen crash
    Failure Indicators: Exception kastes, app krasjer
    Evidence: .sisyphus/evidence/task-3-error-handling.txt
  ```

  **Commit**: YES (groups with 1, 2)
  - Message: `feat(api): add MatchResult entity, scoring service, and WC2026 API client`
  - Files: `Services/Wc2026ApiClient.cs`, `Program.cs`
  - Pre-commit: `dotnet build`

- [x] 4. Frontend ResultResponse-type + API-klient-funksjoner

  **What to do**:
  - Legg til `ResultResponse` interface i `src/api/client.ts`: `{ matchId: number, homeScore: number, awayScore: number, fetchedAt: string }`
  - Legg til `PointsResponse` interface: `{ matchId: number, points: number, outcomePoints: number, homeGoalPoints: number, awayGoalPoints: number }`
  - Legg til `getResults(): Promise<ResultResponse[]>` — kaller `GET /api/results`
  - Legg til `getUserPoints(): Promise<PointsResponse[]>` — kaller `GET /api/results/points`
  - Følg eksakt samme `request<T>()` wrapper-mønster som eksisterende funksjoner

  **Must NOT do**:
  - Endre eksisterende interfaces eller funksjoner
  - Legge til polling/timer-logikk i frontend — hent data ved page load
  - Introdusere nye avhengigheter

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Enkel tillegg til eksisterende API-klient fil, følger etablert mønster
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2, 3)
  - **Blocks**: Task 7
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `src/api/client.ts:47-52` — `PredictionResponse` interface mønster — kopier for ResultResponse
  - `src/api/client.ts:67-69` — `getPredictions()` funksjon mønster — kopier for getResults
  - `src/api/client.ts:11-37` — `request<T>()` wrapper som alle funksjoner bruker

  **Acceptance Criteria**:

  - [ ] `ResultResponse` og `PointsResponse` interfaces lagt til i `src/api/client.ts`
  - [ ] `getResults()` og `getUserPoints()` funksjoner implementert
  - [ ] `npm run build` kompilerer uten TypeScript-feil

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Frontend kompilerer med nye typer
    Tool: Bash
    Preconditions: Typer og funksjoner lagt til
    Steps:
      1. npm run build
      2. Verifiser at build fullføres uten TypeScript-feil
    Expected Result: Build succeeded, ingen type-feil
    Failure Indicators: TypeScript compilation errors
    Evidence: .sisyphus/evidence/task-4-frontend-build.txt

  Scenario: Funksjoner følger eksisterende mønster
    Tool: Bash
    Preconditions: Funksjoner lagt til
    Steps:
      1. grep "getResults" src/api/client.ts — verifiser funksjon eksisterer
      2. grep "getUserPoints" src/api/client.ts — verifiser funksjon eksisterer
      3. Verifiser at begge bruker request<T>() wrapper
    Expected Result: Begge funksjoner finnes og bruker riktig mønster
    Failure Indicators: Funksjoner mangler eller bruker fetch direkte
    Evidence: .sisyphus/evidence/task-4-api-functions.txt
  ```

  **Commit**: YES (groups with 7, 8)
  - Message: `feat(ui): add results context and display scores with points on match cards`
  - Files: `src/api/client.ts`
  - Pre-commit: `npm run build`

- [x] 5. ResultFetcherService — bakgrunnstjeneste for automatisk polling

  **What to do**:
  - Opprett `api/WorldCup.Api/Services/ResultFetcherService.cs` som `BackgroundService`
  - Bruk `IServiceScopeFactory` for å opprette scoped `AppDbContext` (singleton service kan IKKE injisere scoped DbContext direkte)
  - Injiser `Wc2026ApiClient`, `ScoringService`, `MatchSchedule`, `ILogger<ResultFetcherService>`
  - Poll-logikk:
    1. Kjør loop med `Task.Delay(TimeSpan.FromMinutes(10))` mellom hver sjekk
    2. **Smart polling**: Sjekk kun om det finnes kamper der `match.Date + 2.5 timer < DateTime.UtcNow` OG match-ID ikke allerede finnes i MatchResults-tabellen
    3. Hvis ingen kamper forventes ferdige — skip API-kallet helt (spar kvote)
    4. Kall `Wc2026ApiClient.GetCompletedMatchesAsync()`
    5. For hvert resultat: sjekk om MatchId allerede er lagret (upsert-mønster — ikke blind insert)
    6. Lagre nye resultater som `MatchResult` i databasen
    7. **Etter lagring**: for hvert nytt resultat, hent alle `Prediction` for den kampen og beregn poeng med `ScoringService`
    8. Lagre poeng (enten som felt på Prediction eller i egen tabell — vurder hva som er enklest)
  - Registrer i `Program.cs` med `builder.Services.AddHostedService<ResultFetcherService>()`
  - Logg tydelig: "Checking for completed matches", "Found N new results", "Calculated points for match X"
  - Turneringsslutt: Ikke poll etter 2026-07-20 (dagen etter finalen)

  **Must NOT do**:
  - Injisere `AppDbContext` direkte i constructor — bruk `IServiceScopeFactory`
  - Polle oftere enn hvert 10. minutt (rate limit: 100 req/dag)
  - Kaste exceptions som krasjer bakgrunnstjenesten — all feilhåndtering med try/catch + logging
  - Modifisere `MatchSchedule` singleton

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: Kompleks bakgrunnstjeneste med threading, scoping, smart polling-logikk, og poengberegning
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 6, 7 i Wave 2)
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 9
  - **Blocked By**: Task 1 (MatchResult entity), Task 3 (Wc2026ApiClient)

  **References**:

  **Pattern References**:
  - `api/WorldCup.Api/Program.cs:13-14` — Service-registrering: `builder.Services.AddSingleton(...)` — bruk `AddHostedService<T>()` for BackgroundService
  - `api/WorldCup.Api/Program.cs:63-67` — Scope-opprettelse mønster med `CreateScope()` — same pattern trengs i BackgroundService

  **API/Type References**:
  - `api/WorldCup.Api/Models/MatchResult.cs` — (ny fra Task 1) Entiteten som skal lagres
  - `api/WorldCup.Api/Services/Wc2026ApiClient.cs` — (ny fra Task 3) HTTP-klient for API-kall
  - `api/WorldCup.Api/Services/ScoringService.cs` — (ny fra Task 2) Poengberegning
  - `api/WorldCup.Api/Services/MatchSchedule.cs:19-20` — `GetMatch(matchId)` for å sjekke kampdata
  - `api/WorldCup.Api/Models/Prediction.cs:3-13` — Prediction-modellen som poeng beregnes for

  **External References**:
  - .NET BackgroundService docs: `ExecuteAsync(CancellationToken)` pattern
  - .NET IServiceScopeFactory: Korrekt scope-håndtering for hosted services

  **Acceptance Criteria**:

  - [ ] `api/WorldCup.Api/Services/ResultFetcherService.cs` eksisterer som `BackgroundService`
  - [ ] `dotnet build` kompilerer uten feil
  - [ ] `IServiceScopeFactory` brukes for DbContext (grep bekrefter)
  - [ ] `AddHostedService<ResultFetcherService>()` finnes i Program.cs
  - [ ] Logging-statements for polling-aktivitet finnes i koden

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: BackgroundService starter og logger
    Tool: Bash
    Preconditions: Service registrert, API bygger
    Steps:
      1. cd api/WorldCup.Api && dotnet build
      2. grep "AddHostedService" Program.cs — verifiser registrering
      3. grep "IServiceScopeFactory" Services/ResultFetcherService.cs — verifiser scope-bruk
      4. grep "ILogger" Services/ResultFetcherService.cs — verifiser logging
    Expected Result: Service er registrert, bruker scope factory og logger
    Failure Indicators: Manglende registrering, direkte DbContext-injeksjon
    Evidence: .sisyphus/evidence/task-5-background-service.txt

  Scenario: Smart polling — skipper API-kall når ingen kamper er ferdige
    Tool: Bash
    Preconditions: Service implementert
    Steps:
      1. Verifiser at koden sjekker match-datoer mot DateTime.UtcNow før API-kall
      2. Verifiser at koden har en guard clause som skipper kallet når ingen kamper er ferdige
    Expected Result: Koden inneholder logikk for å sjekke kamptidspunkt + 2.5 timer
    Failure Indicators: Blind polling uten dato-sjekk
    Evidence: .sisyphus/evidence/task-5-smart-polling.txt
  ```

  **Commit**: YES (groups with 6)
  - Message: `feat(api): add result fetcher background service and results controller`
  - Files: `Services/ResultFetcherService.cs`, `Program.cs`
  - Pre-commit: `dotnet build`

- [x] 6. ResultsController — API-endepunkt for resultater og poeng

  **What to do**:
  - Opprett `api/WorldCup.Api/Controllers/ResultsController.cs`
  - Følg eksakt samme mønster som `PredictionsController.cs`: `[ApiController]`, `[Authorize]`, `[Route("api/results")]`, primary constructor DI
  - Opprett `api/WorldCup.Api/DTOs/ResultResponse.cs` med: `MatchId`, `HomeScore`, `AwayScore`, `FetchedAt`
  - Opprett `api/WorldCup.Api/DTOs/PointsResponse.cs` med: `MatchId`, `Points`, `OutcomePoints`, `HomeGoalPoints`, `AwayGoalPoints`
  - Endepunkter:
    1. `GET /api/results` — Returnerer alle lagrede resultater som `ResultResponse[]`. Ingen auth required (AllowAnonymous) slik at alle ser resultater.
    2. `GET /api/results/points` — Returnerer autentisert brukers poeng for alle kamper med resultat. Krever auth. Beregner poeng on-the-fly med `ScoringService` (join MatchResult + Prediction for brukeren).

  **Must NOT do**:
  - Legge scoring-logikk direkte i controlleren — bruk ScoringService
  - Returnere alle brukeres poeng — kun innlogget brukers egne
  - Legge til PUT/POST/DELETE endepunkter — resultater er readonly (skrives av BackgroundService)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Standard CRUD-controller som følger etablert mønster, ingen kompleks logikk
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 5, 7 i Wave 2)
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 9
  - **Blocked By**: Task 1 (MatchResult entity), Task 2 (ScoringService)

  **References**:

  **Pattern References**:
  - `api/WorldCup.Api/Controllers/PredictionsController.cs:12-15` — Controller-deklarasjon: `[ApiController]`, `[Authorize]`, `[Route("api/...")]`, primary constructor DI
  - `api/WorldCup.Api/Controllers/PredictionsController.cs:17-39` — GET-endepunkt mønster: async, LINQ query, Select til DTO, Ok() return
  - `api/WorldCup.Api/Controllers/PredictionsController.cs:139-144` — `GetAuthenticatedUserId()` helper — kopier denne

  **API/Type References**:
  - `api/WorldCup.Api/DTOs/PredictionResponse.cs` — DTO-mønster å følge for ResultResponse
  - `api/WorldCup.Api/Models/MatchResult.cs` — (ny fra Task 1) Data å returnere
  - `api/WorldCup.Api/Services/ScoringService.cs` — (ny fra Task 2) Brukes for poeng-beregning

  **Acceptance Criteria**:

  - [ ] `api/WorldCup.Api/Controllers/ResultsController.cs` eksisterer
  - [ ] `api/WorldCup.Api/DTOs/ResultResponse.cs` og `PointsResponse.cs` eksisterer
  - [ ] `dotnet build` kompilerer uten feil
  - [ ] GET /api/results returnerer 200 med JSON array
  - [ ] GET /api/results/points med auth returnerer brukerens poeng

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: GET /api/results returnerer tom liste (ingen resultater ennå)
    Tool: Bash (curl)
    Preconditions: API kjører, ingen resultater lagret
    Steps:
      1. Start API: cd api/WorldCup.Api && dotnet run &
      2. curl -s -w "\n%{http_code}" http://localhost:5211/api/results
      3. Verifiser HTTP 200 og tom JSON array "[]"
    Expected Result: HTTP 200, body er "[]"
    Failure Indicators: 404, 500, eller feil format
    Evidence: .sisyphus/evidence/task-6-results-empty.txt

  Scenario: GET /api/results/points krever autentisering
    Tool: Bash (curl)
    Preconditions: API kjører
    Steps:
      1. curl -s -w "\n%{http_code}" http://localhost:5211/api/results/points (uten token)
      2. Verifiser HTTP 401 Unauthorized
    Expected Result: 401 Unauthorized
    Failure Indicators: 200 uten auth, 500 error
    Evidence: .sisyphus/evidence/task-6-points-auth-required.txt
  ```

  **Commit**: YES (groups with 5)
  - Message: `feat(api): add result fetcher background service and results controller`
  - Files: `Controllers/ResultsController.cs`, `DTOs/ResultResponse.cs`, `DTOs/PointsResponse.cs`
  - Pre-commit: `dotnet build`

- [x] 7. ResultsContext — React context for resultater og poeng

  **What to do**:
  - Opprett `src/context/ResultsContext.tsx`
  - Følg EKSAKT samme mønster som `PredictionsContext.tsx`:
    1. `ResultsContextValue` interface med: `results: Map<number, ResultResponse>`, `points: Map<number, PointsResponse>`, `isLoading: boolean`
    2. `createContext<ResultsContextValue | null>(null)`
    3. `ResultsProvider` komponent som henter data ved mount (og når `user` endres)
    4. `useResults()` hook med error-throw om brukt utenfor provider
  - Hent data fra `getResults()` og `getUserPoints()` (fra Task 4)
  - Wrap i `App.tsx` — legg `ResultsProvider` innenfor `PredictionsProvider`
  - Resultater hentes for alle (også uinnloggede kan se resultater). Poeng hentes kun når bruker er innlogget.

  **Must NOT do**:
  - Endre `PredictionsContext` — legg til separat context
  - Legge til polling/auto-refresh — hent én gang ved mount
  - Introdusere state management bibliotek (Zustand, Redux etc.)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Direkte kopi av PredictionsContext-mønster, minimal tilpasning
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 5, 6 i Wave 2)
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 8
  - **Blocked By**: Task 4 (frontend API-funksjoner)

  **References**:

  **Pattern References**:
  - `src/context/PredictionsContext.tsx:1-65` — HELE filen er mønsteret. Kopier strukturen: imports, interface, createContext, Provider med useEffect+fetch, export hook.
  - `src/context/PredictionsContext.tsx:8-12` — `PredictionsContextValue` interface — tilpass for results + points
  - `src/context/PredictionsContext.tsx:21-40` — useEffect med auth-avhengighet og Map-oppbygging — bruk samme for results
  - `src/context/PredictionsContext.tsx:59-64` — Hook med error-throw mønster

  **API/Type References**:
  - `src/api/client.ts` — `ResultResponse` og `PointsResponse` (ny fra Task 4)
  - `src/api/client.ts` — `getResults()` og `getUserPoints()` (ny fra Task 4)
  - `src/context/AuthContext.tsx` — `useAuth()` for å sjekke innlogget status

  **Acceptance Criteria**:

  - [ ] `src/context/ResultsContext.tsx` eksisterer med Provider + hook
  - [ ] `ResultsProvider` wrappet i `App.tsx`
  - [ ] `npm run build` kompilerer uten feil
  - [ ] `npm test` passerer

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Frontend kompilerer med ResultsContext
    Tool: Bash
    Preconditions: Context opprettet og wrappet i App.tsx
    Steps:
      1. npm run build
      2. Verifiser ingen TypeScript-feil
      3. grep "ResultsProvider" src/App.tsx — verifiser wrapping
    Expected Result: Build OK, ResultsProvider finnes i App.tsx
    Failure Indicators: Build feil, manglende provider i App.tsx
    Evidence: .sisyphus/evidence/task-7-results-context.txt

  Scenario: useResults hook kaster utenfor provider
    Tool: Bash
    Preconditions: Context implementert
    Steps:
      1. grep "throw new Error" src/context/ResultsContext.tsx
      2. Verifiser at error-melding finnes for bruk utenfor provider
    Expected Result: Error-throw for bruk utenfor provider
    Failure Indicators: Ingen guard clause
    Evidence: .sisyphus/evidence/task-7-hook-guard.txt
  ```

  **Commit**: YES (groups with 4, 8)
  - Message: `feat(ui): add results context and display scores with points on match cards`
  - Files: `src/context/ResultsContext.tsx`, `src/App.tsx`
  - Pre-commit: `npm run build`

- [x] 8. MatchCard oppdatering — vis resultat + tipp + poeng

  **What to do**:
  - Oppdater `src/components/MatchCard.tsx` for å vise resultater og poeng
  - Hent resultat og poeng via `useResults()` hook (fra Task 7)
  - **Ny visuell tilstand for ferdige kamper** (match har resultat):
    1. Vis den offisielle scoren tydelig mellom lagnavn (erstatt "–" med f.eks. "2 – 1")
    2. Vis brukerens tipp som en liten badge (som i dag)
    3. Vis opptjente poeng som en ny badge (f.eks. "3p" med fargekoding: 4p=gull, 2-3p=grønn, 1p=oransje, 0p=rød)
    4. Hvis brukeren IKKE har tippet kampen: vis resultat men INGEN poeng-badge (ikke "0p")
  - **Behold eksisterende visning** for kamper uten resultat (uendret)
  - Bruk eksisterende CSS-variabler for konsistent styling

  **Must NOT do**:
  - Fjerne eller endre eksisterende tipp-visning for kamper uten resultat
  - Legge til klikk-handling eller modal for resultater
  - Opprette egen resultatside
  - Introdusere nye CSS-biblioteker

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: Frontend UI-arbeid med visuell tilstandshåndtering, CSS-variabler, og responsive layout
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3
  - **Blocks**: Task 9
  - **Blocked By**: Task 7 (ResultsContext)

  **References**:

  **Pattern References**:
  - `src/components/MatchCard.tsx:76-118` — Eksisterende tilstandshåndtering: locked med tipp, locked uten tipp, unlocked med tipp, unlocked uten tipp. Legg til ny tilstand: "har resultat".
  - `src/components/MatchCard.tsx:66-74` — Lagvisning med flagg og navn mellom disse — resultat-scoren bør vises her
  - `src/components/MatchCard.tsx:56-63` — Tid/stage badge — mønster for badge-styling

  **API/Type References**:
  - `src/context/ResultsContext.tsx` — (ny fra Task 7) `useResults()` hook
  - `src/api/client.ts` — `ResultResponse` og `PointsResponse` interfaces (ny fra Task 4)

  **External References**:
  - Eksisterende CSS-variabler: `--color-success-light`, `--color-success-text`, `--color-badge-bg`, `--color-badge-text`, `--color-text-muted`, `--color-primary`

  **Acceptance Criteria**:

  - [ ] MatchCard viser offisiell score for kamper med resultat
  - [ ] MatchCard viser brukerens tipp ved siden av resultatet
  - [ ] MatchCard viser poeng-badge med fargekoding
  - [ ] MatchCard uten resultat ser UENDRET ut (ingen visuell regresjon)
  - [ ] Kamper uten bruker-tipp viser resultat men ingen poeng
  - [ ] `npm run build` kompilerer uten feil
  - [ ] `npm test` passerer

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: MatchCard viser resultat + tipp + poeng for ferdig kamp
    Tool: Playwright
    Preconditions: Backend kjører med minst ett resultat lagret, bruker innlogget med tipp
    Steps:
      1. Naviger til http://localhost:5173/
      2. Finn [data-testid="match-card"] for en kamp med resultat
      3. Assert: offisiell score er synlig (f.eks. "2 – 1" i bold)
      4. Assert: brukerens tipp-badge er synlig (f.eks. "3–1")
      5. Assert: poeng-badge er synlig (f.eks. "3p")
      6. Ta screenshot
    Expected Result: Alle tre elementer (resultat, tipp, poeng) er synlige på kampkortet
    Failure Indicators: Manglende elementer, feil plassering, overlappende tekst
    Evidence: .sisyphus/evidence/task-8-matchcard-result.png

  Scenario: Kommende kamp ser uendret ut (ingen regresjon)
    Tool: Playwright
    Preconditions: Appen kjører
    Steps:
      1. Naviger til http://localhost:5173/
      2. Finn [data-testid="match-card"] for en kamp UTEN resultat
      3. Assert: "Bet" knapp eller tipp-badge vises som før
      4. Assert: INGEN resultat-score eller poeng-badge er synlig
      5. Ta screenshot
    Expected Result: Kampkort for kommende kamper ser identisk ut som før endringen
    Failure Indicators: Resultat-elementer synlige på kommende kamper
    Evidence: .sisyphus/evidence/task-8-matchcard-no-result.png

  Scenario: Kamp uten brukertipp viser resultat men ingen poeng
    Tool: Playwright
    Preconditions: Bruker innlogget, kamp med resultat men uten tipp fra denne brukeren
    Steps:
      1. Naviger til http://localhost:5173/
      2. Finn kamp med resultat der bruker ikke har tippet
      3. Assert: offisiell score er synlig
      4. Assert: INGEN poeng-badge (ikke "0p")
    Expected Result: Score synlig, ingen poeng-indikator
    Failure Indicators: "0p" badge vises, eller score mangler
    Evidence: .sisyphus/evidence/task-8-matchcard-no-prediction.png
  ```

  **Commit**: YES (groups with 4, 7)
  - Message: `feat(ui): add results context and display scores with points on match cards`
  - Files: `src/components/MatchCard.tsx`
  - Pre-commit: `npm test`

- [x] 9. Ende-til-ende integrasjonstest

  **What to do**:
  - Verifiser at hele flyten fungerer ende-til-ende:
    1. Backend starter uten feil med bakgrunnstjeneste aktiv
    2. ResultsController returnerer korrekte data
    3. Frontend henter og viser resultater
    4. Poengberegning er korrekt
  - Kjør alle byggkommandoer: `dotnet build`, `npm run build`, `npm test`
  - Test med manuelt innsatte testdata i databasen (INSERT MatchResult via sqlite3) for å verifisere frontend-visning uten å være avhengig av ekte API
  - Verifiser at eksisterende funksjonalitet ikke er ødelagt (predictions, auth, invitations)

  **Must NOT do**:
  - Endre produksjonskode — kun testing
  - Legge igjen testdata i databasen

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Kryssende verifikasjon over hele stacken — backend, frontend, database
  - **Skills**: [`playwright`]

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3 (etter Task 8)
  - **Blocks**: F1-F4
  - **Blocked By**: Tasks 5, 6, 8

  **References**:

  **Pattern References**:
  - Alle filer fra Task 1-8

  **Acceptance Criteria**:

  - [ ] `dotnet build` uten feil
  - [ ] `npm run build` uten feil
  - [ ] `npm test` alle tester passerer
  - [ ] `curl /api/results` returnerer 200
  - [ ] `curl /api/results/points` med auth returnerer 200
  - [ ] Frontend viser resultater og poeng korrekt

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Full stack verifikasjon
    Tool: Bash + Playwright
    Preconditions: Backend og frontend kjører
    Steps:
      1. cd api/WorldCup.Api && dotnet build — verifiser OK
      2. npm run build — verifiser OK
      3. npm test — verifiser alle tester passerer
      4. Start backend: dotnet run &
      5. Sett inn testdata: sqlite3 worldcup.db "INSERT INTO MatchResults (Id, MatchId, HomeScore, AwayScore, FetchedAt) VALUES ('test-id', 1, 2, 1, '2026-06-12T00:00:00Z')"
      6. curl -s http://localhost:5211/api/results | jq '.[0].matchId' — verifiser returnerer 1
      7. Start frontend: npm run dev &
      8. Playwright: naviger til http://localhost:5173, finn match-card for kamp 1, assert resultat "2 – 1" vises
      9. Rydd opp testdata
    Expected Result: Hele flyten fungerer — backend serverer, frontend viser
    Failure Indicators: Build feil, API feil, frontend viser ikke resultater
    Evidence: .sisyphus/evidence/task-9-e2e-verification.png

  Scenario: Eksisterende funksjonalitet er uberørt
    Tool: Bash (curl)
    Preconditions: Backend kjører
    Steps:
      1. curl -s http://localhost:5211/api/predictions — verifiser 401 (krever auth, som før)
      2. curl -s http://localhost:5211/api/invitations — verifiser 401 (som før)
    Expected Result: Eksisterende endepunkter oppfører seg identisk
    Failure Indicators: Endrede statuskoder, feil response
    Evidence: .sisyphus/evidence/task-9-no-regression.txt
  ```

  **Commit**: NO (ingen kodeendringer)

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Present consolidated results to user and get explicit "okay" before completing.

- [x] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read file, curl endpoint, run command). For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in .sisyphus/evidence/. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [x] F2. **Code Quality Review** — `unspecified-high`
  Run `dotnet build` + `npm test`. Review all changed files for: `as any`/`@ts-ignore`, empty catches, console.log in prod, commented-out code, unused imports. Check AI slop: excessive comments, over-abstraction, generic names (data/result/item/temp). Verify `IHttpClientFactory` used (not raw HttpClient), `IServiceScopeFactory` used in BackgroundService, API key not in source code.
  Output: `Build [PASS/FAIL] | Tests [N pass/N fail] | Files [N clean/N issues] | VERDICT`

- [x] F3. **Real Manual QA** — `unspecified-high` (+ `playwright` skill)
  Start from clean state. Execute EVERY QA scenario from EVERY task — follow exact steps, capture evidence. Test cross-task integration (results + predictions + points displayed together). Test edge cases: match with no prediction, 0-0 result. Save to `.sisyphus/evidence/final-qa/`.
  Output: `Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT`

- [x] F4. **Scope Fidelity Check** — `deep`
  For each task: read "What to do", read actual diff (git log/diff). Verify 1:1 — everything in spec was built (no missing), nothing beyond spec was built (no creep). Check "Must NOT do" compliance. Flag unaccounted changes.
  Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | VERDICT`

---

## Commit Strategy

| Commit | Scope | Message | Files | Pre-commit |
|--------|-------|---------|-------|------------|
| 1 | Backend foundation | `feat(api): add MatchResult entity, scoring service, and WC2026 API client` | Models/MatchResult.cs, Services/ScoringService.cs, Services/Wc2026ApiClient.cs, Data/AppDbContext.cs, Migration files, appsettings.json | `dotnet build` |
| 2 | Backend services | `feat(api): add result fetcher background service and results controller` | Services/ResultFetcherService.cs, Controllers/ResultsController.cs, DTOs/ResultResponse.cs, Program.cs | `dotnet build` |
| 3 | Frontend | `feat(ui): add results context and display scores with points on match cards` | src/api/client.ts, src/context/ResultsContext.tsx, src/components/MatchCard.tsx, src/types/index.ts | `npm test` |

---

## Success Criteria

### Verification Commands
```bash
dotnet build                                    # Expected: Build succeeded
curl -s http://localhost:5211/api/results       # Expected: JSON array (may be empty before matches)
npm test                                        # Expected: All tests pass
```

### Final Checklist
- [ ] All "Must Have" present — scoring, auto-polling, 90-min results, error handling
- [ ] All "Must NOT Have" absent — no live scores, no leaderboard, no admin panel
- [ ] Backend compiles and runs without errors
- [ ] Frontend tests pass
- [ ] MatchCard shows result + tipp + poeng for ferdige kamper
- [ ] MatchCard shows unchanged UI for kommende kamper
