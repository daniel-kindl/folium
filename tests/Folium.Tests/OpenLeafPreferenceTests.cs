using System.Runtime.Versioning;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class OpenLeafPreferenceTests
{
    [Fact]
    public void AFileNameWithoutAWaitingRestoreChangesNothing()
    {
        string directory = NewDirectory();
        LibraryViewModel library = LibraryViewModel.Open(new FileBookStore(Path.Combine(directory, "library.json")));

        library.SetRestoreFileName("Folium backup.json");

        Assert.False(library.ShowRestore);
        Assert.Equal("Backup file", library.RestoreFileName);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GridChoiceThatCannotBeReadOpensTheList()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are not available.");
        Assert.SkipWhen(string.Equals(Environment.UserName, "root", StringComparison.Ordinal), "The root user can read every file.");
        string directory = NewDirectory();
        string path = Path.Combine(directory, "library.json");
        string viewPath = path + ".view";
        File.WriteAllText(viewPath, "grid");
        File.SetUnixFileMode(viewPath, UnixFileMode.None);

        LibraryViewModel library = LibraryViewModel.Open(new FileBookStore(path));

        Assert.False(library.ShowGrid);
        File.SetUnixFileMode(viewPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.Delete(directory, recursive: true);
    }

    private static string NewDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "folium-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        return directory;
    }
}
