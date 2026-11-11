namespace Folium.Storage;

internal static class AtomicFile
{
    public static void Write(string path, ReadOnlySpan<byte> bytes)
    {
        string temporary = path + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed cleanup must not hide a write failure or invalidate a committed save.
        }
    }
}
