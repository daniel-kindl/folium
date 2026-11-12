using System.Text.Json;
using Folium.Storage;

namespace Folium.Updates;

/// <summary>Loads and saves private update preferences.</summary>
/// <param name="path">The settings file path.</param>
public sealed class UpdateSettingsFile(string path)
{
    private readonly string _path = path ?? throw new ArgumentNullException(nameof(path));
    /// <summary>Reads preferences. Missing or invalid files use default settings.</summary>
    public UpdateSettings Read()
    {
        try
        {
            using FileStream stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize<UpdateSettings>(stream) ?? new UpdateSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new UpdateSettings();
        }
    }

    /// <summary>Atomically writes preferences.</summary>
    /// <param name="settings">The preferences to write.</param>
    public void Write(UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(settings);
        AtomicFile.Write(_path, bytes);
    }
}
