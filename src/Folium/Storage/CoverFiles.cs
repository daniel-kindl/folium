using System.Security.Cryptography;

namespace Folium.Storage;

internal sealed class CoverFiles(string directory)
{
    public string Save(ReadOnlySpan<byte> bytes)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string path = Path.Combine(directory, hash);
        _ = Directory.CreateDirectory(directory);
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
        {
            AtomicFile.Write(path, bytes);
        }

        return hash;
    }

    public byte[] Read(string hash)
    {
        if (!IsHash(hash))
        {
            throw new LibraryStoreException("The library contains an invalid saved cover reference.");
        }

        byte[] bytes = File.ReadAllBytes(Path.Combine(directory, hash));
        return string.Equals(hash, Convert.ToHexStringLower(SHA256.HashData(bytes)), StringComparison.Ordinal)
            ? bytes
            : throw new LibraryStoreException("A saved cover file is damaged. Restore a personal library backup.");
    }

    public void Collect(HashSet<string> retained)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                foreach (string path in Directory.EnumerateFiles(directory))
                {
                    string name = Path.GetFileName(path);
                    if (ShouldDelete(name, retained))
                    {
                        AtomicFile.TryDelete(path);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Retry orphan collection after the next successful save.
        }
    }

    private static bool ShouldDelete(string name, HashSet<string> retained)
    {
        return IsTemporary(name) || (IsHash(name) && !retained.Contains(name));
    }

    private static bool IsTemporary(string name)
    {
        return name.EndsWith(".tmp", StringComparison.Ordinal) && IsHash(name[..^4]);
    }

    private static bool IsHash(string value)
    {
        return value.Length == 64 && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}
