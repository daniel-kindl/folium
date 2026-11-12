using Android.Content.PM;
using Android.Content.Res;
using Android.Views;
using AndroidX.Activity;
using Avalonia.Android;

namespace Folium.Android;

/// <summary>The Android activity that shows Folium.</summary>
[Activity(Label = "Folium", Theme = "@style/Theme.AppCompat.NoActionBar", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.FontScale,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity
{
    private LibraryBackCallback? _libraryBackCallback;

    /// <summary>Registers the back callback after the activity starts.</summary>
    /// <param name="savedInstanceState">The state from a previous instance. Null when the activity starts fresh.</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemFontScale(Resources?.Configuration?.FontScale ?? 1);
        _libraryBackCallback = new LibraryBackCallback(this);
        OnBackPressedDispatcher.AddCallback(this, _libraryBackCallback);
    }

    /// <summary>Checks for a due app update when the activity resumes.</summary>
    protected override void OnResume()
    {
        base.OnResume();
        if (Avalonia.Application.Current is App { Library.Updates: { } updates })
        {
            updates.OnResume();
            _ = updates.CheckAtStartAsync();
        }
    }

    /// <summary>Applies the system text size after a handled configuration change.</summary>
    /// <param name="newConfig">The new Android configuration.</param>
    public override void OnConfigurationChanged(Configuration newConfig)
    {
        ArgumentNullException.ThrowIfNull(newConfig);
        base.OnConfigurationChanged(newConfig);
        ApplySystemFontScale(newConfig.FontScale);
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

    private static void ApplySystemFontScale(double scale)
    {
        if (Avalonia.Application.Current is App app)
        {
            app.ApplySystemFontScale(scale);
        }
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
