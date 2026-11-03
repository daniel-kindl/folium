using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Folium.Android;

[Activity(Label = "Folium", Theme = "@android:style/Theme.Material.NoActionBar", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
