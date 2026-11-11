# Physical device verification

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
