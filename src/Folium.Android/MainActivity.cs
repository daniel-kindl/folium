using Android.Content.PM;
using Android.Views;
using AndroidX.Activity;
using Avalonia.Android;

namespace Folium.Android;

[Activity(Label = "Folium", Theme = "@style/Theme.AppCompat.NoActionBar", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity
{
    private LibraryBackCallback? _libraryBackCallback;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _libraryBackCallback = new LibraryBackCallback(this);
        OnBackPressedDispatcher.AddCallback(this, _libraryBackCallback);
    }

    protected override void OnDestroy()
    {
        _libraryBackCallback?.Dispose();
        _libraryBackCallback = null;
        base.OnDestroy();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _libraryBackCallback?.Dispose();
            _libraryBackCallback = null;
        }

        base.Dispose(disposing);
    }

    private static bool LeaveScreen()
    {
        return Avalonia.Application.Current is App { Library: { } library } && library.GoBack();
    }

    private sealed class LibraryBackCallback(MainActivity activity) : OnBackPressedCallback(enabled: true)
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
