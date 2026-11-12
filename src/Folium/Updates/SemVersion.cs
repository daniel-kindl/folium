using System.Globalization;
using System.Runtime.InteropServices;

namespace Folium.Updates;

/// <summary>A numeric major, minor and patch version.</summary>
/// <param name="Major">The major version.</param>
/// <param name="Minor">The minor version.</param>
/// <param name="Patch">The patch version.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SemVersion(int Major, int Minor, int Patch) : IComparable<SemVersion>, IComparable
{
    /// <summary>Parses a strict X.Y.Z version.</summary>
    /// <param name="value">The version text.</param>
    /// <exception cref="FormatException">The value is not a numeric X.Y.Z version.</exception>
    public static SemVersion Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string[] parts = value.Split('.');
        return parts.Length == 3 && !parts.Any(part => part.Length > 1 && part[0] == '0')
            ? ParseComponents(parts)
            : throw new FormatException("The version must use X.Y.Z format.");
    }

    private static SemVersion ParseComponents(string[] parts)
    {
        return TryParseNumber(parts[0], out int major)
            && TryParseNumber(parts[1], out int minor)
            && TryParseNumber(parts[2], out int patch)
            ? new SemVersion(major, minor, patch)
            : throw new FormatException("The version must use X.Y.Z format.");
    }

    private static bool TryParseNumber(string part, out int number)
    {
        return int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 0;
    }

    /// <inheritdoc />
    public int CompareTo(SemVersion other)
    {
        int result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }
        result = Minor.CompareTo(other.Minor);
        return result != 0 ? result : Patch.CompareTo(other.Patch);
    }

    /// <inheritdoc />
    public int CompareTo(object? obj)
    {
        return obj is SemVersion version ? CompareTo(version) : throw new ArgumentException("Object must be a SemVersion.", nameof(obj));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");
    }

    /// <summary>Compares two versions by major, minor and patch values.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    public static bool operator <(SemVersion left, SemVersion right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>Compares two versions by major, minor and patch values.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    public static bool operator <=(SemVersion left, SemVersion right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>Compares two versions by major, minor and patch values.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    public static bool operator >(SemVersion left, SemVersion right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>Compares two versions by major, minor and patch values.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    public static bool operator >=(SemVersion left, SemVersion right)
    {
        return left.CompareTo(right) >= 0;
    }
}
