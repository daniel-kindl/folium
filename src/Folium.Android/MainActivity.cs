using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
using Avalonia;
using Avalonia.Android;

namespace Folium.Android;

[Activity(Label = "Folium", Theme = "@style/Theme.AppCompat.NoActionBar", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        OnBackPressedDispatcher.AddCallback(this, new LibraryBackCallback(this));
    }

    private static bool LeaveScreen() =>
        Avalonia.Application.Current is App { Library: { } library } && library.GoBack();

    private sealed class LibraryBackCallback(MainActivity activity) : OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            if (LeaveScreen())
            {
                return;
            }

            Enabled = false;
            activity.OnBackPressedDispatcher.OnBackPressed();
            Enabled = true;
        }
    }
}
