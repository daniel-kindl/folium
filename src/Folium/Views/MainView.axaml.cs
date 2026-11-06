using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Folium.Views;

/// <summary>The screen that shows the personal library.</summary>
public partial class MainView : UserControl
{
    /// <summary>Loads the view markup. The mutation compile does not run the Avalonia source generator.</summary>
    public MainView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
