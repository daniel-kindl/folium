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
The current store writes that entire JSON document to a temporary file for each save.
Thus the field gives logical bytes written for the final metadata save.
The intermediate saves of the finished status write one additional byte.
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
Migration, cleanup, corruption handling, and Android restart checks remain pending.
