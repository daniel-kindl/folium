# Folium

Folium is an offline-first Android app for a personal book library. The initial app displays a placeholder; book search and library management are not implemented yet.

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

## Structure

- `src/Folium`: shared Avalonia UI and presentation logic.
- `src/Folium.Android`: Android application host.
- `tests/Folium.Tests`: automated tests.

Application logic, domain models, persistence, and external integration will be introduced when needed. See [AGENTS.md](AGENTS.md) for development instructions. Use [Conventional Commits and short-lived branches](https://raw.githubusercontent.com/daniel-kindl/dkstack/v1/version-control/index.md); versions follow [Semantic Versioning](https://raw.githubusercontent.com/daniel-kindl/dkstack/v1/versioning/index.md). Changes require independent pull-request review.

## License

[MIT](LICENSE)
