# Feature: Import books
Status: BLOCKED
Issue: https://github.com/daniel-kindl/folium/issues/8
PR: https://github.com/daniel-kindl/folium/pull/17
Branch: feat/8-import-books
Claim-ID: 090a38b1-1ffe-45fa-ad66-377cdb3fca6c

## Goal
Review an Open Library edition, edit its metadata, and save a book for offline use.

## Requirements and acceptance criteria
- Search, select, review, and save available metadata and an optional cover.
- Edit imported metadata and retain it after restart without network access.
- Missing optional metadata or a failed cover request does not prevent saving.
- Missing or undecodable covers show a fallback.
- Test mapping and persistence deterministically. Document sources and cover rights.

## In scope
Extend the existing book editor and atomic JSON store. Keep source identifiers.

## Out of scope
Bulk synchronization, multiple providers, cloud storage, automatic remote overwrites.

## Dependencies and constraints
Local library #6 shipped in PR #13. Search #7 shipped in PR #16.
No active native blockers. Initial tests: 134 passed.
Tracker preflight passed with no missing capabilities or labels.

## Decisions and open questions
Use optional book metadata to retain compatibility with existing library files.
Store optional cover bytes in the same atomic document to avoid a second transaction.
Keep search read-only. The library owns import and the editable draft.
Each import starts with cover storage disabled. The user must confirm permission.
Existing title and author requirements remain. The user can complete missing text.

## Commit plan
### C01: Persist imported metadata
Status: COMPLETED
Depends on: none
Proposed commit: feat(books): persist imported metadata
Actual commit: cf0e74d
Objective: Save metadata and cover together and retain old library compatibility.
Work: Failing persistence and edit tests, then optional metadata in book and store.
Verification: Domain and storage tests, including restart and write failures.
Evidence: 141 tests passed, including metadata restart and editing tests.

### C02: Review and edit an import
Status: COMPLETED
Depends on: C01
Proposed commit: feat(import): review and save editions
Actual commit: 5774fbc
Objective: Connect edition preview to the editor and offline details.
Work: Failing navigation and mapping tests, then draft mapping and UI.
Verification: Headless UI and view model tests for save, cancel, missing data and covers.
Evidence: 143 tests passed. The cover fallback test failed before the fix and passed after it.

### C03: Document and verify import
Status: BLOCKED
Depends on: C02
Proposed commit: docs: describe offline imports and cover rights
Actual commit: 1365f03
Objective: Document attribution, rights, and verification.
Work: Documentation, required checks and independent review.
Verification: Format, build, tests, coverage, mutation and CRAP gates.
Evidence: All local automated gates passed. Human Android usability remains NOT RUN.

## Final verification
- 143 tests passed. Line coverage 94.62%, branch coverage 87.90%.
- Mutation testing passed at 85.11%. The report retains surviving and uncovered mutants.
- CRAP passed with maximum 8.0 after separating editor cancellation from screen navigation.
- Locked restore and high/critical vulnerability audit passed.
- Final Android Release build passed with no warnings or errors.
- Final format verification passed.
- Android usability: NOT RUN. No device is connected. A human check is a merge requirement.
- The headless decoder accepts arbitrary bytes. The fallback test reproduces its failure result
  by clearing the image source. Native decoder failure is not verified on Android.
- Local test coverage needs TESTINGPLATFORM_PIPE_DIRECTORY=/tmp because the default path
  exceeds the Unix socket limit. The CRAP tool runs with DOTNET_ROLL_FORWARD=Major because
  its .NET 8 runtime is absent and this environment has .NET 10.

## Handoff
Next action: Complete independent review of draft PR #17 and obtain human Android usability evidence.
Keep this plan while the required human check is incomplete.
Blockers: A human must verify Android usability before merge.
Risks: Cover rights must not be inferred from metadata licensing. Android usability requires a device check.
