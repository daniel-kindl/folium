# Feature: Import books
Status: IN_PROGRESS
Issue: https://github.com/daniel-kindl/folium/issues/8
Branch: feat/8-import-books
Claim-ID: 090a38b1-1ffe-45fa-ad66-377cdb3fca6c

## Goal
Review an Open Library edition, edit its metadata, and save a book for offline use.

## Requirements and acceptance criteria
- Search, select, review, and save available metadata and an optional cover.
- Edit imported metadata and retain it after restart without network access.
- Missing optional metadata or a failed cover request does not prevent saving.
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
Status: IN_PROGRESS
Depends on: C01
Proposed commit: feat(import): review and save editions
Actual commit: none
Objective: Connect edition preview to the editor and offline details.
Work: Failing navigation and mapping tests, then draft mapping and UI.
Verification: Headless UI and view model tests for save, cancel, missing data and covers.
Evidence: not yet run

### C03: Document and verify import
Status: PENDING
Depends on: C02
Proposed commit: docs: describe offline imports and cover rights
Actual commit: none
Objective: Document attribution, rights, and verification.
Work: Documentation, required checks and independent review.
Verification: Format, build, tests, coverage, mutation and CRAP gates.
Evidence: not yet run

## Final verification
NOT RUN for feature changes.

## Handoff
Next action: Verify the decoder fallback, then run the required gates.
Blockers: none.
Risks: Cover rights must not be inferred from metadata licensing. Android usability requires a device check.
