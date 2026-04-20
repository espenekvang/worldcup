# MatchCard Mobile Layout — Responsive Stacked Design

## TL;DR

> **Quick Summary**: Redesign MatchCard.tsx to use a two-row stacked layout on mobile (<640px) so country names like "Bosnia-Hercegovina" and "Elfenbenskysten" display without truncation. Desktop layout stays identical.
> 
> **Deliverables**:
> - Updated `src/components/MatchCard.tsx` with responsive two-row mobile layout
> - All existing tests passing
> - Visual QA evidence at 375px and 768px viewports
> 
> **Estimated Effort**: Short
> **Parallel Execution**: NO — sequential (single component, 3 tasks with dependencies)
> **Critical Path**: Task 1 (implement) → Task 2 (verify tests/types) → Task 3 (visual QA)

---

## Context

### Original Request
User reported that country names are truncated on iPhone screens, making the app hard to read on mobile. Requested a stacked layout to give team names more room.

### Interview Summary
**Key Discussions**:
- **Layout strategy**: User chose two-row stacked layout on mobile — top row has time/badge + teams + score, bottom row has action buttons/badges
- **Desktop preservation**: Current horizontal layout stays identical at `sm:` (640px) and above
- **Scope**: MatchCard.tsx only — no other component changes
- **Testing**: Verify existing Vitest tests still pass

**Research Findings**:
- MatchCard.tsx is 162 lines, pure presentational, uses Tailwind CSS with custom CSS variables
- Team names range from "USA" (3 chars) to "Bosnia-Hercegovina" (19 chars)
- Current `truncate` class on team name spans (lines 74, 84) causes the cutting
- Only 2 existing tests — both check text content, not layout. Layout changes are safe.
- Knockout placeholders like "1st Group A" are shorter than worst-case team names

### Metis Review
**Identified Gaps** (addressed):
- **Specific layout design**: Resolved — user chose two-row layout (Option A)
- **Minimum screen width**: Default 375px (standard iPhone). 320px (iPhone SE) is edge case but `break-words` provides safety net
- **Truncate removal strategy**: Remove on mobile, use `sm:truncate` as desktop safety net
- **Flag emoji width variance**: Keep flag + name in same flex child so they stay together
- **Dark mode verification**: Added as QA scenario

---

## Work Objectives

### Core Objective
Restructure MatchCard's internal layout to use a responsive two-row design on mobile, ensuring all country names display fully without truncation.

### Concrete Deliverables
- Modified `src/components/MatchCard.tsx` with mobile-first responsive layout

### Definition of Done
- [ ] "Bosnia-Hercegovina" displays without truncation at 375px viewport width
- [ ] Desktop layout at 768px is visually identical to current design
- [ ] `npm test` passes with 0 failures
- [ ] `npx tsc --noEmit` produces 0 errors

### Must Have
- Two-row layout on mobile: Row 1 = time/badge + team names + score; Row 2 = action buttons/badges
- Single-row horizontal layout preserved on `sm:` (640px+)
- All team names display fully on mobile (no truncation)
- All interactive elements remain accessible and clickable
- Dark mode works correctly at mobile width

### Must NOT Have (Guardrails)
- NO changes to any file other than `src/components/MatchCard.tsx`
- NO new dependencies, components, hooks, or files
- NO JavaScript media queries — CSS-only responsive via Tailwind
- NO changes to `MatchCardProps` interface
- NO changes to conditional rendering logic (locked, prediction, result, teamsUndetermined)
- NO CSS transitions/animations for responsive switching
- NO font size changes, color changes, or theming modifications
- NO extraction of sub-components or structural refactoring beyond layout
- NO "while we're here" improvements to button sizes, touch targets, or other UI polish
- `data-testid="match-card"` must remain on root element

---

## Verification Strategy (MANDATORY)

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: YES (Vitest + jsdom + @testing-library/react)
- **Automated tests**: Verify existing tests pass (no new tests needed — layout change only)
- **Framework**: Vitest

### QA Policy
Every task MUST include agent-executed QA scenarios.
Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **Frontend/UI**: Use Playwright — Navigate, interact, assert DOM, screenshot
- **Build verification**: Use Bash — run test and type-check commands

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Sequential — single component with dependencies):
├── Task 1: Restructure MatchCard.tsx mobile layout [visual-engineering]
├── Task 2: Verify tests + type-check pass (depends: 1) [quick]
└── Task 3: Visual QA via Playwright at 375px and 768px (depends: 1) [visual-engineering]

Wave FINAL (After ALL tasks — review):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Real manual QA (unspecified-high)
└── Task F4: Scope fidelity check (deep)
-> Present results -> Get explicit user okay

Critical Path: Task 1 → Task 2 → Task 3 → F1-F4 → user okay
```

### Dependency Matrix

| Task | Depends On | Blocks | Wave |
|------|-----------|--------|------|
| 1 | — | 2, 3 | 1 |
| 2 | 1 | F1-F4 | 1 |
| 3 | 1 | F1-F4 | 1 |
| F1-F4 | 2, 3 | — | FINAL |

### Agent Dispatch Summary

- **Wave 1**: **3 tasks** — T1 → `visual-engineering`, T2 → `quick`, T3 → `visual-engineering` (T2 and T3 can run in parallel after T1)
- **FINAL**: **4 tasks** — F1 → `oracle`, F2 → `unspecified-high`, F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [x] 1. Restructure MatchCard.tsx to two-row mobile layout

  **What to do**:
  - Restructure the main flex container (line 62) from a single horizontal row to a mobile-first two-row layout:
    - **Mobile (default, <640px)**: Two rows inside the card
      - **Row 1**: Time/stage badge (left) + team names with flags + score/dash (center-right, taking remaining space). Team names should NOT use `truncate` — use `break-words` as safety net instead.
      - **Row 2**: Action buttons area (points badge, prediction badge, Bet/Endre button, 👥 button) — right-aligned or centered
    - **Desktop (`sm:` 640px+)**: Restore current single-row horizontal layout using `sm:` prefixed classes
  - Use Tailwind's mobile-first approach: default classes = mobile layout, `sm:` prefixed = desktop layout
  - Remove `truncate` from team name spans on mobile. Add `sm:truncate` to keep desktop safety net
  - Keep `min-w-0` on team name containers at `sm:` for flex overflow safety
  - Preserve ALL existing conditional rendering logic unchanged (lines 77-87 for result/dash, lines 89-152 for action area)
  - Preserve `data-testid="match-card"` on root div
  - Preserve venue info line at bottom (lines 155-159)

  **Must NOT do**:
  - Change MatchCardProps interface
  - Change conditional rendering logic
  - Add new imports, hooks, or dependencies
  - Change colors, font sizes, or theming
  - Extract sub-components
  - Touch any other file

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: This is a responsive CSS/layout task requiring visual design sensibility for arranging UI elements across breakpoints
  - **Skills**: [`frontend-ui-ux`]
    - `frontend-ui-ux`: Needed for making layout decisions that look good on both mobile and desktop
  - **Skills Evaluated but Omitted**:
    - `playwright`: Not needed for implementation — only for QA task

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 1 — first task
  - **Blocks**: Tasks 2, 3
  - **Blocked By**: None (can start immediately)

  **References** (CRITICAL):

  **Pattern References** (existing code to follow):
  - `src/components/MatchCard.tsx:56-161` — The entire component. The main flex container starts at line 62 (`<div className="flex items-center gap-3">`). Team names are at lines 73-87. Action buttons at lines 89-152. Venue info at lines 155-159.
  - `src/components/MatchCard.tsx:59` — Root div with responsive padding pattern: `px-3 py-2 ... sm:px-4 sm:py-2.5` — follow this mobile-first convention
  - `src/components/MatchCard.tsx:73` — Current team names flex container: `flex min-w-0 flex-1 items-center justify-center gap-1.5 text-sm font-semibold sm:gap-2 sm:text-base`
  - `src/components/MatchCard.tsx:74,84` — Team name spans with truncate: `min-w-0 truncate text-right` / `min-w-0 truncate text-left`

  **API/Type References**:
  - `src/types/index.ts` — Match, Team, Venue types (MatchCardProps must stay identical)

  **Test References**:
  - `src/__tests__/MatchCard.test.tsx` — Existing tests check text content rendering. These MUST still pass. Review to understand what text must be in the DOM.

  **External References**:
  - Tailwind CSS responsive design: Use `sm:` prefix for 640px+ breakpoint (mobile-first approach)

  **WHY Each Reference Matters**:
  - Lines 62-87: This is the exact JSX structure to restructure. Understand the three-section layout (time | teams | actions) before changing it
  - Lines 74,84: These `truncate` classes are the ROOT CAUSE of the bug. They must be removed on mobile
  - Lines 89-152: The action area has complex conditional logic. It must be moved to Row 2 on mobile WITHOUT changing any conditions
  - Test file: Read it to ensure renamed/restructured elements still render the same text content

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Team names display fully on iPhone viewport
    Tool: Playwright
    Preconditions: Dev server running at http://localhost:5173, app loaded with match data
    Steps:
      1. Set viewport to 375×812 (iPhone standard)
      2. Navigate to http://localhost:5173
      3. Wait for match cards to render (wait for `[data-testid="match-card"]` to appear)
      4. Find a match card containing a long team name (search for text matching /Bosnia|Elfenbenskysten|Saudi-Arabia/)
      5. Assert the team name text is fully visible — check that the element's scrollWidth equals its clientWidth (no overflow/truncation)
      6. Screenshot the match card
    Expected Result: Team names display without truncation. Full text visible.
    Failure Indicators: Text is cut off with "..." ellipsis. scrollWidth > clientWidth on team name element.
    Evidence: .sisyphus/evidence/task-1-mobile-team-names.png

  Scenario: Desktop layout preserved at tablet width
    Tool: Playwright
    Preconditions: Dev server running at http://localhost:5173
    Steps:
      1. Set viewport to 768×1024
      2. Navigate to http://localhost:5173
      3. Wait for match cards to render
      4. Find a match card
      5. Assert the card's main content is in a single horizontal row (check that time, team names, and action buttons all have the same Y position / are vertically aligned)
      6. Screenshot the match card
    Expected Result: Horizontal single-row layout identical to current desktop design
    Failure Indicators: Elements are stacked vertically instead of horizontal. Action buttons on a separate row.
    Evidence: .sisyphus/evidence/task-1-desktop-layout.png

  Scenario: Dark mode renders correctly on mobile
    Tool: Playwright
    Preconditions: Dev server running at http://localhost:5173
    Steps:
      1. Set viewport to 375×812
      2. Navigate to http://localhost:5173
      3. Execute JS: document.documentElement.classList.add('dark')
      4. Wait 200ms for theme transition
      5. Screenshot a match card
      6. Assert text is visible (not same color as background)
    Expected Result: Card renders with correct dark theme colors, text is readable, no visual artifacts
    Failure Indicators: White text on white background, missing borders, invisible elements
    Evidence: .sisyphus/evidence/task-1-dark-mode-mobile.png

  Scenario: Match with result + points + prediction displays correctly on mobile
    Tool: Playwright
    Preconditions: Dev server running, a match exists with result, points, and prediction
    Steps:
      1. Set viewport to 375×812
      2. Navigate to http://localhost:5173
      3. Find a match card that shows a score result (e.g., "3 – 1"), points badge, and prediction badge
      4. Assert all three elements are visible and not overlapping
      5. Screenshot
    Expected Result: Score, points badge, and prediction badge all visible without overlap
    Failure Indicators: Elements overlap, badges hidden behind other elements, text unreadable
    Evidence: .sisyphus/evidence/task-1-full-result-mobile.png
  ```

  **Evidence to Capture:**
  - [ ] task-1-mobile-team-names.png
  - [ ] task-1-desktop-layout.png
  - [ ] task-1-dark-mode-mobile.png
  - [ ] task-1-full-result-mobile.png

  **Commit**: YES
  - Message: `fix(ui): use two-row stacked layout on mobile for MatchCard`
  - Files: `src/components/MatchCard.tsx`
  - Pre-commit: `npm test`

---

- [x] 2. Verify tests and type-check pass

  **What to do**:
  - Run `npm test` in project root and verify all tests pass with 0 failures
  - Run `npx tsc --noEmit` and verify 0 TypeScript errors
  - If any failures: report exact error messages. Do NOT fix — report back so Task 1 can be adjusted.

  **Must NOT do**:
  - Modify any files
  - Fix test failures (report only)
  - Skip any test

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Just running two commands and checking output
  - **Skills**: []
  - **Skills Evaluated but Omitted**:
    - All: No special skills needed for running test commands

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 3, after Task 1)
  - **Parallel Group**: Wave 1 — after Task 1 completes
  - **Blocks**: F1-F4
  - **Blocked By**: Task 1

  **References**:

  **Pattern References**:
  - `package.json:9` — Test script: `"test": "vitest run"`

  **Test References**:
  - `src/__tests__/` — All test files in this directory
  - `src/App.test.tsx` — App-level test

  **WHY Each Reference Matters**:
  - package.json test script tells you the exact command to run
  - Test directories tell you what coverage exists

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: All Vitest tests pass
    Tool: Bash
    Preconditions: Task 1 completed, MatchCard.tsx has been modified
    Steps:
      1. Run: npm test (in /Users/eekvang/dev/aiworkshop/worldcup)
      2. Capture full output
      3. Assert exit code is 0
      4. Assert output contains "Tests" with 0 failures
    Expected Result: All tests pass. Output shows "X passed, 0 failed" (or similar)
    Failure Indicators: Non-zero exit code, "FAIL" in output, any test failure message
    Evidence: .sisyphus/evidence/task-2-test-output.txt

  Scenario: TypeScript type-check passes
    Tool: Bash
    Preconditions: Task 1 completed
    Steps:
      1. Run: npx tsc --noEmit (in /Users/eekvang/dev/aiworkshop/worldcup)
      2. Capture output
      3. Assert exit code is 0
      4. Assert no error output
    Expected Result: Zero TypeScript errors. Clean exit.
    Failure Indicators: Non-zero exit code, "error TS" in output
    Evidence: .sisyphus/evidence/task-2-typecheck-output.txt
  ```

  **Evidence to Capture:**
  - [ ] task-2-test-output.txt
  - [ ] task-2-typecheck-output.txt

  **Commit**: NO (verification only)

---

- [x] 3. Visual QA via Playwright at multiple viewports

  **What to do**:
  - Start dev server if not running (`npm run dev`)
  - Use Playwright to verify the responsive layout at multiple viewport sizes
  - Capture screenshots as evidence for all scenarios
  - Test both light and dark themes on mobile
  - Test edge cases: knockout matches with placeholders, locked matches

  **Must NOT do**:
  - Modify any source files
  - Change test data
  - Make code fixes (report issues only)

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: Visual verification task requiring Playwright browser automation and screenshot analysis
  - **Skills**: [`playwright`]
    - `playwright`: Required for browser automation, viewport control, and screenshot capture
  - **Skills Evaluated but Omitted**:
    - `frontend-ui-ux`: Not needed — we're verifying, not designing

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 2, after Task 1)
  - **Parallel Group**: Wave 1 — after Task 1 completes
  - **Blocks**: F1-F4
  - **Blocked By**: Task 1

  **References**:

  **Pattern References**:
  - `src/components/MatchCard.tsx` — The modified component to verify
  - `src/data/teams.json` — Team names for identifying long-name matches

  **WHY Each Reference Matters**:
  - MatchCard.tsx: Need to understand the structure to know what to look for in the DOM
  - teams.json: Identify which teams have the longest names to target for truncation testing

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Mobile layout — two-row structure verified
    Tool: Playwright
    Preconditions: Dev server running at http://localhost:5173
    Steps:
      1. Set viewport to 375×812 (iPhone 13/14 standard)
      2. Navigate to http://localhost:5173
      3. Wait for `[data-testid="match-card"]` to be visible
      4. Take full-page screenshot
      5. Take close-up screenshot of first 3 match cards
      6. Verify: Row 1 contains time info and team names, Row 2 contains action buttons
    Expected Result: Match cards clearly show two-row layout. Team names fully visible. Action buttons on separate row.
    Failure Indicators: Single-row layout still showing, team names truncated, action buttons missing
    Evidence: .sisyphus/evidence/task-3-mobile-fullpage.png, .sisyphus/evidence/task-3-mobile-cards-closeup.png

  Scenario: Desktop layout — single-row preserved
    Tool: Playwright
    Preconditions: Dev server running
    Steps:
      1. Set viewport to 1024×768 (standard desktop)
      2. Navigate to http://localhost:5173
      3. Wait for match cards to render
      4. Take full-page screenshot
      5. Verify: All elements on single horizontal row per card
    Expected Result: Desktop layout unchanged — horizontal row with time, teams, score, buttons all inline
    Failure Indicators: Stacked layout appearing on desktop, elements wrapping to next line
    Evidence: .sisyphus/evidence/task-3-desktop-fullpage.png

  Scenario: Tablet breakpoint boundary (640px)
    Tool: Playwright
    Preconditions: Dev server running
    Steps:
      1. Set viewport to 639×1024 (just below sm: breakpoint)
      2. Navigate, wait for cards, screenshot
      3. Assert: mobile stacked layout showing
      4. Set viewport to 641×1024 (just above sm: breakpoint)
      5. Navigate, wait for cards, screenshot
      6. Assert: desktop horizontal layout showing
    Expected Result: Clean transition at exactly 640px breakpoint
    Failure Indicators: Wrong layout at either viewport, mixed layout elements
    Evidence: .sisyphus/evidence/task-3-breakpoint-639.png, .sisyphus/evidence/task-3-breakpoint-641.png

  Scenario: Knockout match with placeholder text on mobile
    Tool: Playwright
    Preconditions: Dev server running, navigate to knockout stage tab
    Steps:
      1. Set viewport to 375×812
      2. Navigate to http://localhost:5173
      3. Click on a knockout stage tab (e.g., "32-delsfinale" or "8-delsfinale")
      4. Wait for match cards with placeholder text like "1st Group A" or "Vinner gruppe A"
      5. Screenshot a card with placeholder text
      6. Assert placeholder text is fully visible
    Expected Result: Placeholder text displays correctly in stacked layout. No layout breaking.
    Failure Indicators: Placeholder text truncated, layout broken, empty space where team name should be
    Evidence: .sisyphus/evidence/task-3-knockout-placeholder-mobile.png

  Scenario: Dark mode on mobile
    Tool: Playwright
    Preconditions: Dev server running
    Steps:
      1. Set viewport to 375×812
      2. Navigate to http://localhost:5173
      3. Find and click the theme toggle button (ThemeToggle component in header)
      4. Wait 300ms for theme transition
      5. Screenshot match cards in dark mode
      6. Assert: text is readable, borders visible, badges have correct contrast
    Expected Result: Dark mode renders correctly with stacked layout. All text readable.
    Failure Indicators: White text on white bg, missing elements, invisible borders
    Evidence: .sisyphus/evidence/task-3-dark-mode-mobile.png
  ```

  **Evidence to Capture:**
  - [ ] task-3-mobile-fullpage.png
  - [ ] task-3-mobile-cards-closeup.png
  - [ ] task-3-desktop-fullpage.png
  - [ ] task-3-breakpoint-639.png
  - [ ] task-3-breakpoint-641.png
  - [ ] task-3-knockout-placeholder-mobile.png
  - [ ] task-3-dark-mode-mobile.png

  **Commit**: NO (verification only)

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Present consolidated results to user and get explicit "okay" before completing.
>
> **Do NOT auto-proceed after verification. Wait for user's explicit approval before marking work complete.**

- [x] F1. **Plan Compliance Audit** — `oracle` (APPROVE — rejection was false positive: flagged pre-existing window.matchMedia in ThemeToggle.tsx, not a new addition)
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read MatchCard.tsx, check for responsive classes). For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in .sisyphus/evidence/. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [x] F2. **Code Quality Review** — `unspecified-high` (APPROVE)
  Run `npx tsc --noEmit` + `npm test`. Review MatchCard.tsx for: unused classes, inconsistent responsive patterns, missing `sm:` counterparts, broken conditional rendering. Check for AI slop: unnecessary wrapper divs, over-commented code, dead code.
  Output: `Build [PASS/FAIL] | Tests [N pass/N fail] | Code Quality [CLEAN/N issues] | VERDICT`

- [x] F3. **Real Manual QA** — `unspecified-high` (+ `playwright` skill) (APPROVE)
  Start from clean state. Execute EVERY QA scenario from Tasks 1 and 3 — follow exact steps, capture evidence. Test cross-task integration. Test edge cases: iPhone SE width (320px), landscape orientation. Save to `.sisyphus/evidence/final-qa/`.
  Output: `Scenarios [N/N pass] | Edge Cases [N tested] | VERDICT`

- [x] F4. **Scope Fidelity Check** — `deep` (APPROVE)
  Run `git diff` on all changed files. Verify ONLY `src/components/MatchCard.tsx` was modified. Check that no other files were touched. Verify the diff only contains layout/CSS class changes, not logic changes. Flag any unaccounted changes.
  Output: `Files Changed [expected: 1] | Logic Changes [expected: 0] | Scope [CLEAN/BREACH] | VERDICT`

---

## Commit Strategy

| Task | Commit? | Message | Files | Pre-commit |
|------|---------|---------|-------|------------|
| 1 | YES | `fix(ui): use two-row stacked layout on mobile for MatchCard` | `src/components/MatchCard.tsx` | `npm test` |
| 2 | NO | — | — | — |
| 3 | NO | — | — | — |

---

## Success Criteria

### Verification Commands
```bash
npm test          # Expected: All tests pass, 0 failures
npx tsc --noEmit  # Expected: 0 errors
```

### Final Checklist
- [ ] "Bosnia-Hercegovina" fully visible at 375px viewport
- [ ] "Elfenbenskysten" fully visible at 375px viewport
- [ ] Desktop layout (768px+) unchanged from current
- [ ] Dark mode works on mobile
- [ ] Knockout placeholders display correctly on mobile
- [ ] All interactive buttons accessible on mobile
- [ ] Only `src/components/MatchCard.tsx` was modified
- [ ] All tests pass
- [ ] TypeScript compiles without errors
