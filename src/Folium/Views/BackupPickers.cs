using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Folium.ViewModels;

namespace Folium.Views;

/// <summary>Opens the platform document picker for backup export and restore.</summary>
public static class BackupPickers
{
    /// <summary>Asks for a file and writes a backup to it. A failure shows an error and keeps the library.</summary>
    /// <param name="view">The view that hosts the document picker.</param>
    /// <param name="viewModel">The open library.</param>
    /// <returns>A task that ends when the export ends.</returns>
    public static async Task ExportAsync(Visual view, LibraryViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        IStorageProvider? provider = TopLevel.GetTopLevel(view)?.StorageProvider;
        if (provider?.CanSave != true)
        {
            viewModel.ShowBackupError("Folium could not open the document picker.");
            return;
        }

        await BackupActions.ExportAsync(
            viewModel,
            async name => await OpenExportAsync(provider, name).ConfigureAwait(false)).ConfigureAwait(true);
    }

    /// <summary>Asks for a backup file and prepares replace confirmation with the file name.</summary>
    /// <param name="view">The view that hosts the document picker.</param>
    /// <param name="viewModel">The open library.</param>
    /// <returns>A task that ends when the restore preview is ready or the read has failed.</returns>
    public static async Task RestoreAsync(Visual view, LibraryViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        IStorageProvider? provider = TopLevel.GetTopLevel(view)?.StorageProvider;
        if (provider?.CanOpen != true)
        {
            viewModel.ShowBackupError("Folium could not open the document picker.");
            return;
        }

        PickedFile picked = new(provider);
        await BackupActions.RestoreAsync(viewModel, picked.OpenAsync).ConfigureAwait(true);
        viewModel.SetRestoreFileName(picked.Name);
    }

    private static async Task<Stream?> OpenExportAsync(IStorageProvider provider, string name)
    {
        IStorageFile? file = await provider.SaveFilePickerAsync(SaveOptions(name)).ConfigureAwait(true);
        return file is null ? null : await file.OpenWriteAsync().ConfigureAwait(true);
    }

    private static async Task<IStorageFile?> PickRestoreAsync(IStorageProvider provider)
    {
        IReadOnlyList<IStorageFile> files = await provider.OpenFilePickerAsync(OpenOptions()).ConfigureAwait(true);
        return files.Count == 0 ? null : files[0];
    }

    private sealed class PickedFile(IStorageProvider provider)
    {
        public string Name { get; private set; } = "";

        public async Task<Stream?> OpenAsync()
        {
            IStorageFile? file = await PickRestoreAsync(provider).ConfigureAwait(false);
            if (file is null)
            {
                return null;
            }

            Name = file.Name;
            return await file.OpenReadAsync().ConfigureAwait(false);
        }
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
}
