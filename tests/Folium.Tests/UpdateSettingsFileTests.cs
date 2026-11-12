using Folium.Updates;

namespace Folium.Tests;

public sealed class UpdateSettingsFileTests
{
    [Fact]
    public void RequiresPreferencesWhenWriting()
    {
        using TemporaryDirectory directory = new();
        UpdateSettingsFile file = new(Path.Combine(directory.Path, "updates.json"));

        _ = Assert.Throws<ArgumentNullException>(() => file.Write(null!));
    }

    [Fact]
    public void MissingOrCorruptSettingsUseEnabledDefaults()
    {
        using TemporaryDirectory directory = new();
        UpdateSettingsFile file = new(Path.Combine(directory.Path, "updates.json"));
        Assert.Equal(new UpdateSettings(), file.Read());
        File.WriteAllText(Path.Combine(directory.Path, "updates.json"), "{");
        Assert.Equal(new UpdateSettings(), file.Read());
    }

    [Fact]
    public void WritesAndReadsPreferences()
    {
        using TemporaryDirectory directory = new();
        UpdateSettingsFile file = new(Path.Combine(directory.Path, "updates.json"));
        UpdateSettings settings = new(ChecksEnabled: false, LastCheck: new DateTimeOffset(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero), SkippedVersion: "0.2.0");
        file.Write(settings);
        Assert.Equal(settings, file.Read());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
