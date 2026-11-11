namespace Folium.Versioning;

/// <summary>Entry point for the release version check tool.</summary>
internal static class Program
{
    /// <summary>Validates a release version and writes CHANGELOG notes when requested.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>0 when the check passes; 1 when the check fails.</returns>
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception) when (exception is ArgumentException
            or FormatException
            or InvalidOperationException
            or IOException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        CheckReleaseArguments arguments = CheckReleaseArguments.Parse(args);
        IReadOnlyList<string> tags = arguments.UseGitTags ? GitTags.List() : arguments.PreviousTags;
        string notes = ReleaseCheck.Run(
            new ReleaseCheckRequest(
                arguments.Version,
                arguments.PropsPath,
                arguments.ChangelogPath,
                tags));
        if (arguments.NotesPath is not null)
        {
            File.WriteAllText(arguments.NotesPath, notes + "\n");
        }

        Console.WriteLine("The release version " + arguments.Version + " is valid.");
        return 0;
    }
}
