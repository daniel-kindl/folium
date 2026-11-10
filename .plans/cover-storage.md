# Feature: Separate saved cover storage
Status: IN_PROGRESS
Issue: https://github.com/daniel-kindl/folium/issues/23
Branch: perf/23-cover-storage
Claim-ID: adb58147-910d-4a7b-832d-111f8fdd6896

## Goal
Reduce metadata write amplification without losing saved books or covers.

## Requirements and acceptance criteria
- Measure saves for 0, 100, and 1,000 books before storage changes.
- Keep approved covers in private files with stable metadata references.
- Preserve book IDs, edits, statuses, source attribution, and offline behavior.
- Preserve valid data after failed saves, interrupted migration, and disk exhaustion.
- Handle missing or corrupt covers, replacement, deletion, and orphan cleanup.
- Prove migration, recovery, corruption handling, and Android offline restart.
- Preserve the portable backup contract and all existing quality gates.

## In scope
Deterministic benchmark, local storage format, migration, recovery, tests, and documentation.

## Out of scope
Database replacement, downloads, network calls, accounts, telemetry, and backup format changes.

## Dependencies and constraints
Backup issue #22 is closed as completed. The repository contains LibraryBackup version 1.
That format stores embedded cover bytes independently of the internal library layout.
Keep its 32 MiB limit and cover round-trip behavior unchanged.
No native blocker links were present when this session claimed #23.

## Decisions and open questions
Measure the existing FileBookStore before changing production code.
The benchmark uses deterministic synthetic byte payloads, not decodable images.
The payload sizes model bounded cover storage. They do not measure image decoding.
Compare immutable content-addressed files with per-book immutable files after the baseline.
Prefer metadata replacement as the transaction commit point, after all required files exist.
Keep legacy data available until migration commits. Specify rollback before implementation.
Define cleanup roots to include every retained recovery document before deleting any files.
Do not silently discard a missing or corrupt cover when a later metadata edit is saved.
Time measurements depend on the host and its page cache. They are not Android latency evidence.

## Commit plan

### C01: Measure the current store
Status: COMPLETED
Depends on: none
Proposed commit: test(storage): measure cover write costs
Actual commit: 0e0513d
Objective: Record repeatable baseline costs and a justified target before production changes.
Work: Exercise the real store with fixed IDs, metadata, and cover payloads.
Verification: Release build with strict analyzers, run all six fixtures, verify reopened book counts, and check formatting.
Evidence: Two Release runs passed all six fixtures. JSON sizes matched across runs. Strict analyzers, formatting, and diff whitespace checks passed. Baseline results and the 95% write-reduction target are in docs/performance/cover-storage.md.
Risks: Synthetic bytes model size only. The OS can buffer writes.

### C02: Separate covers and migrate safely
Status: PENDING
Depends on: C01
Proposed commit: perf(storage): separate saved cover files
Actual commit: none
Objective: Commit metadata without rewriting unchanged covers and preserve recoverable data on failure.
Work: Write failing behavioral tests before implementation. Define version 2 and its recovery invariants.
Verification: Legacy round-trip, backup round-trip, interrupted writes and migration, disk exhaustion, missing and corrupt files, replacement, deletion, and safe cleanup.
Evidence: Not run.
Risks: Cleanup can destroy recovery data if it ignores retained document references.

### C03: Verify performance and Android recovery
Status: PENDING
Depends on: C02
Proposed commit: docs(storage): record migration evidence
Actual commit: none
Objective: Prove write reduction, portability, and offline restart behavior.
Work: Repeat the baseline, document space and rollback costs, and exercise isolated Android storage.
Verification: Existing format, build, test, coverage, mutation, CRAP, design gates, and Android offline restart checks.
Evidence: Not run.
Risks: Desktop measurements cannot establish Android power-loss durability.

## Final verification
Not run. Production storage is unchanged.

## Handoff
Next action: Compare file layouts and define migration and recovery behavior before the first failing C02 test.
Blockers: None established.
Risks: Migration and cleanup are not yet implemented or verified.
