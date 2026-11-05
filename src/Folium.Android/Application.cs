using Android.App;
using Android.Runtime;
using Avalonia.Android;

namespace Folium.Android;

[Application]
public class Application : AvaloniaAndroidApplication<App>
{
    public Application(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
        var files = FilesDir?.AbsolutePath;
        if (string.IsNullOrWhiteSpace(files))
        {
            throw new InvalidOperationException("The app data directory is not available.");
        }

        App.LibraryFilePath = Path.Combine(files, "library.json");
    }
}
