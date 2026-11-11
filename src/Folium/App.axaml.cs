using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Folium.Storage;
using Folium.ViewModels;
using Folium.Views;

namespace Folium;

/// <summary>The Folium application.</summary>
public class App : Application
{
    private static readonly string[] FontMetricKeys =
    [
        "FoliumCaptionFontSize", "FoliumCaptionLineHeight",
        "FoliumBodyFontSize", "FoliumBodyLineHeight",
        "FoliumLabelFontSize", "FoliumLabelLineHeight",
        "FoliumSectionFontSize", "FoliumSectionLineHeight",
        "FoliumTitleFontSize", "FoliumTitleLineHeight",
        "FoliumLabelSmallFontSize", "FoliumLabelSmallLineHeight",
    ];
    private static Dictionary<string, double>? _baseFontMetrics;

    /// <summary>The library file path. The Android host sets it before the main view opens.</summary>
    public static string? LibraryFilePath { get; set; }

#if FOLIUM_VERIFICATION
    /// <summary>The deterministic search provider for native verification builds.</summary>
    public static Search.IBookSearch? VerificationSearch { get; set; }
#endif

    /// <summary>The open library. Null before the main view opens.</summary>
    public LibraryViewModel? Library { get; private set; }

    /// <summary>Loads the application markup.</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Scales UI text to match the current platform text size.</summary>
    /// <param name="scale">The platform text scale.</param>
    /// <exception cref="ArgumentOutOfRangeException">The scale is not finite or is not positive.</exception>
    public void ApplySystemFontScale(double scale)
    {
        if (!double.IsFinite(scale) || scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        _baseFontMetrics ??= ReadBaseFontMetrics();
        foreach ((string key, double value) in _baseFontMetrics)
        {
            Resources[key] = value * scale;
        }
    }

    /// <summary>Supplies the main view for an Android activity lifetime.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = CreateMainView;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private MainView CreateMainView()
    {
        string path = string.IsNullOrWhiteSpace(LibraryFilePath) ? LibraryPaths.DefaultPath() : LibraryFilePath;
#if FOLIUM_VERIFICATION
        LibraryViewModel library = LibraryViewModel.Open(new FileBookStore(path), VerificationSearch);
#else
        LibraryViewModel library = LibraryViewModel.Open(new FileBookStore(path));
#endif
        Library = library;
        return new MainView { DataContext = library };
    }

    private Dictionary<string, double> ReadBaseFontMetrics()
    {
        Dictionary<string, double> metrics = new(StringComparer.Ordinal);
        foreach (string key in FontMetricKeys)
        {
            if (!Resources.TryGetResource(key, ThemeVariant.Light, out object? value)
                || value is not double metric)
            {
                throw new InvalidOperationException($"The theme resource '{key}' must be a number.");
            }

            metrics.Add(key, metric);
        }

        return metrics;
    }
}
