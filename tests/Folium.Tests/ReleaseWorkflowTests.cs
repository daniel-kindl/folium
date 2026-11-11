namespace Folium.Tests;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void ReleaseJobPushesTheCommitAndTheTagAtomically()
    {
        string text = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", "release.yml"));

        Assert.Contains(
            "git push --atomic origin \"HEAD:main\" \"refs/tags/v${version}\"",
            text,
            StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
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

        throw new DirectoryNotFoundException("The repository root was not found from the test output directory.");
    }
}
