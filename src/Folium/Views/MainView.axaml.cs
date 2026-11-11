using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Folium.ViewModels;

namespace Folium.Views;

/// <summary>The screen that shows the personal library.</summary>
public partial class MainView : UserControl
{
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

    private async void ExportBackupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel viewModel)
        {
            return;
        }

        IStorageProvider? provider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (provider?.CanSave != true)
        {
            viewModel.ShowBackupError("Folium could not open the document picker.");
            return;
        }

        await BackupActions.ExportAsync(
            viewModel,
            async name => await OpenExportAsync(provider, name).ConfigureAwait(false)).ConfigureAwait(true);
    }

    private async void RestoreBackupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel viewModel)
        {
            return;
        }

        IStorageProvider? provider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (provider?.CanOpen != true)
        {
            viewModel.ShowBackupError("Folium could not open the document picker.");
            return;
        }

        await BackupActions.RestoreAsync(
            viewModel,
            async () => await OpenRestoreAsync(provider).ConfigureAwait(false)).ConfigureAwait(true);
    }

    private static async Task<Stream?> OpenExportAsync(IStorageProvider provider, string name)
    {
        IStorageFile? file = await provider.SaveFilePickerAsync(SaveOptions(name)).ConfigureAwait(true);
        return file is null ? null : await file.OpenWriteAsync().ConfigureAwait(true);
    }

    private static async Task<Stream?> OpenRestoreAsync(IStorageProvider provider)
    {
        IReadOnlyList<IStorageFile> files = await provider.OpenFilePickerAsync(OpenOptions()).ConfigureAwait(true);
        return files.Count == 0 ? null : await files[0].OpenReadAsync().ConfigureAwait(true);
    }

    private static FilePickerSaveOptions SaveOptions(string name)
    {
        return new FilePickerSaveOptions
        {
            Title = "Export backup",
            SuggestedFileName = name,
            ShowOverwritePrompt = true,
            FileTypeChoices = [BackupFileType()],
        };
    }

    private static FilePickerOpenOptions OpenOptions()
    {
        return new FilePickerOpenOptions
        {
            Title = "Restore backup",
            AllowMultiple = false,
            FileTypeFilter = [BackupFileType()],
        };
    }

    private static FilePickerFileType BackupFileType()
    {
        return new FilePickerFileType("Folium backup")
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"],
        };
    }

    private void MainViewSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        _emptyIcon.IsVisible = args.NewSize.Width <= args.NewSize.Height;
        bool compact = IsCompactStatus();
        SetStatusMode(compact);
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

        return widest;
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
