# Feature: Open Library search
Status: BLOCKED
Issue: https://github.com/daniel-kindl/folium/issues/7
Branch: feat/open-library-search
Claim-ID: folium-7-c819264b-934f-4f16-89b5-33dace056cf2

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

## Final verification
Automated checks passed. Independent source checks found no remaining material
issues. This is self-review preparation, not approval of the pull request.
The Android device check remains required before merge.

## Handoff
Next action: Check search, editions, covers, cancellation, and Back on an Android
phone or emulator. Record the result, complete this plan, and remove it before
requesting final review of the pull request.
Blockers: The required Android usability check has no available device.
Risks: Live provider fields can be absent. Device verification requires an available device.
