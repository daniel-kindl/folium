using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Folium.Views;

/// <summary>The status filter at large text sizes. It shows "Status: All" and opens a list of the four filters.</summary>
public partial class StatusFilterButton : Button
{
    /// <summary>Loads the button markup. The mutation compile does not run the Avalonia source generator.</summary>
    public StatusFilterButton()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Button);
}
