using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Folium.Storage;
using Folium.ViewModels;
using Folium.Views;

namespace Folium;

public partial class App : Application
{
    public static string? LibraryFilePath { get; set; }

    public LibraryViewModel? Library { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

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
        var path = string.IsNullOrWhiteSpace(LibraryFilePath) ? LibraryPaths.DefaultPath() : LibraryFilePath;
        var library = LibraryViewModel.Open(new FileBookStore(path));
        Library = library;
        return new MainView { DataContext = library };
    }
}
