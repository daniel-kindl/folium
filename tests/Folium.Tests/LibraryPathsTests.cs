using Folium.Storage;

namespace Folium.Tests;

public class LibraryPathsTests
{
    [Fact]
    public void DefaultPathIsThePrivateLibraryFile()
    {
        string root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        string path = LibraryPaths.DefaultPath();

        Assert.Equal(Path.Combine(root, "folium", "library.json"), path);
    }
}
