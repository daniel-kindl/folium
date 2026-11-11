namespace Folium.Tests;

internal sealed class ReleaseCheckFiles : IDisposable
{
    private ReleaseCheckFiles(string directory, string propsPath, string changelogPath)
    {
        _directory = directory;
        PropsPath = propsPath;
        ChangelogPath = changelogPath;
    }

    private readonly string _directory;

    public string PropsPath { get; }

    public string ChangelogPath { get; }

    public static ReleaseCheckFiles Create(string version, string changelog)
    {
        string directory = Path.Combine(Path.GetTempPath(), "folium-release-check-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        string propsPath = Path.Combine(directory, "Directory.Build.props");
        string changelogPath = Path.Combine(directory, "CHANGELOG.md");
        File.WriteAllText(
            propsPath,
            "<Project><PropertyGroup><FoliumVersion>" + version + "</FoliumVersion></PropertyGroup></Project>\n");
        File.WriteAllText(changelogPath, changelog);
        return new ReleaseCheckFiles(directory, propsPath, changelogPath);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
