using Android.Runtime;
using Avalonia.Android;

namespace Folium.Android;

/// <summary>The Android application host for Folium.</summary>
/// <param name="javaReference">The Java object handle from the Android runtime.</param>
/// <param name="transfer">How the runtime transfers ownership of the handle.</param>
[Application]
public class Application(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
    /// <summary>Sets the library file path after the Android application starts.</summary>
    /// <exception cref="InvalidOperationException">The application data directory is not available.</exception>
    public override void OnCreate()
    {
        base.OnCreate();
        string? files = FilesDir?.AbsolutePath;
        if (string.IsNullOrWhiteSpace(files))
        {
            throw new InvalidOperationException("The app data directory is not available.");
        }

        App.LibraryFilePath = Path.Combine(files, "library.json");
    }
}
