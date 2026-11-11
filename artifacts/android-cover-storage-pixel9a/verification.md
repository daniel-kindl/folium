# Pixel 9a cover storage verification

Device: Pixel 9a, API 37, arm64-v8a. Production code commit: 484ea98.
APK SHA-256 and device metadata are in `environment.json`.
The run used `FoliumVerification=true` with `verification-library.json`.
The user's existing `library.json` was never replaced by the test.
The original installed APK was saved before installation and restored afterward.
The original library hash matched after restoration.

## Result

1. Seeded `legacy.json`, which contains one synthetic version 1 book and the repository cover fixture.
2. Opened the book and visually checked its native cover and source identifiers in `02-legacy-cover.png`.
3. Selected Reading. The live document became version 2 and its cover reference matched the fixture SHA-256.
4. Checked the extracted cover bytes and exact version 1 rollback copy against the seeded fixture.
5. Force-stopped and relaunched the app in provider-offline mode. The saved cover remained visible in `04-offline-restart-cover.png`.
6. Selected Finished. The cover file bytes and modification timestamp remained unchanged.
7. Force-stopped and relaunched again. The book retained Finished and its cover remained visible in `05-second-restart-cover.png`.
8. Deleted only the isolated fixture cover. Restart showed the load-error recovery action in `06-missing-cover-recovery.png`.
9. Used Restore backup and the Android Downloads picker to select `portable-backup.json`.
10. Checked the explicit confirmation in `07-recovery-confirmation.png`, then selected Replace library.
11. Checked the restored status, source IDs, rollback copy, and exact cover bytes. The native cover is visible in `08-recovered-cover.png`.
12. Restored the original APK and checked the original library hash. Removed the synthetic Downloads file.

`device-library.json` records the checked fixture state before deliberate cover removal.
All committed data and screenshots are synthetic fixture evidence.

## Execution method and limits

The accessibility runner did not pass. Android UiTestAutomationBridge returned a null root node.
The previous runner silently read an old XML dump after this error.
It now removes the previous dump and rejects an error before reading XML.
The same null-root failure occurred with the launcher in the foreground.
No accessibility service or system preference was changed.

The successful run used screenshot inspection, direct ADB taps, and file assertions.
For this 1080 by 2424 device configuration, it opened the book at (540,1510).
It scrolled with `input swipe 540 1780 540 500 450`.
It selected Reading at (540,1620) and Finished at (540,1810).
The recovery action was at (540,520) and Replace library at (540,2015).
These coordinates are evidence for this configuration, not a portable UI selector.

`tools/android/verify_storage.py` contains the repeatable flow for a working accessibility bridge.
Build with `dotnet build src/Folium.Android -c Debug -p:FoliumVerification=true`.
Run that script with `--serial`, `--apk`, `--restore-apk`, and `--output`.
The restore APK must be the normal APK saved from the device before the test.

Provider-offline mode rejects provider requests without changing device connectivity.
This run did not exhaust the phone filesystem or test sudden power loss.
Linux automated tests cover failed writes at both temporary-file boundaries.
