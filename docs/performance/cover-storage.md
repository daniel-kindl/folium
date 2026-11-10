# Cover storage measurements

Run `dotnet run --project tools/StorageBenchmark -c Release` from the repository root.
The command uses a temporary directory and removes it after the run.
It does not open the user's library or make network requests.

## Baseline

The baseline uses the version 1 FileBookStore from commit 728a87b.
The measurements ran on Linux x64 with .NET SDK 10.0.403.
See [the CSV results](cover-storage-baseline.csv).

Each fixture has 0, 100, or 1,000 books. Each book has a unique fixed ID.
Each cover contains 32 KiB or 128 KiB of deterministic synthetic bytes.
SHA-256 produces the payload blocks from the book index and block offset.
These sizes model bounded saved covers. The payloads are not decodable images.
The benchmark does not measure rendering or image validation.

The command saves each fixture once before measurement.
It then measures five saves, with a reading-status edit before each save of a nonempty library.
The CSV reports median elapsed time and median managed allocations independently.
The process-wide allocation counter includes the stopwatch and concurrent runtime allocations.
The command reopens each saved library and checks its book count.
Fixture generation and reopening are outside the timed operation.

`library_bytes` is the resulting JSON size, not a device block-write counter.
The baseline version 1 store writes that entire JSON document to a temporary file for each save.
Thus the field gives logical bytes written for the final metadata save.
Samples two and four write one additional byte because `finished` is longer than `reading`.
The OS can buffer these writes. These results do not establish power-loss durability or Android latency.

With 1,000 books and 128 KiB per cover, a status edit writes 175,247,035 bytes.
The median save took 492.060 ms and allocated 350,733,656 managed bytes.
With 32 KiB per cover, it writes 44,175,035 bytes and took 125.100 ms.
An empty library writes 33 bytes.

## Target before implementation

For all four nonempty fixtures, reduce logical bytes written by at least 95% for a status edit.
After the initial save, write no unchanged cover files during metadata edits.
Record file writes as well as resulting document sizes when verifying that target.
The small metadata records make this target feasible without a database.
Repeat this command on the same host after implementation and report measured latency separately.
Do not use an elapsed-time threshold as a CI gate because shared-host timings vary.

## Storage and recovery trade-offs to verify

Raw cover files avoid base64 expansion, which adds approximately one third to cover size.
Individual files add file-system allocation and directory overhead.
Migration requires temporary free space for both embedded covers and extracted files.
A retained legacy recovery copy keeps the old base64 cost until deliberate cleanup.
The implementation must document its recovery retention and rollback procedure.

The portable backup remains version 1 with embedded covers and a 32 MiB limit.
Large benchmark fixtures can exceed that limit. They do not imply larger backup support.
## Version 2 results

See [the version 2 CSV](cover-storage-after.csv).
The command now also reports total raw cover bytes and the number of cover files whose modification time changed.
Before measured saves, it gives each cover file a fixed timestamp outside the timed operation.
All four nonempty fixtures retained their cover timestamps.
The storage tests also verify unchanged cover content, replacement, deduplication, and deletion.

For 1,000 books, the metadata document is 570,035 bytes at either cover size.
This reduces logical metadata writes by 98.71% for 32 KiB covers and 99.67% for 128 KiB covers.
Raw cover storage is 32,768,000 bytes or 131,072,000 bytes, respectively.
Including metadata, the latter fixture uses 131,642,035 logical bytes instead of 175,247,035.
These counts exclude file-system block allocation, directory entries, and any retained migration copy.

The recorded version 2 run measured median saves of 72.274 ms and 198.535 ms for those fixtures.
Both versions process all covers in memory during a save.
Version 2 reads existing cover bytes to check them before reuse.
Thus the change reduces writes but does not make save cost independent of the number of covers.
Version 2 explicitly flushes files before publication. The baseline did not do that.
Empty-library saves can consequently take longer despite writing the same 33 bytes.
The timings compare the observed implementations, not equal durability policies.

## Transaction and recovery rules

The application owns one writer to its private library path.
This format does not support concurrent writers from separate instances or processes.
Covers use lowercase SHA-256 filenames. The reader rejects other references before opening files.
The writer compares existing bytes before reuse and can repair damaged files from a valid backup.
It flushes each temporary cover before renaming it, then flushes and replaces the metadata document.
Only the metadata rename commits the new book list.
Cleanup starts afterward. The application ignores cleanup errors and reports the committed save as successful.

Version 1 migrates on the first save. Reads alone do not modify it.
The exact `library.json.v1.bak` is retained before any migration commit.
That backup embeds its own covers. Cleanup does not need to retain separate cover files for it.
It is never overwritten automatically. It can retain bytes removed from the active library.
After verifying a newer portable backup, the owner can remove the legacy copy deliberately.
The README gives the exact pre-migration rollback procedure and downgrade limits.

A missing file or hash mismatch stops the library from opening without changing the saved files.
The load-error screen offers explicit backup restore with the existing preview and replace confirmation.
A cancel or a failed save leaves the current metadata document unchanged.
The reader does not confuse intact but undecodable images with changed file bytes.
Intact undecodable images still use the existing UI fallback.

Tests exercise legacy migration, interrupted publication, both disk-full stages, damaged files,
backup repair, shared cover references, temporary files, and recovery through the view model.
The Linux disk-full tests use `/dev/full` at the temporary write boundary.
They prove write-error handling, not a complete Android filesystem exhaustion event.
File flushes do not prove directory-entry persistence after sudden power loss.
Android migration, two offline process restarts, unchanged cover writes, and picker recovery passed on the Pixel 9a.
See [device evidence](../../artifacts/android-cover-storage-pixel9a/verification.md).
The successful run used screenshots and direct ADB taps because the accessibility bridge failed.
