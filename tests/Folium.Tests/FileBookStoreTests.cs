using Folium.Books;
using Folium.Storage;

namespace Folium.Tests;

public sealed class FileBookStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-" + Guid.NewGuid().ToString("N"));

    public FileBookStoreTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string LibraryPath => Path.Combine(_directory, "library.json");

    [Fact]
    public void BlankPathIsRejected()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new FileBookStore(" "));

        Assert.Equal("path", exception.ParamName);
    }

    [Fact]
    public void NullDocumentStaysUnchanged()
    {
        File.WriteAllText(LibraryPath, "null");
        byte[] bytes = File.ReadAllBytes(LibraryPath);
        FileBookStore store = new(LibraryPath);

        _ = Assert.Throws<LibraryStoreException>(store.Read);

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void NullStoredBookStaysUnchanged()
    {
        File.WriteAllText(LibraryPath, /*lang=json,strict*/ """{"version":1,"books":[null]}""");
        byte[] bytes = File.ReadAllBytes(LibraryPath);
        FileBookStore store = new(LibraryPath);

        _ = Assert.Throws<LibraryStoreException>(store.Read);

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void ReplaceRejectsAnEmptyBookAndDoesNotCreateFile()
    {
        FileBookStore store = new(LibraryPath);

        _ = Assert.Throws<ArgumentException>(() => store.Replace([null!]));

        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void FailedWriteKeepsThePreviousFile()
    {
        string blocked = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(blocked, "x");
        byte[] bytes = File.ReadAllBytes(blocked);
        FileBookStore store = new(Path.Combine(blocked, "library.json"));
        Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert");

        _ = Assert.Throws<LibraryStoreException>(() => store.Replace([book]));

        Assert.Equal(bytes, File.ReadAllBytes(blocked));
    }
}
