using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Folium.ViewModels;

namespace Folium.Views;

/// <summary>The screen that shows the personal library.</summary>
public partial class MainView : UserControl
{
    /// <summary>Defines the <see cref="GridColumns"/> property.</summary>
    public static readonly StyledProperty<int> GridColumnsProperty =
        AvaloniaProperty.Register<MainView, int>(nameof(GridColumns), defaultValue: 3);

    private readonly Button[] _filterButtons;
    private readonly Control _emptyIcon;
    private readonly Grid _filterGrid;

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

    /// <summary>The number of cover grid columns: three, or two at large text sizes.</summary>
    public int GridColumns
    {
        get => GetValue(GridColumnsProperty);
        set => SetValue(GridColumnsProperty, value);
    }

    private async void ExportBackupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel)
        {
            await BackupPickers.ExportAsync(this, viewModel).ConfigureAwait(true);
        }
    }

    private async void RestoreBackupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel)
        {
            await BackupPickers.RestoreAsync(this, viewModel).ConfigureAwait(true);
        }
    }

    private void MainViewSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        _emptyIcon.IsVisible = args.NewSize.Width <= args.NewSize.Height;
        bool compact = IsCompactStatus();
        SetStatusMode(compact);
        GridColumns = ScaledBody() >= 24 ? 2 : 3;
        if (!compact)
        {
            ApplyFilterColumns(PreferredColumns(args.NewSize.Width));
        }
    }

    private bool IsCompactStatus()
    {
        return ScaledBody() >= 32;
    }

    private void SetStatusMode(bool compact)
    {
        if (this.FindControl<Button>("StatusFilter") is Button status)
        {
            status.IsVisible = compact;
        }

        _filterGrid.IsVisible = !compact;
        if (this.FindControl<Button>("AddBookButton") is Button add)
        {
            add.Classes.Set("folium-compact", compact);
        }
    }

    private int PreferredColumns(double width)
    {
        if (ScaledBody() >= 24)
        {
            return 2;
        }

        double grid = Math.Max(0, width - Margin.Left - Margin.Right);
        return WidestFilterLabel() <= ContentWidth(grid, columns: 4) ? 4 : 2;
    }

    private double ScaledBody()
    {
        return Application.Current?.TryGetResource("FoliumBodyFontSize", ActualThemeVariant, out object? metric) == true
            && metric is double body
            ? body
            : 0;
    }

    private double ContentWidth(double grid, int columns)
    {
        Thickness padding = _filterButtons[0].Padding;
        double gaps = _filterGrid.ColumnSpacing * (columns - 1);
        return ((grid - gaps) / columns) - padding.Left - padding.Right;
    }

    private double WidestFilterLabel()
    {
        double widest = 0;
        foreach (Button filter in _filterButtons)
        {
            TextBlock probe = new()
            {
                Text = Convert.ToString(filter.Content, CultureInfo.InvariantCulture) ?? string.Empty,
                FontFamily = filter.FontFamily,
                FontSize = filter.FontSize,
            };
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }

        // The selected segment also shows the check icon and its gap.
        return widest + CheckAllowance();
    }

    private double CheckAllowance()
    {
        double icon = this.TryFindResource("FoliumInlineIconSize", out object? size) && size is double inline ? inline : 20;
        double gap = this.TryFindResource("FoliumSpacingXs", out object? spacing) && spacing is double xs ? xs : 4;
        return icon + gap;
    }

    private void ApplyFilterColumns(int columns)
    {
        int rows = _filterButtons.Length / columns;
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
