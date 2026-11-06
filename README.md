# Folium

Folium is an offline-first Android app for a personal book library. A person can add, browse, edit, and remove books on the device. Book search is not implemented yet.

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

A book has a title and an author. Folium stores the personal library in a private JSON file. On Android, that file is `library.json` in the app files directory. See [CONTEXT.md](CONTEXT.md) for the domain terms. Book search and import are not in the app yet.

See [AGENTS.md](AGENTS.md) for development instructions. Use [Conventional Commits and short-lived branches](https://raw.githubusercontent.com/daniel-kindl/dkstack/v1/version-control/index.md). Versions follow [Semantic Versioning](https://raw.githubusercontent.com/daniel-kindl/dkstack/v1/versioning/index.md). Changes require independent pull-request review.

## License

[MIT](LICENSE)
