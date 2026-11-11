using System.Text;
using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class BackupActionsTests : IDisposable
{
    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (string directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExportWritesTheBackupAndLeavesTheLibraryFile()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        MemoryStream? written = null;

        await BackupActions.ExportAsync(viewModel, name =>
        {
            Assert.Equal(BackupActions.SuggestedFileName, name);
            written = new MemoryStream();
            return Task.FromResult<Stream?>(written);
        });

        Assert.NotNull(written);
        string json = Encoding.UTF8.GetString(written.ToArray());
        Assert.Equal("Dune", Assert.Single(LibraryBackup.Read(json)).Title);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Null(viewModel.SaveError);
    }

    [Fact]
    public async Task CanceledExportDoesNotChangeTheLibrary()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        await BackupActions.ExportAsync(viewModel, _ => Task.FromResult<Stream?>(null));

        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Null(viewModel.SaveError);
        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public async Task DeniedExportShowsAnErrorAndKeepsTheLibrary()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        await BackupActions.ExportAsync(viewModel, _ => throw new UnauthorizedAccessException("denied"));

        Assert.Equal("Folium could not write the backup. The library is unchanged.", viewModel.SaveError);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RestorePreparesTheBackupWithoutWritingTheLibrary()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        string backup = LibraryBackup.Export([new Book(Guid.NewGuid(), "Neuromancer", "William Gibson")]);

        await BackupActions.RestoreAsync(viewModel, () => Task.FromResult<Stream?>(new MemoryStream(Encoding.UTF8.GetBytes(backup))));

        Assert.True(viewModel.ShowRestore);
        Assert.Equal("Neuromancer", Assert.Single(viewModel.RestoreTitles));
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanceledRestoreDoesNotChangeTheLibrary()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        await BackupActions.RestoreAsync(viewModel, () => Task.FromResult<Stream?>(null));

        Assert.False(viewModel.ShowRestore);
        Assert.Null(viewModel.SaveError);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeniedRestoreShowsAnErrorAndKeepsTheLibrary()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        await BackupActions.RestoreAsync(viewModel, () => throw new UnauthorizedAccessException("denied"));

        Assert.Equal("Folium could not read the backup. The library is unchanged.", viewModel.SaveError);
        Assert.False(viewModel.ShowRestore);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnreadableRestoreShowsAnErrorAndKeepsTheLibrary()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        await BackupActions.RestoreAsync(viewModel, () => Task.FromResult<Stream?>(new BrokenReadStream()));

        Assert.Equal("Folium could not read the backup. The library is unchanged.", viewModel.SaveError);
        Assert.False(viewModel.ShowRestore);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidRestoreFileShowsTheBackupError()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();

        await BackupActions.RestoreAsync(viewModel, () => Task.FromResult<Stream?>(new MemoryStream("{"u8.ToArray())));

        Assert.Equal("This backup is incomplete.", viewModel.SaveError);
        Assert.Equal("Dune", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
    }

    [Fact]
    public async Task CorruptUtf8RestoreKeepsTheLibrary()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        byte[] bytes = Encoding.UTF8.GetBytes(LibraryBackup.Export([new Book(Guid.NewGuid(), "Corrupt", "Author")]));
        bytes[Array.IndexOf(bytes, (byte)'C')] = 0xFF;

        await BackupActions.RestoreAsync(viewModel, () => Task.FromResult<Stream?>(new MemoryStream(bytes)));

        Assert.Equal("This backup is incomplete.", viewModel.SaveError);
        Assert.False(viewModel.ShowRestore);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingProviderStreamShowsAnError()
    {
        (LibraryViewModel viewModel, string path) = OpenWithBook();
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        await BackupActions.ExportAsync(viewModel, _ => throw new InvalidOperationException("Failed to open content stream"));
        Assert.Equal("Folium could not write the backup. The library is unchanged.", viewModel.SaveError);
        await BackupActions.RestoreAsync(viewModel, () => throw new InvalidOperationException("Failed to open content stream"));
        Assert.Equal("Folium could not read the backup. The library is unchanged.", viewModel.SaveError);
        Assert.False(viewModel.ShowRestore);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StreamOverTheByteLimitIsRejected()
    {
        using MemoryStream stream = new([1, 2, 3, 4, 5]);

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.ReadText(stream, maximumBytes: 4));

        Assert.Equal("This backup is too large to restore.", exception.Message);
    }

    [Fact]
    public void StreamAtTheByteLimitReturnsTheText()
    {
        using MemoryStream stream = new("true"u8.ToArray());

        Assert.Equal("true", LibraryBackup.ReadText(stream, maximumBytes: 4));
    }

    [Fact]
    public void ManifestDoesNotRequestBroadStorageAccess()
    {
        string manifest = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Folium.Android", "Properties", "AndroidManifest.xml"));

        Assert.Contains("android.permission.INTERNET", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("READ_EXTERNAL_STORAGE", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("WRITE_EXTERNAL_STORAGE", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("MANAGE_EXTERNAL_STORAGE", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestUsesTheOpenLeafLauncherIcon()
    {
        string root = FindRepositoryRoot();
        string manifest = File.ReadAllText(Path.Combine(root, "src", "Folium.Android", "Properties", "AndroidManifest.xml"));
        string foreground = File.ReadAllText(Path.Combine(root, "src", "Folium.Android", "Resources", "drawable", "ic_launcher_foreground.xml"));
        string monochrome = File.ReadAllText(Path.Combine(root, "src", "Folium.Android", "Resources", "drawable", "ic_launcher_monochrome.xml"));
        string background = File.ReadAllText(Path.Combine(root, "src", "Folium.Android", "Resources", "values", "colors.xml"));
        string adaptive = File.ReadAllText(Path.Combine(root, "src", "Folium.Android", "Resources", "mipmap-anydpi-v26", "ic_launcher.xml"));

        Assert.Contains("android:icon=\"@mipmap/ic_launcher\"", manifest, StringComparison.Ordinal);
        Assert.Contains("android:scaleX=\"0.6\"", foreground, StringComparison.Ordinal);
        Assert.Contains("#FAF8F3", foreground, StringComparison.Ordinal);
        Assert.Contains("#AED4B3", foreground, StringComparison.Ordinal);
        Assert.Contains("#315C43", foreground, StringComparison.Ordinal);
        Assert.Contains("#315C43", background, StringComparison.Ordinal);
        Assert.Contains("#202922", monochrome, StringComparison.Ordinal);
        Assert.Contains("@drawable/ic_launcher_foreground", adaptive, StringComparison.Ordinal);
        Assert.Contains("@drawable/ic_launcher_monochrome", adaptive, StringComparison.Ordinal);
    }

    private (LibraryViewModel ViewModel, string Path) OpenWithBook()
    {
        string directory = Path.Combine(Path.GetTempPath(), "folium-picker-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        _directories.Add(directory);
        string path = Path.Combine(directory, "library.json");
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(path));
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = "Dune";
        viewModel.AuthorInput = "Frank Herbert";
        viewModel.SaveCommand.Execute(parameter: null);
        return (viewModel, path);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Folium.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root is not above the test directory.");
    }

    private sealed class BrokenReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new IOException("The backup stream cannot be read.");
        }
    }
}
