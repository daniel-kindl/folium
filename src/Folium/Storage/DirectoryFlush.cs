using System.Runtime.InteropServices;

namespace Folium.Storage;

/// <summary>Flushes a directory entry. File streams cannot open a directory.</summary>
internal static partial class DirectoryFlush
{
    /// <summary>Linux and Android value of O_DIRECTORY. O_RDONLY is zero.</summary>
    private const int OPEN_DIRECTORY = 0x10000;

    /// <summary>Makes the directory entry durable. Returns false when the operating system cannot flush it.</summary>
    /// <param name="directory">The directory that contains the replaced file.</param>
    /// <returns>True when the flush succeeds.</returns>
    public static bool TryFlush(string directory)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsAndroid())
        {
            return false;
        }

        int descriptor = Open(directory, OPEN_DIRECTORY);
        if (descriptor < 0)
        {
            return false;
        }

        try
        {
            return FlushFile(descriptor) == 0;
        }
        finally
        {
            _ = Close(descriptor);
        }
    }

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "fsync", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int FlushFile(int descriptor);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int Close(int descriptor);
}
