using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Folium.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        // The mutation compile does not run the Avalonia source generator.
        AvaloniaXamlLoader.Load(this);
    }
}
