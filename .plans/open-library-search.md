# Feature: Open Library search
Status: BLOCKED
Issue: https://github.com/daniel-kindl/folium/issues/7
Branch: feat/open-library-search
Claim-ID: folium-7-8014629e-4df4-4c17-abd2-26722d4d2291

## Goal
Find a book by title and inspect a specific edition before saving anything.

## Requirements and acceptance criteria
Search titles, distinguish works and editions, preview available authors, ISBNs,
publisher, publication date, description and cover. Explain empty results, network
failures and cancellation. Test provider mapping with deterministic JSON fixtures.
Search must not write to the personal library.

## In scope
User-initiated work search, paged editions and edition preview. Open Library only.

## Out of scope
Import, saved metadata, backend, keys, scraping and bulk downloads.

## Dependencies and constraints
Use HttpClient and System.Text.Json without new production packages.
Use pinned Avalonia.Headless in tests to verify rendered controls and input. This
test-only dependency adds no Android package size. It uses Avalonia's MIT license
and matching version; a home-built dispatcher and renderer would be harder to maintain. Serialize requests at
one request per second. Identify Folium with its repository URL. Cache successful
metadata responses in bounded memory. Honor Retry-After without automatic retries.

## Decisions and open questions
Search works first, then list actual editions with publication context. Do not use
work-level ISBN or publisher aggregates as edition metadata. Keep provider access
and search state separate from PersonalLibrary. This adds an interface for tests;
an HTTP handler alone would not isolate view-state tests. No library API changes.
Load a cover only for a selected preview or an explicit result-cover request. Missing covers must not block metadata.
Use explicit next/previous pages, never background prefetch. No open questions.

## Commit plan
### C01: Search provider and mapping
Status: COMPLETED
Depends on: none
Proposed commit: feat(search): query Open Library metadata
Actual commit: cf45eb8
Objective: Isolate provider requests and map work and edition fixtures.
Work: Write failing fixture tests, then implement mapping, pacing and errors.
Verification: Deterministic tests with a fake HTTP handler.
Evidence: 129 tests passed in Release; deterministic provider fixtures passed.
The live Dune check returned works, editions, metadata and cover bytes.

### C02: Search and preview screens
Status: BLOCKED
Depends on: C01
Proposed commit: feat(search): add edition preview screens
Actual commit: 4305e1f
Objective: Add search, editions and preview without library writes.
Work: Write state tests first, then implement navigation, cancellation and UI.
Verification: State tests, full build, coverage, formatting and Android check.
Evidence: 129 Release tests passed, including real headless keyboard input.
Release coverage: lines 94.85%, branches 87.65%. The full Android release build
passed with no warnings or errors. Formatting verification and locked restore
with the high/critical dependency audit passed. Mutation score: 84.78%.
The pinned CRAP tool passed with a maximum method score of 8.0.
The tool ran on .NET 10 through runtime roll-forward; .NET 8 is not installed.
NOT RUN: Android device usability. No device is connected.

### C03: Review fixes
Status: COMPLETED
Depends on: C02
Proposed commit: fix(search): correct preview and paging
Actual commit: 7b972c6
Objective: Fix the pull request 16 review comments.
Work: Page from the raw provider count. Keep an edition when an author lookup fails. Show a repeated cover on the selected result. Name the page size and the cache limit.
Verification: Release tests, formatting verification, coverage, and the Android release build.
Evidence: 134 Release tests passed. Formatting verification passed.
Release coverage: lines 95.00%, branches 88.33%.
The Android release build passed with no warnings or errors.
NOT RUN: mutation score, CRAP, locked dependency audit, live Open Library call, and the Android device check.

### C04: CI repair
Status: COMPLETED
Depends on: C03
Proposed commit: refactor(search): simplify cover updates
Objective: Fix the failing CRAP check without changing cover behavior.
Work: Separate bitmap reuse and decoding from image placement.
Verification: Run the pinned CRAP tool and the existing headless tests.
Evidence: The local check reproduced the CRAP score of 11 before the change.
After the change, UpdateCover and CoverBitmap each score 6 with 100% coverage.
All 134 tests passed. Formatting verification passed. No quality gate changed.
The required Android device check remains pending.

## Final verification
Review fixes passed the Release tests, formatting verification, coverage, and the Android release build.
NOT RUN in this session: mutation, CRAP, the locked dependency audit, the live Open Library call, and the Android device check.
The Android device check remains required before merge.
The earlier session recorded mutation score 84.78%, a CRAP maximum of 8.0, a passed locked audit, and a live Dune check. Those checks were not repeated after the review fixes.

## Handoff
Next action: Check search, editions, covers, cancellation, and Back on an Android
phone or emulator. Record the result, complete this plan, and remove it before
requesting final review of the pull request.
Blockers: The required Android usability check has no available device.
Risks: Live provider fields can be absent. Device verification requires an available device.
