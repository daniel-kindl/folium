using System.Text;
using Folium.Storage;

namespace Folium.ViewModels;

/// <summary>Exports and restores a backup through streams the caller opens. A null stream means the person canceled.</summary>
public static class BackupActions
{
    /// <summary>The file name offered by the document picker. This is not the live library file name.</summary>
    public static string SuggestedFileName => "Folium backup.json";

    /// <summary>Writes a backup to the stream from <paramref name="openWrite"/>. A failure leaves the library unchanged.</summary>
    /// <param name="viewModel">The open library. Must not be null.</param>
    /// <param name="openWrite">Opens the chosen file. A null stream means the person canceled.</param>
    /// <returns>A task that ends when the export ends.</returns>
    /// <exception cref="ArgumentNullException">The view model or the callback is null.</exception>
    public static async Task ExportAsync(LibraryViewModel viewModel, Func<string, Task<Stream?>> openWrite)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(openWrite);
        string? json = TryExport(viewModel);
        if (json is null)
        {
            return;
        }

        string? error = await WriteBackupAsync(json, openWrite).ConfigureAwait(true);
        if (error is not null)
        {
            viewModel.ShowBackupError(error);
        }
    }

    /// <summary>Reads a backup from the stream and prepares replace confirmation. A failure leaves the library unchanged.</summary>
    /// <param name="viewModel">The open library. Must not be null.</param>
    /// <param name="openRead">Opens the chosen file. A null stream means the person canceled.</param>
    /// <returns>A task that ends when the restore preview is ready or the read has failed.</returns>
    /// <exception cref="ArgumentNullException">The view model or the callback is null.</exception>
    public static async Task RestoreAsync(LibraryViewModel viewModel, Func<Task<Stream?>> openRead)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(openRead);
        (string? json, string? error) = await ReadBackupAsync(openRead).ConfigureAwait(true);
        if (error is not null)
        {
            viewModel.ShowBackupError(error);
            return;
        }

        if (json is not null)
        {
            viewModel.PrepareRestore(json);
        }
    }

    private static async Task<(string? Json, string? Error)> ReadBackupAsync(Func<Task<Stream?>> openRead)
    {
        try
        {
            return await ReadOpenBackupAsync(openRead).ConfigureAwait(false);
        }
        catch (LibraryBackupException exception)
        {
            return (null, exception.Message);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return (null, "Folium could not read the backup. The library is unchanged.");
        }
    }

    private static async Task<(string? Json, string? Error)> ReadOpenBackupAsync(Func<Task<Stream?>> openRead)
    {
        Stream? stream = await openRead().ConfigureAwait(false);
        if (stream is null)
        {
            return (null, null);
        }

        try
        {
            return (LibraryBackup.ReadText(stream), null);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool IsStorageFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static async Task<string?> WriteBackupAsync(string json, Func<string, Task<Stream?>> openWrite)
    {
        try
        {
            Stream? stream = await openWrite(SuggestedFileName).ConfigureAwait(false);
            if (stream is null)
            {
                return null;
            }

            StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            try
            {
                await writer.WriteAsync(json).ConfigureAwait(false);
            }
            finally
            {
                await writer.DisposeAsync().ConfigureAwait(false);
            }

            return null;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return "Folium could not write the backup. The library is unchanged.";
        }
    }

    private static string? TryExport(LibraryViewModel viewModel)
    {
        try
        {
            return viewModel.CreateBackup();
        }
        catch (LibraryBackupException exception)
        {
            viewModel.ShowBackupError(exception.Message);
            return null;
        }
    }
}
