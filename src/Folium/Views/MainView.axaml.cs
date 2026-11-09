using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Folium.Views;

/// <summary>The screen that shows the personal library.</summary>
public partial class MainView : UserControl
{
    private readonly Button[] _filterButtons;
    private readonly Control _emptyIcon;
    private readonly Grid _filterGrid;
    private bool? _isLandscape;

    /// <summary>Loads the view markup. The mutation compile does not run the Avalonia source generator.</summary>
    public MainView()
    {
        AvaloniaXamlLoader.Load(this);
        _emptyIcon = this.FindControl<Control>("EmptyIcon")!;
        _filterGrid = this.FindControl<Grid>("FilterGrid")!;
        _filterButtons =
        [
            this.FindControl<Button>("AllFilter")!,
            this.FindControl<Button>("ToReadFilter")!,
            this.FindControl<Button>("ReadingFilter")!,
            this.FindControl<Button>("FinishedFilter")!,
        ];
        SizeChanged += MainViewSizeChanged;
    }

    private void MainViewSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        bool isLandscape = args.NewSize.Width > args.NewSize.Height;
        if (_isLandscape == isLandscape)
        {
            return;
        }

        _isLandscape = isLandscape;
        _emptyIcon.IsVisible = !isLandscape;
        int columns = isLandscape ? _filterButtons.Length : 2;
        int rows = isLandscape ? 1 : 2;
        _filterGrid.ColumnDefinitions.Clear();
        _filterGrid.RowDefinitions.Clear();
        for (int column = 0; column < columns; column++)
        {
            _filterGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int row = 0; row < rows; row++)
        {
            _filterGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (int index = 0; index < _filterButtons.Length; index++)
        {
            _ = _filterButtons[index].SetValue(Grid.RowProperty, index / columns);
            _ = _filterButtons[index].SetValue(Grid.ColumnProperty, index % columns);
        }
    }
}
