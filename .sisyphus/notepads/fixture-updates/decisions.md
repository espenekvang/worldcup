# Decisions — fixture-updates

## [2026-04-11] Arkitekturvalg
- Lagdata hentes fra WC2026 API, ikke beregnet lokalt
- matches.json oppdateres som lagringskilde (ikke database)
- MatchScheduleProvider wrapper erstatter direkte singleton-injeksjon
- `manualOverride: true` flagg i matches.json for admin-overstyringer
- SemaphoreSlim(1,1) for concurrent write-protection
- Atomisk skriving: temp-fil + File.Move(overwrite: true)
- MatchNumber brukes som primærnøkkel for API-mapping (ikke bare kickoff-tid)
- Frontend fallback: statisk import fra matches.json hvis API feiler
- Backend-testene etableres i `tests/WorldCup.Api.Tests/` som separat xUnit-prosjekt referert fra solutionen

## [2026-04-12] T12 test strategy
- Admin PUT coverage in unit tests should validate the happy-path override behavior, not ASP.NET authorization attributes.
- Reflection is acceptable for exercising the private fixture-update method when the background service loop is too timing-sensitive for a stable test.
