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
