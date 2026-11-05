using Folium.Storage;

namespace Folium.Tests;

public class LibraryPathsTests
{
    [Fact]
    public void DefaultPathIsThePrivateLibraryFile()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        var path = LibraryPaths.DefaultPath();

        Assert.Equal(Path.Combine(root, "folium", "library.json"), path);
    }
}
