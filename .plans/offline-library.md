# Feature: Offline personal library
Status: IN_PROGRESS
Issue: https://github.com/daniel-kindl/folium/issues/6
Branch: feat/offline-library

## Goal
A person can keep a personal library of books on the device with no network.

## Requirements and acceptance criteria
The issue requires all of these:

- A person can add, browse, edit, and remove a book with no internet access.
- The books remain after the app restarts. Removal behaves in a predictable way.
- Automated tests cover input rules and persistence.
- The UI is usable on an Android phone.

The owner set these rules in this session on 2026-11-05:

- A book has a title and an author only.
- Two books may share the same title and author.
- After confirmation, removal is permanent. There is no undo.

## In scope
- Manual add, list, details, edit, and remove.
- Local persistence across a new library instance.
- Empty, validation, and save or load error states.
- Confirmation before removal.
- Android Back moves through those screens.

## Out of scope
- Search, import, covers, and reading status.
- Accounts, sync, and other collection types.
- The theme, bundled fonts, and icon pack from issue #10.
- Undo.

## Dependencies and constraints
- No prerequisite issues.
- The app already uses Avalonia Fluent controls.
- The app targets Android 12 or later.
- Do not add a package unless a private file cannot meet the criteria.
- Claim: folium-6-037c9c5a-dc58-489e-b56d-76850a8ae0de

## Decisions and open questions
- Title and author are required. Folium trims them and rejects blank text.
- Duplicates are separate books. Identity is not the title or the author.
- Removal asks for confirmation, then deletes that book. A second removal of the same book fails.
- The store is a private JSON file in the app data directory. Book rules talk to a small store interface. There is one file implementation. This choice is easy to replace, so there is no ADR.
- A missing file is an empty library. A file Folium cannot read is an error. Folium does not replace that file with an empty library.
- The list keeps insertion order.
- Open questions: none.

## Commit plan

### C01: Save a book on the device
Status: COMPLETED
Depends on: none
Proposed commit: feat(library): save a book on device
Actual commit: 275b034
Objective: A person can add a book with a title and an author, and a new library instance reads it back.
Work: Failing tests first for trim, blank fields, duplicates, restart, and an unreadable file. Then the book rules, the file store, and the glossary terms.
Verification: `dotnet test tests/Folium.Tests` for the new tests. The placeholder test still passes.
Evidence: `dotnet test tests/Folium.Tests` passed on 2026-11-05. 12 tests passed and 0 failed. Format and the Release build are not run for this commit.

### C02: Edit and remove a book
Status: COMPLETED
Depends on: C01
Proposed commit: feat(library): edit and remove books
Actual commit: 3cdb640
Objective: A person can change the title and author, and can remove one book permanently.
Work: Tests first for edit, a missing book, and removal. Removal of a missing book fails. A failed write keeps the previous books.
Verification: `dotnet test tests/Folium.Tests`
Evidence: `dotnet test tests/Folium.Tests` passed on 2026-11-05. 19 tests passed and 0 failed. Format and the Release build are not run for this commit.

### C03: Show the library on screen
Status: COMPLETED
Depends on: C02
Proposed commit: feat(library): show books on screen
Actual commit: 9d9a9ef
Objective: The app shows the library, editor, details, confirmation, empty state, and errors. Android Back moves through those screens.
Work: View model tests first. Then the view model, views, and activity back handling.
Verification: `dotnet test tests/Folium.Tests`. Format and Release build when the code is complete.
Evidence: `dotnet test tests/Folium.Tests` passed on 2026-11-05. 35 tests passed and 0 failed. `dotnet format` completed with exit code 0. The Release build and the Android build are not run yet.

### C04: Document the personal library
Status: IN_PROGRESS
Depends on: C03
Proposed commit: docs: describe the personal library
Actual commit: none
Objective: The README and changelog match the behavior.
Work: Update the README and CHANGELOG. Do not document an unbuilt design system.
Verification: The text matches the code. No new behavior.
Evidence: not yet run

## Final verification
- `dotnet format --verify-no-changes`
- `dotnet test`
- `dotnet build` in Release
- Device check if a device is connected. If not, record NOT RUN.

## Handoff
Next action: Commit the README and changelog, then run the required checks.
Blockers: none
Risks: Android Back and the app data path need a device to prove. Automated tests do not start the Android UI.
