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
