using Android.Runtime;
using Avalonia.Android;

namespace Folium.Android;

[Application]
public class Application(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
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
