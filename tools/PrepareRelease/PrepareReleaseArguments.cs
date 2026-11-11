namespace Folium.Versioning;

/// <summary>Parsed arguments for the release preparation tool.</summary>
internal sealed class PrepareReleaseArguments
{
    private PrepareReleaseArguments(
        string propsPath,
        string changelogPath,
        VersionBump bump,
        Uri repositoryUrl,
        bool useGit,
        IReadOnlyList<string> tags,
        string? notesPath,
        string? versionPath)
    {
        PropsPath = propsPath;
        ChangelogPath = changelogPath;
        Bump = bump;
        RepositoryUrl = repositoryUrl;
        UseGit = useGit;
        Tags = tags;
        NotesPath = notesPath;
        VersionPath = versionPath;
    }

    public string PropsPath { get; }

    public string ChangelogPath { get; }

    public VersionBump Bump { get; }

    public Uri RepositoryUrl { get; }

    public bool UseGit { get; }

    public IReadOnlyList<string> Tags { get; }

    public string? NotesPath { get; }

    public string? VersionPath { get; }

    /// <summary>Parses the command-line arguments for release preparation.</summary>
    /// <param name="args">The raw argument list.</param>
    /// <returns>The parsed arguments.</returns>
    /// <exception cref="ArgumentException">A required argument is missing or an argument is not valid.</exception>
    public static PrepareReleaseArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        RawArguments raw = ReadRaw(args);
        Reject(raw.UseGit && raw.HasTags, "Use --git or --tags, not both.", nameof(args));
        Reject(!raw.UseGit && !raw.HasTags, "Use --git or --tags.", nameof(args));
        return new PrepareReleaseArguments(
            Required(raw.PropsPath, "The --props argument is required.", nameof(args)),
            Required(raw.ChangelogPath, "The --changelog argument is required.", nameof(args)),
            raw.Bump,
            ParseRepository(Required(raw.Repository, "The --repository argument is required.", nameof(args))),
            raw.UseGit,
            raw.Tags,
            raw.NotesPath,
            raw.VersionPath);
    }

    private static RawArguments ReadRaw(string[] args)
    {
        RawArguments raw = new();
        for (int index = 0; index < args.Length; index++)
        {
            Apply(raw, args, ref index);
        }

        return raw;
    }

    private static void Apply(RawArguments raw, string[] args, ref int index)
    {
        string argument = args[index];
        switch (argument)
        {
            case "--props":
                raw.PropsPath = ReadValue(args, ref index, argument);
                break;
            case "--changelog":
                raw.ChangelogPath = ReadValue(args, ref index, argument);
                break;
            case "--bump":
                raw.Bump = ParseBump(ReadValue(args, ref index, argument));
                break;
            case "--repository":
                raw.Repository = ReadValue(args, ref index, argument);
                break;
            case "--git":
                raw.UseGit = true;
                break;
            case "--tags":
                raw.HasTags = true;
                raw.Tags.AddRange(SplitList(ReadValue(args, ref index, argument)));
                break;
            case "--notes-file":
                raw.NotesPath = ReadValue(args, ref index, argument);
                break;
            case "--version-file":
                raw.VersionPath = ReadValue(args, ref index, argument);
                break;
            default:
                throw new ArgumentException("Unknown argument: " + argument, nameof(args));
        }
    }

    private static VersionBump ParseBump(string value)
    {
        return value switch
        {
            "auto" => VersionBump.Auto,
            "patch" => VersionBump.Patch,
            "minor" => VersionBump.Minor,
            "major" => VersionBump.Major,
            _ => throw new ArgumentException(
                "The --bump argument must be auto, patch, minor, or major.",
                nameof(value)),
        };
    }

    private static Uri ParseRepository(string value)
    {
        return ReadAbsoluteHttp(value)
            ?? ReadGithubSlug(value)
            ?? throw new ArgumentException(
                "The --repository argument must be an owner/name slug or an absolute HTTP URL.",
                nameof(value));
    }

    private static Uri? ReadAbsoluteHttp(string value)
    {
        return TryAbsoluteHttp(value, out Uri? absolute) ? absolute : null;
    }

    private static Uri? ReadGithubSlug(string value)
    {
        return TryGithubSlug(value, out Uri? fromSlug) ? fromSlug : null;
    }

    private static bool TryAbsoluteHttp(string value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute) || !IsHttp(absolute))
        {
            return false;
        }

        uri = absolute;
        return true;
    }

    private static bool TryGithubSlug(string value, out Uri? uri)
    {
        uri = null;
        if (value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length != 2
            || value.Contains(' ', StringComparison.Ordinal)
            || !Uri.TryCreate("https://github.com/" + value.Trim(), UriKind.Absolute, out Uri? fromSlug))
        {
            return false;
        }

        uri = fromSlug;
        return true;
    }

    private static bool IsHttp(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
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

    private static IEnumerable<string> SplitList(string value)
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

    private sealed class RawArguments
    {
        public string? PropsPath { get; set; }

        public string? ChangelogPath { get; set; }

        public VersionBump Bump { get; set; } = VersionBump.Auto;

        public string? Repository { get; set; }

        public bool UseGit { get; set; }

        public bool HasTags { get; set; }

        public List<string> Tags { get; } = [];

        public string? NotesPath { get; set; }

        public string? VersionPath { get; set; }
    }
}
