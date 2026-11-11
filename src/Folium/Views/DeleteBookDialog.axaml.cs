using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Folium.Views;

/// <summary>The dialog that asks before Folium deletes a book. Keep book comes first.</summary>
public partial class DeleteBookDialog : UserControl
{
    /// <summary>Loads the dialog markup. The mutation compile does not run the Avalonia source generator.</summary>
    public DeleteBookDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
