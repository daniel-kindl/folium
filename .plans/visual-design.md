# Feature: Apply the approved visual design system
Status: IN_PROGRESS
Issue: https://github.com/daniel-kindl/folium/issues/20
Branch: feat/20-visual-design
Claim-ID: f64d335e-7abc-44d5-9e51-0305ebbc9545

## Goal
Apply the approved forest-and-paper design to the library, editor, details and online search views.

## Requirements and acceptance criteria
- Use shared semantic light and dark resources derived from `docs/design/tokens.json`, with dynamic resources for changing brushes.
- Bundle licensed Noto Sans and selected Lucide assets. Do not fetch assets at runtime.
- Use the defined typography, spacing, radii and touch bounds. Respect Android/system theme and retain view state across theme changes.
- Distinguish empty, loading, error, active-filter, button and focus states. Provide accessible names and responsive phone/landscape layouts.
- Add resource, AXAML, control-state, theme-switch, accessibility and layout checks. Review screenshot baselines deliberately.
- Record Android TalkBack, text scaling, reachability, light/dark, cover rendering, APK size and device evidence. State unrun checks.
- Preserve CRUD, status, search and import behavior. Preserve existing quality gates.

## In scope
Shared Avalonia theme resources and styles, bundled licensed visual assets, current screen layouts and behavior checks required by issue 20.

## Out of scope
Navigation rewrite, unrelated behavior, remote assets, new runtime dependencies, accounts, telemetry, backend, release and production deployment.

## Dependencies and constraints
- Approved visual research #10 and PR #12.
- Android verification issue #18 is closed and PR #19 is merged. Existing device workflow evidence is under `artifacts/android-device-api37-pixel9a`; do not imply it covers new visual checks.
- Keep FluentTheme as the control foundation. Keep Default as the initial system theme preference.
- Do not modify the phone's existing library. Use isolated verification storage and fixtures for device checks.
- `docs/design/README.md` prohibits a runtime token loader or theme service. `docs/research/visual-quality-gates.md` defines required automated and manual evidence.

## Decisions and open questions
- Use compile-time Avalonia resources based on the canonical token file; add validation that detects token/resource drift.
- Use Avalonia stroked paths and shapes from the selected Lucide SVGs. Avalonia supports the source stroke width, caps and joins. This preserves the source appearance without a runtime SVG parser. Keep the source files and full license notices. Record source revisions and hashes.
- No unresolved product questions. Measure the chosen responsive layout behavior and Android APK delta during implementation.

## Commit plan

### C01: Add canonical theme resources and resource checks
Status: COMPLETED
Depends on: none
Proposed commit: feat(ui): add semantic theme resources
Actual commit: 148ce59daf2fe87ab28698a4c9b87a3ff37b5de3
Objective: Make token-based light and dark semantic resources available to Avalonia.
Work: Add failing resource and AXAML checks, shared resources/styles, DynamicResource use, and a narrow color-literal guard.
Verification: Resource values match tokens; real AXAML loads and theme switching works in headless checks.
Evidence: The focused theme test passed (3 tests). Release app build and `dotnet format Folium.slnx --verify-no-changes --no-restore` passed on Linux. `git diff --check` passed.

### C02: Bundle approved fonts and icon roles
Status: COMPLETED
Depends on: C01
Proposed commit: feat(ui): bundle approved type and icons
Actual commit: 7e74335b7971002947389757ee37cf3c66983576
Objective: Use local Noto Sans and selected Lucide roles with complete attribution.
Work: Bundle licensed font faces, selected icon geometry, complete licenses and pinned source records. Add a deterministic geometry generator.
Verification: Check embedded assets, licenses, source hashes, glyph availability and absence of runtime downloads.
Evidence: Asset tests pass for embedded fonts, all icon roles, hashes and Czech, Greek and Cyrillic glyphs. The geometry check, Release app build, format, token and contrast checks pass on Linux. Android glyph rendering remains to run.

### C03: Apply tokens to library, editor and details
Status: COMPLETED
Depends on: C01, C02
Proposed commit: feat(ui): style library and book views
Objective: Apply approved typography, spacing, surfaces and control states to local library flows.
Work: Style library, filters, empty/no-match/error states, book rows, editor and details while retaining bindings and commands.
Verification: Headless AXAML and state checks cover CRUD/status paths, semantics, accessibility names, focus, narrow and landscape layouts.
Evidence: Headless checks cover shared typography, theme state, focus, accessible active-filter state, library touch bounds in portrait and landscape, CRUD, status, editor and details. Focused library, search, theme and asset tests pass (16 tests). Release build, format and diff checks pass.

### C04: Apply tokens to online search
Status: COMPLETED
Depends on: C01, C02
Proposed commit: feat(ui): style online search views
Objective: Apply the same visual system to search, results, editions, preview and loading/error states.
Work: Style current search layouts and controls without changing search behavior.
Verification: Existing search tests plus headless state, theme, accessibility and responsive layout checks pass.
Evidence: Existing headless search tests pass (12 tests), including search, paging, preview, import, cover and return flows. Release build and format pass. Android screenshot review remains for C05.

### C05: Review visual baselines and Android evidence
Status: IN_PROGRESS
Depends on: C03, C04
Proposed commit: test(ui): verify visual system on Android
Objective: Produce reviewable screenshot and real-device evidence for the issue's manual acceptance criteria.
Work: Capture deliberate light/dark library, search and details baselines; run isolated Android checks for TalkBack, scaling, reachability, themes, covers and landscape; record APK size delta.
Verification: Required project gates and automated visual checks pass. Record each manual check as passed, failed or not run with evidence.
Evidence: Automated headless portrait and landscape layout checks pass. No ADB device is visible in this session, so visual baselines and device accessibility checks have not run.

## Final verification
Run format, Release build, tests, coverage, mutation, CRAP, token and contrast checks. Run required Android checks on the connected device with isolated storage. Record any check that cannot run as NOT RUN.

## Handoff
Next action: Run screenshot and accessibility checks on the connected device when ADB can see it. Then run final quality gates and independent review.
Blockers: The claimed connected device is not visible to ADB in this session (`adb devices -l` returns no devices).
Risks: Full TalkBack and 200% scaling verification may need a separately installed accessibility setup; do not claim these checks without device evidence.
