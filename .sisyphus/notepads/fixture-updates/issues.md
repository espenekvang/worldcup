# Issues — fixture-updates

## [2026-04-12] Test timing sensitivity
- The background `ResultFetcherService` loop is too timing-sensitive for a stable write-path assertion, so the fixture-update test uses direct invocation of the private method.
