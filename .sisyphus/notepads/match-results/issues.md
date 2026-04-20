# match-results — Issues

- One supplied scoring example appears inconsistent with the stated 2+1+1 model: `1-3` vs `2-3` evaluates to 3 under the documented rules, but the task expected 1.

## Task 9 — E2E Verification (2026-04-11)

- **Playwright MCP browser fails**: The built-in Playwright MCP tool cannot connect to Chrome via CDP (WebSocket 200 error). Used `node /tmp/playwright-test/verify4.js` with the `playwright` npm package installed in /tmp as workaround.
- **Frontend auth gate**: App requires Google OAuth login (ProtectedRoute). Bypassed in Playwright tests using `context.addInitScript` to inject `auth_token`/`auth_user` into localStorage before React mounts.
- **ResultsContext catches 401 and clears results**: When `getUserPoints()` returns 401, the catch block clears BOTH results AND points maps. Fixed in Playwright test by intercepting `/api/results/points` to return 200 `[]`.
- **Old API already running on port 5211**: An older API build was running and didn't have the /api/results endpoint. Had to kill it and start fresh.
