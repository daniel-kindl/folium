using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Folium.Versioning;

/// <summary>Reads the single FoliumVersion property from a Directory.Build.props file.</summary>
public static class VersionSource
{
    /// <summary>Reads and parses the FoliumVersion property.</summary>
    /// <param name="propsPath">The path of the properties file.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="ArgumentException">The path is blank.</exception>
    /// <exception cref="InvalidOperationException">The property is missing, duplicated, or the file cannot be read.</exception>
    public static AppVersion Read(string propsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propsPath);
        try
        {
            XDocument document = XDocument.Load(propsPath);
            XElement[] elements = [.. document.Descendants("FoliumVersion")];
            return elements.Length == 1 && !string.IsNullOrWhiteSpace(elements[0].Value)
                ? AppVersion.Parse(elements[0].Value.Trim())
                : throw new InvalidOperationException("The properties file does not contain a FoliumVersion property.");
        }
        catch (Exception exception) when (exception is IOException or XmlException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "The version source at {0} cannot be read.", propsPath),
                exception);
        }
    }

    /// <summary>Fails when the requested version and the version source differ.</summary>
    /// <param name="requested">The version from the workflow input or the release tag.</param>
    /// <param name="source">The version from the version source file.</param>
    /// <exception cref="ArgumentNullException">A version is null.</exception>
    /// <exception cref="InvalidOperationException">The two versions differ.</exception>
    public static void EnsureMatches(AppVersion requested, AppVersion source)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(source);
        if (requested.Equals(source))
        {
            return;
        }

        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "The requested version {0} does not match the version source {1}.",
                requested.DisplayVersion,
                source.DisplayVersion));
    }

    /// <summary>Replaces the FoliumVersion property value and keeps the rest of the file intact.</summary>
    /// <param name="propsPath">The path of the properties file.</param>
    /// <param name="version">The version to write.</param>
    /// <exception cref="ArgumentException">The path is blank.</exception>
    /// <exception cref="ArgumentNullException">The version is null.</exception>
    /// <exception cref="InvalidOperationException">The property is missing or the file cannot be written.</exception>
    public static void Write(string propsPath, AppVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propsPath);
        ArgumentNullException.ThrowIfNull(version);
        _ = Read(propsPath);
        string text = File.ReadAllText(propsPath);
        const string OPEN = "<FoliumVersion>";
        const string CLOSE = "</FoliumVersion>";
        int start = text.IndexOf(OPEN, StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf(CLOSE, start + OPEN.Length, StringComparison.Ordinal);
        if (start < 0 || end < 0 || start != text.LastIndexOf(OPEN, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The properties file does not contain a FoliumVersion property.");
        }

        string updated = string.Concat(text.AsSpan(0, start + OPEN.Length), version.DisplayVersion, text.AsSpan(end));
        File.WriteAllText(propsPath, updated);
        AppVersion written = Read(propsPath);
        if (!written.Equals(version))
        {
            throw new InvalidOperationException("The version source was not updated.");
        }
    }
}
