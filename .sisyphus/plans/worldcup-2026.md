# FIFA World Cup 2026 Match Viewer

## TL;DR

> **Quick Summary**: Build a React + TypeScript web app that displays all 104 FIFA World Cup 2026 matches organized by tournament phase (tabs), with a smart countdown timer that shifts from tournament start to next match during the event.
> 
> **Deliverables**:
> - Vite + React + TypeScript project with Tailwind CSS v4
> - Static JSON dataset of all 104 matches with venues and teams
> - Tabbed match viewer (Group Stage | Round of 32 | Round of 16 | QF | SF | Final)
> - Smart countdown component (pre-tournament → next match)
> - Basic Vitest component tests
> 
> **Estimated Effort**: Medium (6-8 hours)
> **Parallel Execution**: YES - 3 waves
> **Critical Path**: Scaffold → Types + Data → Components → Polish + Tests

---

## Context

### Original Request
Build a web application to display all FIFA World Cup 2026 matches with dates, times, teams, and stadiums, plus a countdown timer.

### Interview Summary
**Key Discussions**:
- **App type**: Web app (React + TypeScript + Vite) — user is experienced developer
- **Styling**: Tailwind CSS (clean, professional)
- **Data**: Static JSON — match schedule is predetermined, no API needed
- **Knockout matches**: Show all 104 with bracket position labels ("Winner Group A vs Runner-up Group B")
- **Countdown**: Smart — counts to tournament start before June 11, then to next upcoming match
- **Match organization**: Tabs by tournament phase
- **Timezone**: Display in user's local timezone via `Intl.DateTimeFormat`
- **Testing**: Basic Vitest component tests (3-5 tests)
- **Hosting**: Local dev server only

### Research Findings
- **Tournament**: 48 teams, 104 matches, 16 venues across USA/Mexico/Canada, June-July 2026
- **Timezones**: 4+ time zones. Mexico abolished DST in 2023 (UTC-6 year-round). Must use IANA timezone identifiers, never hardcoded offsets
- **Tailwind v4.2**: Breaking changes from v3 — CSS-first config, `@import "tailwindcss"` replaces `@tailwind` directives, uses `@tailwindcss/vite` plugin (NOT PostCSS)
- **Current stack versions**: Vite 8.x, React 19.x, TypeScript 5.x, Vitest 4.x

### Metis Review
**Identified Gaps** (addressed):
- Knockout match display strategy → Resolved: bracket position labels
- Countdown target ambiguity → Resolved: smart countdown (shifts)
- Timezone handling → Resolved: user's local time via Intl API
- Match grouping UX → Resolved: tabs by tournament phase
- Scope creep risk → Locked: explicit "Must NOT Have" list

---

## Work Objectives

### Core Objective
Display all 104 FIFA World Cup 2026 matches in a tabbed interface organized by tournament phase, with a smart countdown timer, using React + TypeScript + Tailwind CSS.

### Concrete Deliverables
- Working React app served via Vite dev server
- `matches.json` with all 104 matches + venue data
- TypeScript type definitions for all data structures
- Tabbed view: Group Stage | Round of 32 | Round of 16 | QF | SF | Final
- Match cards showing: date/time (local), teams/bracket-position, venue, city
- Smart countdown header component
- 3-5 Vitest component tests

### Definition of Done
- [ ] `npm run dev` starts the app and renders all 104 matches across tabs
- [ ] `npm run build` completes with zero errors
- [ ] `npx tsc --noEmit` passes with zero type errors
- [ ] `npm test` passes all tests
- [ ] Countdown timer displays and updates every second
- [ ] All 6 tournament phase tabs are navigable and display correct matches

### Must Have
- All 104 matches displayed with correct dates, teams/bracket positions, and venues
- Tabbed navigation by tournament phase
- Smart countdown timer (tournament start → next match)
- Match times displayed in user's local timezone
- All times stored as UTC ISO 8601 in JSON
- Responsive layout (mobile-friendly)
- TypeScript strict mode with zero type errors

### Must NOT Have (Guardrails)
- ❌ React Router — single page app, tabs are local state only
- ❌ Redux/Zustand/any state management library — `useState` + props is sufficient
- ❌ Framer Motion or animation libraries — CSS transitions only
- ❌ i18n/localization — English only
- ❌ Backend/API server — static JSON imported at build time
- ❌ Live scores, WebSocket, polling — static data only
- ❌ User accounts, authentication, favorites — no personalization
- ❌ ESLint/Prettier config beyond Vite defaults
- ❌ More than ~8 component files — keep flat hierarchy
- ❌ Utility/helper files beyond one `dateUtils.ts`
- ❌ `as any`, `@ts-ignore`, or type casting workarounds
- ❌ Over-abstracted factories/strategies/adapters — simple is better

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: NO (will be set up in Task 1)
- **Automated tests**: YES (tests-after implementation)
- **Framework**: Vitest 4.x + @testing-library/react v15 + jsdom
- **Scope**: 3-5 focused component tests (Countdown, MatchCard, dateUtils)

### QA Policy
Every task includes agent-executed QA scenarios.
Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **UI Components**: Use Playwright — navigate to localhost, interact with tabs, assert content, screenshot
- **Utilities**: Use Bash — run vitest, assert pass/fail
- **Build**: Use Bash — `npm run build`, `npx tsc --noEmit`, assert exit code 0

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately — foundation):
├── Task 1: Project scaffold (Vite + React + TS + Tailwind v4 + Vitest) [quick]
└── Task 2: Match data JSON + TypeScript types [unspecified-high]

Wave 2 (After Wave 1 — core components, PARALLEL):
├── Task 3: Smart countdown component (depends: 1, 2) [deep]
├── Task 4: Match card component (depends: 1, 2) [visual-engineering]
└── Task 5: Tabbed match list with phase filtering (depends: 1, 2) [visual-engineering]

Wave 3 (After Wave 2 — integration + testing):
├── Task 6: App integration + responsive layout (depends: 3, 4, 5) [visual-engineering]
└── Task 7: Vitest component tests (depends: 3, 4, 6) [quick]

Wave FINAL (After ALL tasks — 4 parallel reviews, then user okay):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Real manual QA (unspecified-high)
└── Task F4: Scope fidelity check (deep)
-> Present results -> Get explicit user okay
```

### Dependency Matrix

| Task | Depends On | Blocks | Wave |
|------|-----------|--------|------|
| 1 | — | 3, 4, 5, 6, 7 | 1 |
| 2 | — | 3, 4, 5, 6, 7 | 1 |
| 3 | 1, 2 | 6, 7 | 2 |
| 4 | 1, 2 | 5, 6, 7 | 2 |
| 5 | 1, 2, 4 | 6 | 2 |
| 6 | 3, 4, 5 | 7 | 3 |
| 7 | 3, 4, 6 | — | 3 |
| F1-F4 | ALL | — | FINAL |

### Agent Dispatch Summary

- **Wave 1**: **2 parallel** — T1 → `quick`, T2 → `unspecified-high`
- **Wave 2**: **3 parallel** — T3 → `deep`, T4 → `visual-engineering`, T5 → `visual-engineering`
- **Wave 3**: **2 parallel** — T6 → `visual-engineering`, T7 → `quick`
- **FINAL**: **4 parallel** — F1 → `oracle`, F2 → `unspecified-high`, F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [x] 1. Project Scaffold — Vite + React + TypeScript + Tailwind CSS v4 + Vitest

  **What to do**:
  - Run `npm create vite@latest . -- --template react-ts` in the project directory (`.` = current dir, do NOT create a subdirectory)
  - Install Tailwind CSS v4 for Vite: `npm install -D tailwindcss @tailwindcss/vite`
  - Configure Vite to use Tailwind plugin: add `tailwindcss()` to `vite.config.ts` plugins array. Import from `@tailwindcss/vite`
  - Replace `src/index.css` content with: `@import "tailwindcss";` (Tailwind v4 syntax — NOT `@tailwind base/components/utilities`)
  - Install Vitest + testing-library: `npm install -D vitest jsdom @testing-library/react @testing-library/jest-dom`
  - Add Vitest config to `vite.config.ts`: `test: { globals: true, environment: 'jsdom', setupFiles: './src/test-setup.ts' }`
  - Create `src/test-setup.ts` with: `import '@testing-library/jest-dom'`
  - Add test script to `package.json`: `"test": "vitest run"`
  - Remove Vite boilerplate: delete `src/App.css`, clean `src/App.tsx` to a minimal "Hello World"
  - Verify: `npm run dev` starts successfully, `npm test` runs (0 tests is OK), `npx tsc --noEmit` passes

  **Must NOT do**:
  - Do NOT install React Router, Redux, Zustand, or any extra library
  - Do NOT create a subdirectory — scaffold in the current working directory
  - Do NOT use Tailwind v3 config pattern (no `tailwind.config.js`, no PostCSS config)
  - Do NOT use `@tailwind base; @tailwind components; @tailwind utilities;` — this is v3 syntax
  - Do NOT add ESLint/Prettier config beyond Vite defaults

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Standard scaffold with well-known tools, no complex logic
  - **Skills**: []
    - No specialized skills needed — standard npm/Vite commands

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Task 2)
  - **Blocks**: Tasks 3, 4, 5, 6, 7
  - **Blocked By**: None (can start immediately)

  **References**:

  **External References**:
  - Tailwind CSS v4 Vite install guide: https://tailwindcss.com/docs/installation/vite — Follow EXACTLY. Key points: `@tailwindcss/vite` plugin, `@import "tailwindcss"` in CSS
  - Vitest getting started: https://vitest.dev/guide/ — Configure inside `vite.config.ts`, not separate file

  **WHY Each Reference Matters**:
  - Tailwind v4 has breaking changes from v3. The Vite install guide ensures correct plugin setup and CSS syntax
  - Vitest config in vite.config.ts avoids duplicate config files and ensures Vite aliases work in tests

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Dev server starts and renders
    Tool: Bash
    Preconditions: npm install completed
    Steps:
      1. Run `npm run dev -- --port 5173 &` (background the dev server)
      2. Wait 5 seconds for server startup
      3. Run `curl -s http://localhost:5173` — capture response
      4. Kill the background dev server
    Expected Result: HTTP 200 response containing HTML with `<div id="root">`
    Failure Indicators: Connection refused, non-200 status, missing root div
    Evidence: .sisyphus/evidence/task-1-dev-server.txt

  Scenario: TypeScript compilation passes
    Tool: Bash
    Preconditions: Project scaffolded
    Steps:
      1. Run `npx tsc --noEmit`
    Expected Result: Exit code 0, no output (no errors)
    Failure Indicators: Any TypeScript error output, non-zero exit code
    Evidence: .sisyphus/evidence/task-1-typecheck.txt

  Scenario: Vitest runs without errors
    Tool: Bash
    Preconditions: Vitest configured
    Steps:
      1. Run `npm test`
    Expected Result: Exit code 0, "No test files found" or "0 tests" is acceptable
    Failure Indicators: Configuration errors, missing dependencies, non-zero exit code
    Evidence: .sisyphus/evidence/task-1-vitest.txt

  Scenario: Tailwind CSS v4 is working
    Tool: Bash
    Preconditions: Dev server running
    Steps:
      1. Run `npm run dev -- --port 5173 &`
      2. Wait 5 seconds
      3. Create a temp test: add `className="text-blue-500"` to App.tsx root div
      4. Run `curl -s http://localhost:5173` and check for Tailwind CSS output in the response
      5. Kill dev server
    Expected Result: Page loads without CSS errors, Tailwind classes are processed
    Failure Indicators: Unstyled page, CSS compilation errors in console
    Evidence: .sisyphus/evidence/task-1-tailwind.txt
  ```

  **Commit**: YES
  - Message: `chore(scaffold): init Vite + React + TS + Tailwind v4 + Vitest`
  - Files: All scaffold files
  - Pre-commit: `npm run dev` starts, `npm test` runs, `npx tsc --noEmit` passes

- [x] 2. Match Data JSON + TypeScript Type Definitions

  **What to do**:
  - Create `src/types/index.ts` with TypeScript interfaces:
    - `Team`: `{ code: string; name: string; flag: string }` (flag = emoji flag or 2-letter code)
    - `Venue`: `{ id: string; name: string; city: string; country: string; timezone: string }` (timezone = IANA identifier e.g. `"America/New_York"`)
    - `Match`: `{ id: number; date: string; homeTeam: string | null; awayTeam: string | null; homePlaceholder?: string; awayPlaceholder?: string; group?: string; stage: Stage; venueId: string }`
    - `Stage`: union type `"group" | "round-of-32" | "round-of-16" | "quarter-final" | "semi-final" | "third-place" | "final"`
    - `MatchData`: `{ teams: Record<string, Team>; venues: Venue[]; matches: Match[] }`
  - Create `src/data/venues.json` with all 16 World Cup 2026 venues:
    - Include IANA timezone for each (e.g., MetLife Stadium → `"America/New_York"`, Estadio Azteca → `"America/Mexico_City"`)
    - The 16 venues: MetLife Stadium (East Rutherford), Rose Bowl (Pasadena), AT&T Stadium (Arlington), SoFi Stadium (Los Angeles), Hard Rock Stadium (Miami), Lincoln Financial Field (Philadelphia), Lumen Field (Seattle), NRG Stadium (Houston), Estadio Azteca (Mexico City), Estadio BBVA (Monterrey), Estadio Akron (Guadalajara), BMO Field (Toronto), BC Place (Vancouver), Mercedes-Benz Stadium (Atlanta), Arrowhead Stadium (Kansas City), Levi's Stadium (Santa Clara)
  - Create `src/data/teams.json` with all 48 qualified teams with their name, FIFA code, and flag emoji
  - Create `src/data/matches.json` with all 104 matches:
    - 72 group stage matches: `homeTeam` and `awayTeam` set to team codes, `group` field set (A-L)
    - 32 knockout matches: `homeTeam: null`, `awayTeam: null`, with `homePlaceholder` and `awayPlaceholder` set (e.g., "Winner Group A", "Runner-up Group B")
    - All dates as ISO 8601 UTC strings (e.g., `"2026-06-11T20:00:00Z"`)
    - Use the actual FIFA-published schedule for dates, times, and venue assignments
  - Create `src/data/index.ts` that imports all JSON and exports typed data as `MatchData`

  **Must NOT do**:
  - Do NOT hardcode timezone offsets (e.g., `-5`, `UTC-6`) — use IANA identifiers only
  - Do NOT duplicate venue data inside match objects — normalize via `venueId` reference
  - Do NOT invent match dates — use the actual FIFA-published schedule
  - Do NOT create API endpoints or fetch calls — direct JSON import only

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Data entry of 104 matches requires attention to detail and accuracy, plus schema design
  - **Skills**: []
    - No specialized skills needed — TypeScript types and JSON data entry

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Task 1)
  - **Blocks**: Tasks 3, 4, 5, 6, 7
  - **Blocked By**: None (can start immediately — types don't need the scaffold)

  **References**:

  **External References**:
  - FIFA World Cup 2026 schedule: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026 — Official match schedule, venues, and group draws
  - IANA timezone database: Use standard identifiers like `America/New_York`, `America/Chicago`, `America/Denver`, `America/Los_Angeles`, `America/Mexico_City`, `America/Toronto`, `America/Vancouver`

  **WHY Each Reference Matters**:
  - FIFA site has the authoritative schedule. All 104 match dates, times, venues, and group assignments come from here
  - IANA timezone identifiers ensure `Intl.DateTimeFormat` works correctly, especially for Mexico (no DST since 2023)

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: TypeScript types compile without errors
    Tool: Bash
    Preconditions: Task 1 scaffold complete, types and data files created
    Steps:
      1. Run `npx tsc --noEmit`
    Expected Result: Exit code 0, no errors
    Failure Indicators: Type errors in JSON imports or interface definitions
    Evidence: .sisyphus/evidence/task-2-typecheck.txt

  Scenario: Match count is correct
    Tool: Bash
    Preconditions: Data files created
    Steps:
      1. Run `node -e "const d = require('./src/data/matches.json'); console.log('Total:', d.length); console.log('Group:', d.filter(m => m.stage === 'group').length); console.log('Knockout:', d.filter(m => m.stage !== 'group').length)"`
    Expected Result: Total: 104, Group: 72, Knockout: 32
    Failure Indicators: Wrong counts, JSON parse errors
    Evidence: .sisyphus/evidence/task-2-match-count.txt

  Scenario: All venues have IANA timezone identifiers
    Tool: Bash
    Preconditions: venues.json created
    Steps:
      1. Run `node -e "const v = require('./src/data/venues.json'); v.forEach(venue => { if (!venue.timezone.startsWith('America/')) throw new Error(venue.name + ' has invalid tz: ' + venue.timezone) }); console.log('All', v.length, 'venues have valid IANA timezones')"`
    Expected Result: "All 16 venues have valid IANA timezones"
    Failure Indicators: Error thrown, missing timezone field, non-IANA identifiers
    Evidence: .sisyphus/evidence/task-2-venue-timezones.txt

  Scenario: All dates are ISO 8601 UTC
    Tool: Bash
    Preconditions: matches.json created
    Steps:
      1. Run `node -e "const m = require('./src/data/matches.json'); m.forEach(match => { if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/.test(match.date)) throw new Error('Match ' + match.id + ' has invalid date: ' + match.date) }); console.log('All', m.length, 'matches have valid UTC dates')"`
    Expected Result: "All 104 matches have valid UTC dates"
    Failure Indicators: Non-UTC dates, missing Z suffix, invalid format
    Evidence: .sisyphus/evidence/task-2-date-format.txt

  Scenario: Knockout matches have placeholder labels
    Tool: Bash
    Preconditions: matches.json created
    Steps:
      1. Run `node -e "const m = require('./src/data/matches.json'); const ko = m.filter(m => m.stage !== 'group'); ko.forEach(match => { if (!match.homePlaceholder || !match.awayPlaceholder) throw new Error('Match ' + match.id + ' missing placeholders') }); console.log('All', ko.length, 'knockout matches have placeholders')"`
    Expected Result: "All 32 knockout matches have placeholders"
    Failure Indicators: Missing homePlaceholder or awayPlaceholder on knockout matches
    Evidence: .sisyphus/evidence/task-2-knockout-placeholders.txt
  ```

  **Commit**: YES
  - Message: `feat(data): add WC2026 match data and TypeScript types`
  - Files: `src/types/index.ts`, `src/data/*.json`, `src/data/index.ts`
  - Pre-commit: `npx tsc --noEmit`

- [x] 3. Smart Countdown Component + Date Utilities

  **What to do**:
  - Create `src/utils/dateUtils.ts` with utility functions:
    - `formatMatchDate(isoDate: string): string` — formats date in user's locale using `Intl.DateTimeFormat` with weekday, date, month, year
    - `formatMatchTime(isoDate: string): string` — formats time in user's locale with hours and minutes
    - `getTimeUntil(targetDate: string): { days: number; hours: number; minutes: number; seconds: number }` — calculates time remaining
    - `getNextMatch(matches: Match[]): Match | null` — finds the next upcoming match from now
    - `isBeforeTournament(firstMatchDate: string): boolean` — checks if current time is before tournament start
  - Create `src/components/Countdown.tsx`:
    - Smart behavior: If before tournament start → count down to opening match (June 11, 2026). During tournament → count down to next upcoming match
    - Display format: `Xd Xh Xm Xs` — large, prominent styling
    - Show context text: "Until World Cup 2026 begins" or "Until [Team A] vs [Team B]" (or "Until next match" for knockout TBD matches)
    - Use `useEffect` with `setInterval(1000)` for tick updates
    - Use `Date.now()` delta calculation on each tick (NOT incremental subtraction — avoids drift)
    - When countdown reaches 0, auto-advance to next match
    - If no upcoming matches remain, show "World Cup 2026 has ended"

  **Must NOT do**:
  - Do NOT install date libraries (date-fns, luxon, dayjs) — native `Intl` API is sufficient
  - Do NOT hardcode timezone offsets — let `Intl.DateTimeFormat` handle it
  - Do NOT use incremental subtraction for countdown (causes drift over time)
  - Do NOT add animation libraries for the countdown

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: Timer logic with drift prevention, smart state switching, and timezone handling requires careful implementation
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 4, 5)
  - **Blocks**: Tasks 6, 7
  - **Blocked By**: Tasks 1, 2

  **References**:

  **External References**:
  - MDN Intl.DateTimeFormat: https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Intl/DateTimeFormat — For locale-aware date/time formatting
  - MDN setInterval: https://developer.mozilla.org/en-US/docs/Web/API/setInterval — For cleanup pattern with useEffect

  **WHY Each Reference Matters**:
  - `Intl.DateTimeFormat` automatically handles the user's locale and timezone — no manual offset calculation needed
  - `setInterval` + `useEffect` cleanup prevents memory leaks when component unmounts

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Countdown renders and ticks
    Tool: Playwright
    Preconditions: Dev server running with Countdown component mounted in App
    Steps:
      1. Navigate to http://localhost:5173
      2. Locate countdown element (look for text matching pattern /\d+d\s+\d+h\s+\d+m\s+\d+s/ or similar)
      3. Capture the countdown text value
      4. Wait 2 seconds
      5. Capture the countdown text value again
    Expected Result: Second capture differs from first (seconds have decreased)
    Failure Indicators: Text unchanged after 2 seconds, countdown not found, NaN in display
    Evidence: .sisyphus/evidence/task-3-countdown-ticks.png

  Scenario: Countdown shows context text
    Tool: Playwright
    Preconditions: Dev server running
    Steps:
      1. Navigate to http://localhost:5173
      2. Look for text containing "Until" near the countdown
    Expected Result: Context text visible — either "Until World Cup 2026 begins" or "Until [match description]"
    Failure Indicators: No context text, generic "countdown" text without match info
    Evidence: .sisyphus/evidence/task-3-countdown-context.png

  Scenario: Date formatting utility works correctly
    Tool: Bash
    Preconditions: dateUtils.ts created
    Steps:
      1. Run `npx tsc --noEmit` to verify types
      2. Run a quick node script: `node -e "... test formatMatchDate with known input ..."`
    Expected Result: Exit code 0, date formats to a human-readable locale string
    Failure Indicators: Type errors, NaN or Invalid Date in output
    Evidence: .sisyphus/evidence/task-3-date-utils.txt
  ```

  **Commit**: YES (groups with Task 4+5)
  - Message: `feat(countdown): add smart countdown component and date utilities`
  - Files: `src/components/Countdown.tsx`, `src/utils/dateUtils.ts`
  - Pre-commit: `npm run dev` renders countdown

- [ ] 4. Match Card Component

  **What to do**:
  - Create `src/components/MatchCard.tsx`:
    - Props: `match: Match`, `teams: Record<string, Team>`, `venues: Venue[]`
    - Display: Date + time (formatted with `dateUtils`), home team vs away team, venue name + city
    - For group matches: Show team names and flag emojis (looked up from teams record by code)
    - For knockout matches: Show `homePlaceholder` / `awayPlaceholder` text (e.g., "Winner Group A")
    - Show group label for group stage matches (e.g., "Group A")
    - Show stage label for knockout matches (e.g., "Quarter-final")
    - Clean card design with Tailwind: subtle border/shadow, good spacing, readable typography
    - Add `data-testid="match-card"` for testing

  **Must NOT do**:
  - Do NOT add click handlers, modals, or expandable details — cards are display-only
  - Do NOT duplicate venue data lookup logic — pass venues as prop, look up by `venueId`
  - Do NOT create separate components for group vs knockout cards — one component with conditional rendering
  - Do NOT add animations or hover effects beyond basic CSS transitions

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: UI component with design focus — card layout, typography, responsive styling
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 3, 5)
  - **Blocks**: Tasks 5, 6, 7
  - **Blocked By**: Tasks 1, 2

  **References**:

  **Pattern References**:
  - `src/types/index.ts` — Match, Team, Venue types (created in Task 2) — Use these exact interfaces for props typing
  - `src/utils/dateUtils.ts` — `formatMatchDate()`, `formatMatchTime()` (created in Task 3) — Use for all date/time display

  **External References**:
  - Tailwind CSS card patterns: Use `rounded-lg shadow-sm border p-4` pattern for cards

  **WHY Each Reference Matters**:
  - Types file defines the data contract — MatchCard must accept exactly these types
  - dateUtils provides locale-aware formatting — never format dates directly in the component

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Group match card renders team names
    Tool: Playwright
    Preconditions: Dev server running, MatchCard integrated (even temporarily in App.tsx for testing)
    Steps:
      1. Navigate to http://localhost:5173
      2. Find a match card with data-testid="match-card"
      3. Verify it contains two team names (not "TBD" or "null")
      4. Verify it contains a date and time
      5. Verify it contains a venue name
    Expected Result: Card displays team names, date/time, and venue
    Failure Indicators: "null", "undefined", "TBD" in group match, missing date/venue
    Evidence: .sisyphus/evidence/task-4-group-match-card.png

  Scenario: Knockout match card renders bracket labels
    Tool: Playwright
    Preconditions: Dev server running with a knockout match card visible
    Steps:
      1. Navigate to http://localhost:5173
      2. Switch to a knockout tab (e.g., Round of 32)
      3. Find a match card
      4. Verify it contains placeholder text like "Winner Group" or "Runner-up Group"
    Expected Result: Knockout card shows bracket position labels instead of team names
    Failure Indicators: "null" displayed, empty team slots, group match data in knockout tab
    Evidence: .sisyphus/evidence/task-4-knockout-match-card.png
  ```

  **Commit**: YES (groups with Task 3+5)
  - Message: `feat(matches): add match card and tabbed list components`
  - Files: `src/components/MatchCard.tsx`
  - Pre-commit: `npx tsc --noEmit`

- [ ] 5. Tabbed Match List with Phase Filtering

  **What to do**:
  - Create `src/components/TabNav.tsx`:
    - Renders tab buttons for each tournament phase: "Group Stage", "Round of 32", "Round of 16", "Quarter-finals", "Semi-finals", "Final"
    - Props: `activeTab: Stage`, `onTabChange: (stage: Stage) => void`
    - Active tab has distinct styling (bold, underline, or filled background)
    - Responsive: horizontal scroll on mobile if tabs overflow
  - Create `src/components/MatchList.tsx`:
    - Props: `matches: Match[]`, `teams: Record<string, Team>`, `venues: Venue[]`, `activeStage: Stage`
    - Filters matches by `activeStage`
    - For group stage: Sub-group matches by `group` field (show "Group A", "Group B" headers)
    - For knockout stages: Show matches in chronological order
    - Within each group/day: sort chronologically by date
    - Renders `MatchCard` for each match
    - Show match count: "48 matches" (for group stage) etc.

  **Must NOT do**:
  - Do NOT use React Router for tabs — local `useState` only
  - Do NOT add search/filter beyond the phase tabs (no team filter, no date filter)
  - Do NOT create a separate component per tab — one MatchList with filtering logic
  - Do NOT add pagination — show all matches in the active tab at once

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: Tab navigation UI, responsive layout, visual grouping of matches
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 3, 4) — but depends on Task 4 for MatchCard component
  - **Blocks**: Task 6
  - **Blocked By**: Tasks 1, 2, 4

  **References**:

  **Pattern References**:
  - `src/types/index.ts` — `Stage` union type (created in Task 2) — Use for tab values and filtering
  - `src/components/MatchCard.tsx` — (created in Task 4) — Render this for each match

  **WHY Each Reference Matters**:
  - Stage type ensures tab values exactly match the data's stage field — no string mismatches
  - MatchCard is the rendering unit — MatchList composes it, doesn't duplicate its logic

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: All 6 tabs render and are clickable
    Tool: Playwright
    Preconditions: Dev server running with TabNav + MatchList in App
    Steps:
      1. Navigate to http://localhost:5173
      2. Verify 6 tab buttons are visible: "Group Stage", "Round of 32", "Round of 16", "Quarter-finals", "Semi-finals", "Final"
      3. Click each tab in sequence
      4. After each click, verify the match list content changes
    Expected Result: All 6 tabs exist, each click changes the visible matches
    Failure Indicators: Missing tabs, click does nothing, same content on all tabs
    Evidence: .sisyphus/evidence/task-5-tabs-navigation.png

  Scenario: Group stage tab shows grouped matches
    Tool: Playwright
    Preconditions: Dev server running, Group Stage tab active
    Steps:
      1. Navigate to http://localhost:5173
      2. Ensure "Group Stage" tab is active (click if needed)
      3. Look for group headers ("Group A", "Group B", etc.)
      4. Count total match cards displayed
    Expected Result: Group headers visible (A through L), 72 match cards total
    Failure Indicators: No group headers, wrong match count, unsorted matches
    Evidence: .sisyphus/evidence/task-5-group-stage.png

  Scenario: Knockout tab shows correct matches
    Tool: Playwright
    Preconditions: Dev server running
    Steps:
      1. Click "Round of 32" tab
      2. Count match cards
      3. Verify cards show bracket labels (not team names)
    Expected Result: 16 match cards with bracket position labels
    Failure Indicators: Wrong count, team names instead of placeholders
    Evidence: .sisyphus/evidence/task-5-knockout-tab.png
  ```

  **Commit**: YES (groups with Task 3+4)
  - Message: `feat(matches): add match card and tabbed list components`
  - Files: `src/components/TabNav.tsx`, `src/components/MatchList.tsx`
  - Pre-commit: `npm run dev` renders tabs and matches

- [ ] 6. App Integration + Header + Responsive Layout

  **What to do**:
  - Create `src/components/Header.tsx`:
    - App title: "FIFA World Cup 2026" with trophy emoji ⚽🏆
    - Subtitle: "USA • Mexico • Canada"
    - Clean, centered header with Tailwind styling
  - Update `src/App.tsx` to integrate all components:
    - Import match data from `src/data/index.ts`
    - `useState<Stage>` for active tab (default: `"group"`)
    - Layout: Header → Countdown → TabNav → MatchList
    - Pass all required props to child components
  - Update `src/index.css`:
    - Keep `@import "tailwindcss"` at top
    - Add minimal custom styles if needed (background color, font smoothing)
    - Set body background to a subtle color (e.g., `bg-gray-50` or `bg-slate-50`)
  - Responsive design:
    - Mobile-first: single column, cards stack vertically
    - Tablet/Desktop: wider cards with more horizontal space
    - Tab navigation: horizontal scroll on small screens
    - Countdown: smaller text on mobile, larger on desktop

  **Must NOT do**:
  - Do NOT add a footer, sidebar, or navigation beyond the tab bar
  - Do NOT add dark mode toggle
  - Do NOT add custom fonts or font loading
  - Do NOT create more than 1 additional component file (Header.tsx) — keep it simple
  - Do NOT add page transitions or route animations

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: Final UI assembly, responsive layout, visual polish — design-focused work
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3
  - **Blocks**: Task 7
  - **Blocked By**: Tasks 3, 4, 5

  **References**:

  **Pattern References**:
  - `src/components/Countdown.tsx` — (Task 3) — Import and place in layout
  - `src/components/TabNav.tsx` — (Task 5) — Import, wire `activeTab` state
  - `src/components/MatchList.tsx` — (Task 5) — Import, pass filtered data
  - `src/data/index.ts` — (Task 2) — Import typed match data
  - `src/types/index.ts` — (Task 2) — `Stage` type for useState

  **WHY Each Reference Matters**:
  - All components from Tasks 3-5 need to be composed in App.tsx — understanding their prop interfaces is critical
  - Data index provides the typed dataset — import pattern must match Task 2's export

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Full app renders with all sections
    Tool: Playwright
    Preconditions: Dev server running with all components integrated
    Steps:
      1. Navigate to http://localhost:5173
      2. Verify Header is visible with "FIFA World Cup 2026" text
      3. Verify Countdown is visible with ticking timer
      4. Verify TabNav shows 6 tabs
      5. Verify MatchList shows match cards
      6. Take full-page screenshot
    Expected Result: All 4 sections visible in correct order: Header, Countdown, Tabs, Matches
    Failure Indicators: Missing sections, blank page, console errors
    Evidence: .sisyphus/evidence/task-6-full-app.png

  Scenario: Responsive layout at mobile width
    Tool: Playwright
    Preconditions: Dev server running
    Steps:
      1. Set viewport to 375x812 (iPhone dimensions)
      2. Navigate to http://localhost:5173
      3. Verify content is not horizontally overflowing
      4. Verify match cards stack vertically
      5. Verify tabs are scrollable horizontally
      6. Take screenshot
    Expected Result: Clean mobile layout, no horizontal overflow, readable text
    Failure Indicators: Horizontal scroll on body, overlapping elements, cut-off text
    Evidence: .sisyphus/evidence/task-6-mobile-responsive.png

  Scenario: Build succeeds
    Tool: Bash
    Preconditions: All components integrated
    Steps:
      1. Run `npm run build`
      2. Run `npx tsc --noEmit`
    Expected Result: Both commands exit code 0, no errors
    Failure Indicators: Build errors, type errors, missing imports
    Evidence: .sisyphus/evidence/task-6-build.txt

  Scenario: Tab switching works in integrated app
    Tool: Playwright
    Preconditions: Dev server running
    Steps:
      1. Navigate to http://localhost:5173
      2. Click "Round of 32" tab
      3. Verify match cards update to show knockout matches
      4. Click "Final" tab
      5. Verify only 1 match card is displayed (the final)
    Expected Result: Tab switching changes displayed matches, Final tab shows exactly 1 match
    Failure Indicators: Matches don't change, wrong count on Final tab, errors on switch
    Evidence: .sisyphus/evidence/task-6-tab-switching.png
  ```

  **Commit**: YES
  - Message: `feat(app): integrate all components with responsive layout`
  - Files: `src/App.tsx`, `src/index.css`, `src/components/Header.tsx`
  - Pre-commit: `npm run build` succeeds

- [ ] 7. Vitest Component Tests

  **What to do**:
  - Create `src/__tests__/dateUtils.test.ts`:
    - Test `formatMatchDate`: pass a known UTC date string, assert output contains expected date components
    - Test `getTimeUntil`: pass a future date, assert all fields are positive numbers
    - Test `getNextMatch`: pass array with past and future matches, assert it returns the correct next match
  - Create `src/__tests__/Countdown.test.tsx`:
    - Test that Countdown component renders without crashing
    - Test that it displays a timer pattern (days, hours, minutes, seconds)
    - Mock `Date.now()` to control the timer for predictable assertions
  - Create `src/__tests__/MatchCard.test.tsx`:
    - Test that MatchCard renders team names for group stage match (pass mock data)
    - Test that MatchCard renders placeholders for knockout match (pass mock data with `homePlaceholder`/`awayPlaceholder`)
    - Test that it displays venue name and formatted date

  **Must NOT do**:
  - Do NOT aim for 100% coverage — 3-5 focused tests is the target
  - Do NOT add E2E tests (Playwright tests are handled in QA scenarios)
  - Do NOT add snapshot tests — they're brittle for this use case
  - Do NOT test implementation details (internal state, private functions)
  - Do NOT install additional test utilities beyond @testing-library/react

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Small number of straightforward tests following established patterns
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Task 6)
  - **Blocks**: None
  - **Blocked By**: Tasks 3, 4, 6 (needs components to test)

  **References**:

  **Pattern References**:
  - `src/test-setup.ts` — (Task 1) — Test setup file with jest-dom import
  - `src/components/Countdown.tsx` — (Task 3) — Component to test
  - `src/components/MatchCard.tsx` — (Task 4) — Component to test
  - `src/utils/dateUtils.ts` — (Task 3) — Utility functions to test
  - `src/types/index.ts` — (Task 2) — Types for creating mock data in tests

  **External References**:
  - @testing-library/react docs: https://testing-library.com/docs/react-testing-library/intro — render, screen, getByText patterns
  - Vitest mocking: https://vitest.dev/guide/mocking — vi.useFakeTimers for Countdown test

  **WHY Each Reference Matters**:
  - test-setup.ts extends expect with jest-dom matchers — tests will fail without it
  - Testing library's `render` and `screen` are the API for component tests — use getByText, getByTestId patterns
  - Vitest's fake timers let us control Date.now() for deterministic countdown testing

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: All tests pass
    Tool: Bash
    Preconditions: Test files created, all components implemented
    Steps:
      1. Run `npm test`
    Expected Result: Exit code 0, 5-8 tests pass, 0 failures
    Failure Indicators: Non-zero exit code, test failures, configuration errors
    Evidence: .sisyphus/evidence/task-7-test-results.txt

  Scenario: TypeScript compiles with test files
    Tool: Bash
    Preconditions: Test files created
    Steps:
      1. Run `npx tsc --noEmit`
    Expected Result: Exit code 0, no type errors in test files
    Failure Indicators: Type errors in mock data construction or component rendering
    Evidence: .sisyphus/evidence/task-7-typecheck.txt
  ```

  **Commit**: YES
  - Message: `test: add component tests for Countdown, MatchCard, dateUtils`
  - Files: `src/__tests__/dateUtils.test.ts`, `src/__tests__/Countdown.test.tsx`, `src/__tests__/MatchCard.test.tsx`
  - Pre-commit: `npm test` passes all tests

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Present consolidated results to user and get explicit "okay" before completing.

- [ ] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read file, open browser, run command). For each "Must NOT Have": search codebase for forbidden patterns (React Router, Redux, Zustand, Framer Motion, i18next imports). Check evidence files exist in `.sisyphus/evidence/`. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Code Quality Review** — `unspecified-high`
  Run `npx tsc --noEmit` + `npm test` + `npm run build`. Review all .ts/.tsx files for: `as any`/`@ts-ignore`, empty catches, `console.log` in prod, commented-out code, unused imports. Check AI slop: excessive comments, over-abstraction, generic variable names (data/result/item/temp). Verify component count ≤ 8 files. Verify no state management library installed.
  Output: `Build [PASS/FAIL] | TypeCheck [PASS/FAIL] | Tests [N pass/N fail] | Files [N clean/N issues] | VERDICT`

- [ ] F3. **Real Manual QA** — `unspecified-high` (+ `playwright` skill)
  Start dev server. Open in Playwright. Verify: (1) App renders with countdown visible, (2) All 6 tabs exist and are clickable, (3) Group Stage tab shows group matches with real team names, (4) Knockout tabs show bracket position labels, (5) Match cards show date/time/venue, (6) Responsive at mobile width (375px). Capture screenshots for each tab. Save to `.sisyphus/evidence/final-qa/`.
  Output: `Scenarios [N/N pass] | Tabs [6/6] | Responsive [PASS/FAIL] | VERDICT`

- [ ] F4. **Scope Fidelity Check** — `deep`
  For each task: read "What to do", verify actual implementation matches. Check: no React Router installed, no state management library, no animation library, no i18n, no backend code, no API calls. Verify component count ≤ 8. Verify total utility files = 1 (dateUtils.ts). Flag any unplanned additions.
  Output: `Tasks [N/N compliant] | Forbidden Deps [CLEAN/N found] | Scope [CLEAN/N issues] | VERDICT`

---

## Commit Strategy

| After Task | Commit Message | Files | Pre-commit Check |
|-----------|---------------|-------|-----------------|
| 1 | `chore(scaffold): init Vite + React + TS + Tailwind v4 + Vitest` | All scaffold files | `npm run dev` exits, `npm test` runs |
| 2 | `feat(data): add WC2026 match data and TypeScript types` | `src/data/`, `src/types/` | `npx tsc --noEmit` |
| 3 | `feat(countdown): add smart countdown component` | `src/components/Countdown.tsx`, `src/utils/dateUtils.ts` | `npm run dev` |
| 4+5 | `feat(matches): add match card and tabbed list components` | `src/components/MatchCard.tsx`, `src/components/MatchList.tsx`, `src/components/TabNav.tsx` | `npm run dev` |
| 6 | `feat(app): integrate all components with responsive layout` | `src/App.tsx`, `src/index.css`, `src/components/Header.tsx` | `npm run build` |
| 7 | `test: add component tests for Countdown, MatchCard, dateUtils` | `src/__tests__/` | `npm test` |

---

## Success Criteria

### Verification Commands
```bash
npm run dev          # Expected: Vite dev server starts, app renders on localhost
npm run build        # Expected: Build succeeds with zero errors
npx tsc --noEmit     # Expected: Zero TypeScript errors
npm test             # Expected: All tests pass (3-5 tests)
```

### Final Checklist
- [ ] All 104 matches displayed across 6 tabs
- [ ] Countdown timer visible and updating
- [ ] Group stage matches show real team names
- [ ] Knockout matches show bracket position labels
- [ ] Times displayed in user's local timezone
- [ ] App is responsive at mobile widths
- [ ] Zero `as any` / `@ts-ignore` in codebase
- [ ] No forbidden dependencies installed
- [ ] All "Must NOT Have" items absent from codebase
