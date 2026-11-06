namespace Folium.Storage;

/// <summary>Paths for the personal library file.</summary>
public static class LibraryPaths
{
    /// <summary>Returns the default library file under the application data directory.</summary>
    /// <exception cref="InvalidOperationException">The application data directory is not available.</exception>
    public static string DefaultPath()
    {
        string root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        return string.IsNullOrWhiteSpace(root)
            ? throw new InvalidOperationException("The app data directory is not available.")
            : Path.Combine(root, "folium", "library.json");
    }
}
