namespace Folium.Versioning;

/// <summary>Entry point for the release preparation tool.</summary>
internal static class Program
{
    /// <summary>Writes the next FoliumVersion and CHANGELOG section for a release.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>0 when preparation succeeds; 1 when it fails.</returns>
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception) when (exception is ArgumentException
            or FormatException
            or InvalidOperationException
            or ArgumentOutOfRangeException
            or IOException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        PrepareReleaseArguments arguments = PrepareReleaseArguments.Parse(args);
        IReadOnlyList<string> tags = arguments.UseGit ? GitHistory.MergedReleaseTags() : arguments.Tags;
        IReadOnlyList<ReleaseCommit> commits = arguments.UseGit
            ? GitHistory.CommitsSince("v" + ReleasePrepare.LatestRelease(tags).DisplayVersion)
            : [];
        ReleasePrepareResult result = ReleasePrepare.Run(
            new ReleasePrepareRequest(
                arguments.PropsPath,
                arguments.ChangelogPath,
                tags,
                commits,
                arguments.Bump,
                arguments.RepositoryUrl));
        if (arguments.NotesPath is not null)
        {
            File.WriteAllText(arguments.NotesPath, result.Notes + "\n");
        }

        if (arguments.VersionPath is not null)
        {
            File.WriteAllText(arguments.VersionPath, result.Version.DisplayVersion);
        }

        Console.WriteLine(
            "Prepared release "
            + result.Version.DisplayVersion
            + " after "
            + result.PreviousTag
            + ".");
        return 0;
    }
}
