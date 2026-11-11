using System.Diagnostics;

namespace Folium.Versioning;

/// <summary>Reads local git tag names that match the release pattern.</summary>
internal static class GitTags
{
    /// <summary>Lists local tags that match v*.*.*.</summary>
    /// <returns>The tag names, one per line of git output.</returns>
    /// <exception cref="InvalidOperationException">The git command fails or git is not available.</exception>
    public static IReadOnlyList<string> List()
    {
        using Process process = new();
        process.StartInfo.FileName = "git";
        process.StartInfo.ArgumentList.Add("tag");
        process.StartInfo.ArgumentList.Add("-l");
        process.StartInfo.ArgumentList.Add("v*.*.*");
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        try
        {
            _ = process.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("The git command is not available.", exception);
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        EnsureSuccess(process.ExitCode, error);
        return ReadTagList(output);
    }

    private static void EnsureSuccess(int exitCode, string error)
    {
        _ = exitCode == 0
            ? false
            : throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? "The git tag command failed."
                    : error.Trim());
    }

    private static string[] ReadTagList(string output)
    {
        return string.IsNullOrWhiteSpace(output)
            ? []
            : output
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
