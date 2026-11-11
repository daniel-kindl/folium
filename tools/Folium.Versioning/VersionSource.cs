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
}
