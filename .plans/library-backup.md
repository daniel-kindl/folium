# Feature: Personal library backup
Status: IN_PROGRESS
Issue: https://github.com/daniel-kindl/folium/issues/22
Branch: feat/library-backup
Claim-ID: 455d2a71-d41e-4d47-b34a-234ca959fbc7

## Goal

A person can export the personal library to a file they choose and restore that file later. Restore replaces the saved books only after confirmation. A failed or canceled restore leaves the current library unchanged.

## Requirements and acceptance criteria

- Export a versioned backup of every saved book, reading status, metadata, source identifiers, and saved cover bytes.
- Import a selected backup. Validate the schema, version, and size before any write.
- Reject a corrupt file, an unsupported version, a truncated file, a file over the size limit, a permission failure, a cancellation, and a failed write. The existing library stays unchanged.
- Version 1 restore is a replace. The confirmation shows the book count and the titles. There is no merge.
- A round trip after relaunch keeps distinct book identifiers, including two books with the same title and author, and keeps optional covers.
- The backup file is distinct from app-private `library.json`.
- The backup works offline. Folium does not upload it. The manifest gains no storage permission.
- Document privacy, the size limit, and which backup version this app reads.

## In scope

- A backup document and tests.
- Replace confirmation in the library screen.
- An Android document picker for the file the person selects.
- README and glossary text for the backup.

## Out of scope

- Cloud sync, scheduled backups, merge, and public cover sharing.
- Reading a raw `library.json` file as a backup.

## Dependencies and constraints

- No prerequisite issues.
- No new package and no new network call.
- The live store remains the atomic writer for `library.json`.
- Backup version 1 uses the same book fields as library file version 1.
- The maximum backup size is 32 MiB of UTF-8 bytes.
- Quality gates stay at the current CI thresholds.

## Decisions and open questions

- The backup `kind` is `personal-library-backup` and the backup `version` is 1. A file without that kind is rejected, so a live library file is not a backup.
- The person distinguishes the files by that kind, by the suggested name `Folium backup.json`, and by the picker location. Export does not write `library.json`.
- Saved cover bytes are the bytes already stored after the permission checkbox. Restore keeps those bytes. It does not ask for permission again.
- 32 MiB is the v1 size limit. A larger personal library cannot be exported or restored in this version.
- No open questions.

## Commit plan

### C01: Read and export a backup document
Status: PENDING
Depends on: none
Proposed commit: feat(storage): add library backup
Actual commit: none
Objective: A backup round-trips books, statuses, metadata, source identifiers, and covers. Bad files do not produce books.
Work: Add `LibraryBackup` and tests for a valid file, duplicate identifiers, covers, malformed JSON, the wrong kind, an unsupported version, a truncated file, and the 32 MiB limit.
Verification: `dotnet test` for `LibraryBackupTests`. The test must fail before the production type exists.
Evidence: not yet run

### C02: Replace the library only after confirmation
Status: PENDING
Depends on: C01
Proposed commit: feat(library): restore a backup
Actual commit: none
Objective: The library screen can preview a backup and replace the saved books. Cancel, invalid text, and a failed write leave the stored books in place.
Work: Add replace-all on the personal library and preview, confirm, and cancel on the view model.
Verification: View-model tests for confirm, cancel, invalid text, and a failed write.
Evidence: not yet run

### C03: Choose the backup file on Android
Status: PENDING
Depends on: C02
Proposed commit: feat(android): pick a backup file
Actual commit: none
Objective: Export and restore use the document picker. The screen shows the book count and titles before replace. The manifest stays limited to INTERNET.
Work: Add the library actions, the confirmation screen, and the picker. Keep picker failures off the live file.
Verification: Headless or markup checks for the actions and confirmation. Android picker check on a device when one is connected.
Evidence: not yet run

### C04: Document backup privacy and limits
Status: PENDING
Depends on: C01
Proposed commit: docs: describe library backups
Actual commit: none
Objective: The README and glossary state what the file contains, the 32 MiB limit, version 1, and that Folium does not upload the file.
Work: Update README and CONTEXT.md.
Verification: Read the edited sections against the issue acceptance text.
Evidence: not yet run

## Final verification

- `dotnet format --verify-no-changes`
- Release build and tests
- Coverage at or above 80% lines and 70% branches
- Mutation score at or above 70% for Books and Storage
- Method CRAP at or below 8
- Android picker check when a device is connected

## Handoff

Next action: Write the failing backup round-trip test.
Blockers: none
Risks: The Android document picker still needs a device check. A personal library larger than 32 MiB cannot be backed up in this version.
