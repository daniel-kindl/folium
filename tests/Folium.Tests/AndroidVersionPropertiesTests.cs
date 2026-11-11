using System.Diagnostics;
using System.Text.Json;

namespace Folium.Tests;

public sealed class AndroidVersionPropertiesTests
{
    [Theory]
    [InlineData("0.0.1", "1")]
    [InlineData("0.1.0", "100")]
    [InlineData("0.99.99", "9999")]
    [InlineData("1.0.0", "10000")]
    [InlineData("1.0.1", "10001")]
    [InlineData("1.2.3", "10203")]
    [InlineData("1.99.99", "19999")]
    [InlineData("2.0.0", "20000")]
    [InlineData("10.20.30", "102030")]
    [InlineData("209999.99.99", "2099999999")]
    public void MsBuildComputesDisplayVersionAndVersionCode(string version, string versionCode)
    {
        MsBuildVersionProperties properties = Evaluate(version);

        Assert.Equal(0, properties.ExitCode);
        Assert.Equal(version, properties.DisplayVersion);
        Assert.Equal(versionCode, properties.ApplicationVersion);
    }

    [Theory]
    [InlineData("1.100.0", "minor must be below 100")]
    [InlineData("1.0.100", "patch must be below 100")]
    [InlineData("0.100.0", "minor must be below 100")]
    [InlineData("210000.0.0", "major must be at most 209999")]
    [InlineData("1.2", "strict SemVer X.Y.Z")]
    [InlineData("1.2.3.4", "strict SemVer X.Y.Z")]
    [InlineData("1.2.3-beta", "strict SemVer X.Y.Z")]
    [InlineData("v1.2.3", "strict SemVer X.Y.Z")]
    [InlineData("01.2.3", "strict SemVer X.Y.Z")]
    public void MsBuildRejectsInvalidOrOutOfRangeVersions(string version, string errorFragment)
    {
        MsBuildVersionProperties properties = Evaluate(version);

        Assert.NotEqual(0, properties.ExitCode);
        Assert.Contains(errorFragment, properties.Error, StringComparison.Ordinal);
    }

    private static MsBuildVersionProperties Evaluate(string foliumVersion)
    {
        string project = Path.Combine(RepositoryRoot(), "src", "Folium.Android", "Folium.Android.csproj");
        using Process process = new();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add(project);
        process.StartInfo.ArgumentList.Add("-nologo");
        process.StartInfo.ArgumentList.Add("-t:ValidateFoliumVersion");
        process.StartInfo.ArgumentList.Add("-getProperty:ApplicationVersion");
        process.StartInfo.ArgumentList.Add("-getProperty:ApplicationDisplayVersion");
        process.StartInfo.ArgumentList.Add("-p:FoliumVersion=" + foliumVersion);
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = RepositoryRoot();
        _ = process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("The MSBuild version evaluation did not finish within two minutes.");
        }

        return new MsBuildVersionProperties(process.ExitCode, output, error);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Folium.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root was not found from the test output directory.");
    }

    private sealed class MsBuildVersionProperties
    {
        public MsBuildVersionProperties(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Error = error;
            int start = output.IndexOf('{', StringComparison.Ordinal);
            if (start < 0)
            {
                DisplayVersion = string.Empty;
                ApplicationVersion = string.Empty;
                return;
            }

            using JsonDocument document = JsonDocument.Parse(output[start..]);
            JsonElement properties = document.RootElement.GetProperty("Properties");
            DisplayVersion = properties.GetProperty("ApplicationDisplayVersion").GetString() ?? string.Empty;
            ApplicationVersion = properties.GetProperty(nameof(ApplicationVersion)).GetString() ?? string.Empty;
        }

        public int ExitCode { get; }

        public string DisplayVersion { get; }

        public string ApplicationVersion { get; }

        public string Error { get; }
    }
}
