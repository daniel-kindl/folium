namespace Folium.Versioning;

/// <summary>Parsed arguments for the release version check tool.</summary>
internal sealed class CheckReleaseArguments
{
    private CheckReleaseArguments(
        string version,
        string propsPath,
        string changelogPath,
        string? notesPath,
        bool useGitTags,
        IReadOnlyList<string> previousTags)
    {
        Version = version;
        PropsPath = propsPath;
        ChangelogPath = changelogPath;
        NotesPath = notesPath;
        UseGitTags = useGitTags;
        PreviousTags = previousTags;
    }

    public string Version { get; }

    public string PropsPath { get; }

    public string ChangelogPath { get; }

    public string? NotesPath { get; }

    public bool UseGitTags { get; }

    public IReadOnlyList<string> PreviousTags { get; }

    /// <summary>Parses the command-line arguments for the release check.</summary>
    /// <param name="args">The raw argument list.</param>
    /// <returns>The parsed arguments.</returns>
    /// <exception cref="ArgumentException">A required argument is missing or an argument is not valid.</exception>
    public static CheckReleaseArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? version = null;
        string? propsPath = null;
        string? changelogPath = null;
        string? notesPath = null;
        bool useGitTags = false;
        List<string> previousTags = [];
        bool hasPreviousTags = false;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--version":
                    version = ReadValue(args, ref index, argument);
                    break;
                case "--props":
                    propsPath = ReadValue(args, ref index, argument);
                    break;
                case "--changelog":
                    changelogPath = ReadValue(args, ref index, argument);
                    break;
                case "--notes-file":
                    notesPath = ReadValue(args, ref index, argument);
                    break;
                case "--git-tags":
                    useGitTags = true;
                    break;
                case "--previous-tags":
                    hasPreviousTags = true;
                    previousTags.AddRange(SplitTags(ReadValue(args, ref index, argument)));
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + argument, nameof(args));
            }
        }

        Reject(useGitTags && hasPreviousTags, "Use --git-tags or --previous-tags, not both.", nameof(args));
        return new CheckReleaseArguments(
            Required(version, "The --version argument is required.", nameof(args)),
            Required(propsPath, "The --props argument is required.", nameof(args)),
            Required(changelogPath, "The --changelog argument is required.", nameof(args)),
            notesPath,
            useGitTags,
            previousTags);
    }

    private static string Required(string? value, string message, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(message, paramName)
            : value;
    }

    private static void Reject(bool failed, string message, string paramName)
    {
        _ = failed ? throw new ArgumentException(message, paramName) : false;
    }

    private static string ReadValue(string[] args, ref int index, string name)
    {
        int next = index + 1;
        if (next >= args.Length || args[next].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("The " + name + " argument needs a value.", nameof(args));
        }

        index = next;
        return args[index];
    }

    private static IEnumerable<string> SplitTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return part;
        }
    }
}
