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
            DefaultExtension = "json",
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
