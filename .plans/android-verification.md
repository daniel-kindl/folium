# Feature: Android workflow verification
Status: IN_PROGRESS
Issue: https://github.com/daniel-kindl/folium/issues/18
Branch: test/18-android-verification
Claim-ID: 141bf108-00f1-4666-92b8-f56987dd1d45

## Goal
Exercise the real Android UI and retain reproducible execution evidence.

## Requirements and acceptance criteria
Cover launch, lifecycle, CRUD, search, import, Back, offline restart and native covers.
Preserve existing quality gates. Obtain independent review before merge.

## In scope
ADB accessibility runner, deterministic fixtures, isolated device storage, screenshots and logs.
Repair defects reproduced by the runner with regression tests.

## Out of scope
Visual redesign, dependency changes, release and production deployment.

## Dependencies and constraints
Connected Pixel 9a, API 37, serial 5B051JEBF00522.
The owner explicitly selected this device in chat instead of an emulator.
The phone contains an existing library. Never modify or publish its contents.

## Decisions and open questions
Use a verification build with a fixed separate library path and fixture HTTP responses.
Keep the package identity dev.danielkindl.folium. Exclude verification hooks from normal builds.
Record device results as device results. Do not claim emulator execution.

## Commit plan
### C01: Automate isolated Android verification
Status: IN_PROGRESS
Depends on: none
Proposed commit: test(android): add device workflow verification
Actual commit: none
Objective: Exercise the native UI with deterministic external data.
Work: ADB runner, compile-time verification host, fixture data and reproducible commands.
Verification: Run on the connected phone. Compare the original library hash before and after.
Evidence: Device connected. Existing app exposes Android accessibility nodes.

### C02: Repair confirmed workflow defects
Status: PENDING
Depends on: C01
Proposed commit: fix(android): repair verified workflow defects
Actual commit: none
Objective: Correct only defects established by actual device execution.
Work: Add failing regression tests before each production fix.
Verification: Focused regression tests and repeat affected device scenarios.
Evidence: Not yet run.

### C03: Record evidence and verify gates
Status: PENDING
Depends on: C01, C02
Proposed commit: docs(android): record device verification
Actual commit: none
Objective: Make results and limits reviewable.
Work: Matrix report, evidence artifacts, CI integration where practical and independent review.
Verification: Format, build, test, coverage, mutation, CRAP and design gates.
Evidence: Not yet run.

## Final verification
Not yet run.

## Handoff
Next action: Implement the isolated verification runner.
Blockers: None established.
Risks: API 37 does not establish API 31 or API 35 compatibility. Physical device testing replaces emulator execution only by the owner's chat instruction.
