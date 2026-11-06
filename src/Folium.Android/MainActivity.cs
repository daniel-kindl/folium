using Android.Content.PM;
using Android.Views;
using AndroidX.Activity;
using Avalonia.Android;

namespace Folium.Android;

[Activity(Label = "Folium", Theme = "@style/Theme.AppCompat.NoActionBar", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
/// <summary>The Android activity that shows Folium.</summary>
public class MainActivity : AvaloniaMainActivity
{
    private LibraryBackCallback? _libraryBackCallback;

    /// <summary>Registers the back callback after the activity starts.</summary>
    /// <param name="savedInstanceState">The state from a previous instance. Null when the activity starts fresh.</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _libraryBackCallback = new LibraryBackCallback(this);
        OnBackPressedDispatcher.AddCallback(this, _libraryBackCallback);
    }

    /// <summary>Releases the back callback before the activity stops.</summary>
    protected override void OnDestroy()
    {
        _libraryBackCallback?.Dispose();
        _libraryBackCallback = null;
        base.OnDestroy();
    }

    /// <summary>Releases the back callback when the activity is disposed.</summary>
    /// <param name="disposing">True when managed resources are released.</param>
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
