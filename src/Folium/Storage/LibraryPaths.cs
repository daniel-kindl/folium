namespace Folium.Storage;

public static class LibraryPaths
{
    public static string DefaultPath()
    {
        string root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("The app data directory is not available.");
        }

        return Path.Combine(root, "folium", "library.json");
    }
}
