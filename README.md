# Folium

Folium is an offline-first Android app for a personal book library. A person can add, browse, edit, and remove books on the device, and can mark a book as to read, reading, or finished. A person can also search Open Library by title and preview editions online.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), as specified by `global.json`.
- Android SDK and a supported JDK. Install Android Studio and its SDK components, or follow the [.NET for Android setup instructions](https://learn.microsoft.com/dotnet/android/getting-started/installation/). The Android SDK command-line tools and `adb` should be available on your path. Set `ANDROID_HOME` and `JAVA_HOME` if the tooling cannot locate them.
- An Android 12 (API 31) or later emulator or device with USB debugging enabled for deployment.

From the repository root, install the required .NET workload and restore dependencies:

```sh
dotnet workload restore
dotnet restore --locked-mode
```

## Build and test

```sh
dotnet format --verify-no-changes --no-restore
dotnet build Folium.slnx --configuration Release --no-restore
dotnet test Folium.slnx --configuration Release --no-build --no-restore
```

The solution build includes the Android application. CI also checks the complete dependency graph for high and critical NuGet advisories.

## Deploy to Android

Start an emulator, or connect a device and authorize USB debugging. Confirm that `adb devices` lists it, then run:

```sh
dotnet run --project src/Folium.Android
```

The development build uses a debug signing key. See [Avalonia Android deployment](https://docs.avaloniaui.net/docs/deployment/android) for device setup and release signing.

### Physical device verification

On 2026-11-03, a USB-connected Pixel 9a ran the debug application with identity
`dev.danielkindl.folium`. The centered Folium placeholder rendered after launch
and after returning from the Home screen. The Android build passed with no
warnings or errors, and the existing automated test passed.

The initial launch crashed because the activity used a platform Material theme.
Avalonia's activity requires an AppCompat theme. `Theme.AppCompat.NoActionBar`
fixed that crash. Orientation behavior was not checked in this smoke test.

To repeat the device check, enable USB debugging and authorize the computer.
Build the Android project, then install and launch the signed debug APK:

```sh
dotnet build src/Folium.Android
adb -s DEVICE_SERIAL install -r src/Folium.Android/bin/Debug/net10.0-android/dev.danielkindl.folium-Signed.apk
adb -s DEVICE_SERIAL shell monkey -p dev.danielkindl.folium -c android.intent.category.LAUNCHER 1
```

Replace `DEVICE_SERIAL` with the serial from `adb devices`. Check the placeholder,
press Home, and open Folium again to check resume.

## Structure

- `src/Folium`: shared Avalonia UI, book rules, and the private library file.
- `src/Folium.Android`: Android application host.
- `tests/Folium.Tests`: automated tests.

A book has a title, an author, and a reading status. Folium stores the personal library in a private JSON file. On Android, that file is `library.json` in the app files directory. See [CONTEXT.md](CONTEXT.md) for the domain terms. Search and preview do not save books. Choose **Review and save** to import an edition into the personal library.

In the Library screen, search checks saved titles and authors. It trims the query, uses Unicode Form D normalization, removes combining marks, and compares without case. An empty query leaves the status filter in effect. A query that contains only combining marks is also empty. The default order is title A-Z. Sort uses that same folded text, then the original text, then the local book ID in ascending order. You can also sort by title or author in either direction. Letters that do not decompose stay distinct. Examples are ß, ø, Æ, Ł, and the ﬁ ligature. Search and sorting run on the device and do not change saved records. The search and sort controls stay hidden when the library has no books.

See [AGENTS.md](AGENTS.md) for development instructions. Use [Conventional Commits and short-lived branches](https://raw.githubusercontent.com/daniel-kindl/dkstack/v1/version-control/index.md). Versions follow [Semantic Versioning](https://raw.githubusercontent.com/daniel-kindl/dkstack/v1/versioning/index.md). Changes require independent pull-request review.

## License

[MIT](LICENSE)

## Search Open Library

Choose **Find a book online**, enter a title, then select **Search Open Library**.
Select a work to browse its editions. Publisher, date, language, ISBN and edition
identifiers help distinguish editions. Select an edition to inspect its metadata.
Use **Preview cover** on a result to load an available cover. Empty metadata fields
mean Open Library did not supply the value.

Search requires a network connection. You can cancel a pending request or use Back.
Failures leave the title available for another attempt. Nothing in search changes
the saved library.

The client uses the [Open Library APIs](https://openlibrary.org/developers/api) and
[covers service](https://openlibrary.org/dev/docs/api/covers). Requests start after
user actions, run at most once per second, and identify Folium by its repository
URL. Successful responses stay in a bounded session cache. The client honors
Retry-After and does not retry automatically. Result pages contain at most 20
works or editions. No API key or backend is required.

## Import a book

Open an edition preview and choose **Review and save**. Review the title, author,
publisher, publication date, ISBN, edition statement, language, and description. You can
change these fields before saving. Supply a title and author if either is missing.
Other metadata fields can stay empty. Cancel returns to the preview without saving.

A downloaded cover is optional. Select **I have permission to save this cover on
my device** only when the rights holder or an applicable license permits that private copy. The box starts
unchecked for each import. Missing covers and cover download failures do not
prevent saving. Without a saved image, book details show **No saved cover**.

Saved details and covers work offline after restart. Use **Edit** to change the
metadata or remove a saved cover. Local changes never trigger a remote refresh.
The original Open Library work path, edition path, and cover identifier stay with
the book. The ISBN field remains editable. Saving another import of the same edition creates a separate book with its own
local identifier.

### Attribution and cover rights

Metadata and cover previews come from [Open Library](https://openlibrary.org/).
Each imported book retains its source paths. The source edition page is
`https://openlibrary.org` followed by the saved edition path.

[Open Library's licensing statement](https://openlibrary.org/developers/licensing)
does not assert new proprietary rights over database material. It also warns that
existing rights can remain. Availability through the API does not establish an
open license for each cover. Folium does not determine per-image rights. The
checkbox controls whether Folium retains the image. Folium does not store a
separate permission record. The checkbox does not grant permission or change copyright.

Folium uses the [Covers API](https://openlibrary.org/dev/docs/api/covers) for a
selected edition. It does not crawl or download covers in bulk. Private saved
covers are not uploaded, published, or synchronized.

### Storage and compatibility

The private `library.json` uses live storage version 2. It holds book metadata
and SHA-256 references to optional files in `library.json.covers`.
An unchanged cover file is not rewritten when you edit a title or reading status.
Identical cover bytes share one file. Intact files that cannot be decoded show **No saved cover**.

Version 1 libraries remain readable. The first successful save migrates them.
Before migration, Folium keeps an exact copy at `library.json.v1.bak`.
It writes and flushes covers before atomically replacing the metadata document.
An interrupted save leaves the previous document available. Uncommitted cover files
and temporary cover files are removed after a later successful save.
Deleting or replacing a cover removes files that the committed library no longer references.
Cleanup failures leave unused files for a later save to remove.

A missing cover file or a SHA-256 mismatch prevents opening the library.
Folium keeps the saved files unchanged and shows **Restore backup**.
Choose a valid personal backup, review the titles, then confirm **Replace library**.
Canceling the confirmation keeps the current files unchanged.
Restoring a backup replaces the library, including any edits made after that backup.
The recovery action does not repair malformed JSON or an unsupported live version.

Older app versions cannot read live storage version 2.
Before downgrading, export a personal backup with the current app.
An older app may not support that portable backup format either.
For an exact pre-migration rollback, stop the app and retain the whole private directory.
Replace `library.json` with `library.json.v1.bak` before installing the older app.
This restores the pre-migration state and discards later edits from the active library.
The rollback file remains until you deliberately remove it after verifying a newer backup.
It can retain covers that you later delete from the active library.

Migration needs free space for both the old embedded covers and the extracted files.
Flushing files and atomically replacing metadata protects against interrupted writes.
Sudden power loss and filesystem failures can exceed these guarantees.
See [storage measurements and recovery limits](docs/performance/cover-storage.md).

### Personal library backup

On the Library screen, **Export backup** writes the saved books to a file you choose.
The picker suggests `Folium backup.json`.
The live library stays in the private `library.json` file.

The backup kind is `personal-library-backup` and the version is 1.
The file holds each book's reading status, metadata, source identifiers, and saved cover.
Folium does not upload the backup.

This app reads version 1 backups of 32 MiB or less.
A larger library cannot be exported or restored in this version.
Folium rejects a file that is not a version 1 backup.
A raw `library.json` file is one of those files.

**Restore backup** shows the book count and the titles.
**Replace library** then replaces the books on this device.
**Keep current library** leaves the saved books in place.
A bad file, a denied permission, a cancellation, or a failed write leaves the saved books unchanged.
A failed export can leave the selected file incomplete.
The library on the device stays unchanged.

The backup contains the reading history and any saved covers.
Store the file in a place that stays private.
