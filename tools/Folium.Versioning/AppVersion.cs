using System.Globalization;

namespace Folium.Versioning;

/// <summary>A strict SemVer X.Y.Z value and its Android version code.</summary>
public sealed class AppVersion : IEquatable<AppVersion>
{
    /// <summary>The largest minor or patch value the version code formula accepts.</summary>
    private const int MAX_MINOR_OR_PATCH = 99;

    /// <summary>The largest major value that keeps the version code inside Android's integer range.</summary>
    private const int MAX_MAJOR = 209999;

    /// <summary>Creates a version with the given components.</summary>
    /// <param name="major">The major version. Must be 0 or greater and at most 209999.</param>
    /// <param name="minor">The minor version. Must be 0 through 99.</param>
    /// <param name="patch">The patch version. Must be 0 through 99.</param>
    /// <exception cref="ArgumentOutOfRangeException">A component is negative or above the documented limit.</exception>
    public AppVersion(int major, int minor, int patch)
    {
        if (major is < 0 or > MAX_MAJOR)
        {
            throw new ArgumentOutOfRangeException(
                nameof(major),
                major,
                "The major version must be between 0 and 209999.");
        }

        if (minor is < 0 or > MAX_MINOR_OR_PATCH)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minor),
                minor,
                "The minor version must be below 100.");
        }

        if (patch is < 0 or > MAX_MINOR_OR_PATCH)
        {
            throw new ArgumentOutOfRangeException(
                nameof(patch),
                patch,
                "The patch version must be below 100.");
        }

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>The major version. Must be 0 or greater and at most 209999.</summary>
    public int Major { get; }

    /// <summary>The minor version. Must be 0 through 99.</summary>
    public int Minor { get; }

    /// <summary>The patch version. Must be 0 through 99.</summary>
    public int Patch { get; }

    /// <summary>The user-visible version. This value equals the strict SemVer text.</summary>
    public string DisplayVersion =>
        string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Major, Minor, Patch);

    /// <summary>The Android version code, computed as major * 10000 + minor * 100 + patch.</summary>
    public int VersionCode => checked((Major * 10000) + (Minor * 100) + Patch);

    /// <summary>Returns the next version for an explicit patch, minor, or major bump.</summary>
    /// <param name="bump">The bump to apply. <see cref="VersionBump.Auto"/> is not valid here.</param>
    /// <returns>The next version, with lower components reset for a minor or major bump.</returns>
    /// <exception cref="ArgumentException">The bump is Auto.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The next component would exceed the version code limits.</exception>
    public AppVersion Bump(VersionBump bump)
    {
        return bump switch
        {
            VersionBump.Patch => new AppVersion(Major, Minor, Patch + 1),
            VersionBump.Minor => new AppVersion(Major, Minor + 1, 0),
            VersionBump.Major => new AppVersion(Major + 1, 0, 0),
            VersionBump.Auto => throw new ArgumentException("An explicit bump is required.", nameof(bump)),
            _ => throw new ArgumentOutOfRangeException(nameof(bump), bump, "The bump is not supported."),
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return DisplayVersion;
    }

    /// <inheritdoc />
    public bool Equals(AppVersion? other)
    {
        return other is not null && Major == other.Major && Minor == other.Minor && Patch == other.Patch;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is AppVersion other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Patch);
    }

    /// <summary>Parses strict SemVer text in the form X.Y.Z.</summary>
    /// <param name="text">The version text. It must be strict SemVer X.Y.Z.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="FormatException">The text is not strict SemVer X.Y.Z.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A component is outside the formula limits.</exception>
    public static AppVersion Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryReadParts(text, out string majorText, out string minorText, out string patchText)
            ? new AppVersion(ParseComponent(majorText), ParseComponent(minorText), ParseComponent(patchText))
            : throw new FormatException("The version must be strict SemVer X.Y.Z with no prefix or suffix.");
    }

    /// <summary>Returns true when this version code is higher than every previous release.</summary>
    /// <param name="previousReleases">The versions that were released earlier.</param>
    /// <returns>true when this version code is strictly greater, or when the list is empty.</returns>
    /// <exception cref="ArgumentNullException">The list or one of its items is null.</exception>
    public bool Outranks(IEnumerable<AppVersion> previousReleases)
    {
        return HighestBlocking(previousReleases) is null;
    }

    /// <summary>Fails when this version code is not higher than every previous release.</summary>
    /// <param name="previousReleases">The versions that were released earlier.</param>
    /// <exception cref="ArgumentNullException">The list or one of its items is null.</exception>
    /// <exception cref="InvalidOperationException">The version code does not strictly increase.</exception>
    public void EnsureOutranks(IEnumerable<AppVersion> previousReleases)
    {
        AppVersion? highest = HighestBlocking(previousReleases);
        if (highest is null)
        {
            return;
        }

        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "The version {0} (code {1}) is not higher than the previous release {2} (code {3}).",
                DisplayVersion,
                VersionCode,
                highest.DisplayVersion,
                highest.VersionCode));
    }

    /// <summary>Parses strict vX.Y.Z tags and ignores every other tag name.</summary>
    /// <param name="tags">Tag names from the repository.</param>
    /// <returns>The parsed release versions, in the same order as the matching tags.</returns>
    /// <exception cref="ArgumentNullException">The list is null.</exception>
    public static IReadOnlyList<AppVersion> ParseReleaseTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        List<AppVersion> versions = [];
        foreach (string tag in tags)
        {
            if (tag?.StartsWith('v') == true && TryParseReleaseText(tag[1..], out AppVersion? version) && version is not null)
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    /// <summary>Returns earlier releases, excluding the version that is being released.</summary>
    /// <param name="tags">Tag names from the repository.</param>
    /// <param name="current">The version that is being released.</param>
    /// <returns>The earlier releases.</returns>
    /// <exception cref="ArgumentNullException">The tags or the current version are null.</exception>
    public static IReadOnlyList<AppVersion> PreviousReleases(IEnumerable<string> tags, AppVersion current)
    {
        ArgumentNullException.ThrowIfNull(current);
        List<AppVersion> previous = [];
        foreach (AppVersion version in ParseReleaseTags(tags))
        {
            if (!version.Equals(current))
            {
                previous.Add(version);
            }
        }

        return previous;
    }

    private AppVersion? HighestBlocking(IEnumerable<AppVersion> previousReleases)
    {
        ArgumentNullException.ThrowIfNull(previousReleases);
        AppVersion? highest = null;
        foreach (AppVersion previous in previousReleases)
        {
            if (previous is null)
            {
                throw new ArgumentNullException(nameof(previousReleases));
            }

            if (highest is null || previous.VersionCode > highest.VersionCode)
            {
                highest = previous;
            }
        }

        return highest is null || VersionCode > highest.VersionCode ? null : highest;
    }

    private static bool TryParseReleaseText(string text, out AppVersion? version)
    {
        version = null;
        if (!TryReadParts(text, out string majorText, out string minorText, out string patchText))
        {
            return false;
        }

        try
        {
            version = new AppVersion(ParseComponent(majorText), ParseComponent(minorText), ParseComponent(patchText));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryReadParts(string text, out string major, out string minor, out string patch)
    {
        major = string.Empty;
        minor = string.Empty;
        patch = string.Empty;
        string[] parts = text.Split('.', 4);
        if (parts.Length != 3)
        {
            return false;
        }

        major = parts[0];
        minor = parts[1];
        patch = parts[2];
        return IsNumericIdentifier(major) && IsNumericIdentifier(minor) && IsNumericIdentifier(patch);
    }

    private static bool IsNumericIdentifier(string value)
    {
        if (string.Equals(value, "0", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Length == 0 || value[0] is < '1' or > '9')
        {
            return false;
        }

        for (int index = 1; index < value.Length; index++)
        {
            if (value[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static int ParseComponent(string text)
    {
        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long value)
            && value <= int.MaxValue
            ? (int)value
            : throw new FormatException("The version must be strict SemVer X.Y.Z with no prefix or suffix.");
    }
}
