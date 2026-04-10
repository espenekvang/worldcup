# Learnings

## [2026-04-09] Session Start
- Project: FIFA World Cup 2026 Match Viewer
- Stack: React 19 + TypeScript 5 + Vite 8 + Tailwind CSS v4.2 + Vitest 4
- CRITICAL: Tailwind v4 uses `@import "tailwindcss"` in CSS + `@tailwindcss/vite` plugin (NOT PostCSS, NOT `tailwind.config.js`)
- CRITICAL: All match times must be stored as ISO 8601 UTC strings (e.g., `"2026-06-11T20:00:00Z"`)
- CRITICAL: Use IANA timezone identifiers (e.g., `America/New_York`), never hardcoded offsets
- Mexico abolished DST in 2023 — stays UTC-6 year-round (use `America/Mexico_City`)
- Countdown: Use `Date.now()` delta calculation on each tick, NOT incremental subtraction (avoids drift)
- Component count MUST stay ≤ 8 files: App, Header, Countdown, MatchCard, MatchList, TabNav (max)
- Only one utility file: `src/utils/dateUtils.ts`
- Data: Static JSON imported directly via Vite, NO backend, NO API calls
- Scaffold with: `npm create vite@latest . -- --template react-ts`
- Installed versions observed in this session: Vite 8.0.8, tailwindcss 4.2.2, @tailwindcss/vite 4.2.2, Vitest 4.1.4, jsdom 27.4.0, @testing-library/react 16.3.2, @testing-library/jest-dom 6.9.1

## [2026-04-09] Task 2: Match Data + TypeScript Types
- JSON imports work with Vite's `moduleResolution: "bundler"` — no `resolveJsonModule` needed
- tsconfig.app.json already supports JSON imports via bundler mode
- 48 teams stored as `Record<string, Team>` keyed by FIFA 3-letter code
- Scotland flag: 🏴󠁧󠁢󠁳󠁣󠁴󠁿 (special emoji using tag characters), England: 🏴󠁧󠁢󠁥󠁮󠁧󠁿
- All match times stored as ISO 8601 UTC; ET=UTC-4 in summer, CT=UTC-5, PT=UTC-7, Mexico City=UTC-6 (no DST)
- Match ID scheme: 1-72 group stage, 73-88 round-of-32, 89-96 round-of-16, 97-100 quarter-finals, 101-102 semi-finals, 103 third-place, 104 final
- Knockout matches: homeTeam/awayTeam = null, use homePlaceholder/awayPlaceholder strings
- venueId "rosebowl" added even though task says "Rose Bowl, Pasadena" — used as playoff/overflow venue
- `npx tsc --noEmit` passes clean; `npm run build` builds 190KB JS bundle
- Groups confirmed via ESPN/CBS/NBC Sports (April 2026): A=MEX/KOR/RSA/CZE, B=CAN/SUI/QAT/BIH, C=BRA/MAR/SCO/HAI, D=USA/PAR/AUS/TUR, E=GER/ECU/CIV/CUW, F=NED/JPN/TUN/SWE, G=BEL/IRN/EGY/NZL, H=ESP/URU/KSA/CPV, I=FRA/SEN/NOR/IRQ, J=ARG/AUT/ALG/JOR, K=POR/COL/UZB/COD, L=ENG/CRO/PAN/GHA

## [2026-04-09] Task 3: Countdown + Date Utilities
- `dateUtils.ts` stays the single shared utility file and uses native `Intl`/`Date` APIs only
- Countdown ticking recalculates from `Date.now()` on each interval to avoid timer drift
- Pre-tournament mode keeps `targetMatch` null for the "Until World Cup 2026 begins" label while still counting down to the first kickoff date
- Match context falls back to placeholder labels when knockout fixtures do not yet have concrete team codes

## [2026-04-09] F1 Audit Remediation
- Countdown opener date now derives from `matches` by sorting ISO date strings and taking the earliest kickoff instead of using a hardcoded constant
- Enabling `strict: true` in both app and node tsconfig exposed JSON import widening; runtime parser/type-guard functions in `src/data/index.ts` preserve strong typing without `as` assertions
- `npx tsc --noEmit`, `npm run build`, and `npm test` all pass after the strict-mode/data typing cleanup

## F3 Real Manual QA — 2026-04-09

### QA Execution Notes
- Dev server: `npm run dev -- --port 5173` starts successfully
- Playwright MCP (Chrome 146) has WebSocket connection issues on this machine — workaround: use `playwright` npm package via npx cache with explicit `executablePath` pointing to Playwright's own bundled Chromium at `~/.npm/_npx/.../playwright` + `~/.ms-playwright/chromium-1181`
- Command: `NODE_PATH=... node script.cjs` with `executablePath: '/Users/eekvang/Library/Caches/ms-playwright/chromium-1181/chrome-mac/Chromium.app/Contents/MacOS/Chromium'`

### App Behavior Verified
- Countdown shows: `62 DAYS : 23 HOURS : 32 MIN : 33 SEC` — live updating
- All 6 tabs render correctly with exact names: Group Stage, Round of 32, Round of 16, Quarter-finals, Semi-finals, Final
- Group Stage: 14 real team names confirmed (Mexico, USA, Canada, Brazil, Argentina, Germany, France, Spain, England, Morocco, Portugal, Japan, Saudi Arabia, South Africa)
- Knockout tabs show `Winner Group C vs Runner-up Group F` style bracket labels
- Match cards show venue names (NRG Stadium, Houston, etc.), dates (Tuesday 30 June 2026), times (19:00)
- Mobile responsive at 375px: docWidth === winWidth === 375, no overflow

### Console Errors
- Only 1 error: `favicon.ico 404` — non-critical, cosmetic only
- No JavaScript runtime errors

