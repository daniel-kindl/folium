using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Folium.Storage;
using Folium.ViewModels;
using Folium.Views;

namespace Folium;

/// <summary>The Folium application.</summary>
public class App : Application
{
    /// <summary>The library file path. The Android host sets it before the main view opens.</summary>
    public static string? LibraryFilePath { get; set; }

    /// <summary>The open library. Null before the main view opens.</summary>
    public LibraryViewModel? Library { get; private set; }

    /// <summary>Loads the application markup.</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
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
        LibraryViewModel library = LibraryViewModel.Open(new FileBookStore(path));
        Library = library;
        return new MainView { DataContext = library };
    }
}
