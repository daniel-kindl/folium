using System.Diagnostics;
using System.Text;

namespace Folium.Versioning;

/// <summary>Reads merged release tags and commits since a tag from the local git repository.</summary>
internal static class GitHistory
{
    /// <summary>Lists v*.*.* tags that are ancestors of HEAD.</summary>
    /// <returns>The tag names.</returns>
    /// <exception cref="InvalidOperationException">The git command fails or git is not available.</exception>
    public static IReadOnlyList<string> MergedReleaseTags()
    {
        return Run(["tag", "-l", "v*.*.*", "--merged", "HEAD"]);
    }

    /// <summary>Lists commits after the tag, oldest first, with title and body.</summary>
    /// <param name="tag">The previous release tag, including the v prefix.</param>
    /// <returns>The commits between the tag and HEAD.</returns>
    /// <exception cref="ArgumentException">The tag is blank.</exception>
    /// <exception cref="InvalidOperationException">The git command fails or git is not available.</exception>
    public static IReadOnlyList<ReleaseCommit> CommitsSince(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return ReadCommits(RunText(["log", "--reverse", "--format=%s%x00%b%x00", tag + "..HEAD"]));
    }

    private static List<ReleaseCommit> ReadCommits(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        string[] parts = output
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\0', StringSplitOptions.None);
        List<ReleaseCommit> commits = [];
        for (int index = 0; index + 1 < parts.Length; index += 2)
        {
            string title = parts[index].Trim('\n');
            string body = parts[index + 1].Trim('\n');
            if (title.Length == 0 && body.Length == 0)
            {
                continue;
            }

            commits.Add(new ReleaseCommit(title, body));
        }

        return commits;
    }

    private static string[] Run(IReadOnlyList<string> arguments)
    {
        string output = RunText(arguments);
        return string.IsNullOrWhiteSpace(output)
            ? []
            : output
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string RunText(IReadOnlyList<string> arguments)
    {
        using Process process = new();
        process.StartInfo.FileName = "git";
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
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
        return output;
    }

    private static void EnsureSuccess(int exitCode, string error)
    {
        _ = exitCode == 0
            ? false
            : throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error) ? "The git command failed." : error.Trim());
    }
}
