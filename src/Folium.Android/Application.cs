using Android.App;
using Android.Runtime;
using Avalonia.Android;

namespace Folium.Android;

[Application]
public class Application(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
}
