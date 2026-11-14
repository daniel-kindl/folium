using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
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
    public void ReplaceRejectsAnUndefinedStatusAndDoesNotCreateFile()
    {
        FileBookStore store = new(LibraryPath);
        Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert", (ReadingStatus)9);

        _ = Assert.Throws<LibraryStoreException>(() => store.Replace(new([book], [])));

        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void ReplaceRejectsAnEmptyBookAndDoesNotCreateFile()
    {
        FileBookStore store = new(LibraryPath);

        _ = Assert.Throws<ArgumentException>(() => store.Replace(new([null!], [])));

        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void ReplaceRejectsAnInvalidShelfAndDoesNotCreateFile()
    {
        FileBookStore store = new(LibraryPath);
        Shelf shelf = new(Guid.NewGuid(), " ");

        _ = Assert.Throws<LibraryStoreException>(() => store.Replace(new([], [shelf])));

        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void ReplaceRejectsInvalidShelfIdsAndDuplicateNames()
    {
        Guid id = Guid.NewGuid();
        Shelf[][] invalidSets =
        [
            [new Shelf(Guid.Empty, "Favourites")],
            [new Shelf(id, "Favourites"), new Shelf(id, "Read again")],
            [new Shelf(Guid.NewGuid(), "Café"), new Shelf(Guid.NewGuid(), "CAFE")],
            [new Shelf(Guid.NewGuid(), " Favourites ")],
        ];

        foreach (Shelf[] shelves in invalidSets)
        {
            FileBookStore store = new(LibraryPath);
            _ = Assert.Throws<LibraryStoreException>(() => store.Replace(new([], shelves)));
            Assert.False(File.Exists(LibraryPath));
        }
    }

    [Fact]
    public void FailedWriteKeepsThePreviousFile()
    {
        string blocked = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(blocked, "x");
        byte[] bytes = File.ReadAllBytes(blocked);
        FileBookStore store = new(Path.Combine(blocked, "library.json"));
        Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert");

        _ = Assert.Throws<LibraryStoreException>(() => store.Replace(new([book], [])));

        Assert.Equal(bytes, File.ReadAllBytes(blocked));
    }

    [Fact]
    public void VersionTwoLibraryMigratesToVersionThreeAndKeepsAnExactCopy()
    {
        Guid bookId = Guid.NewGuid();
        string legacy = $$"""{"version":2,"books":[{"id":"{{bookId}}","title":"Dune","author":"Frank Herbert"}]}""";
        File.WriteAllText(LibraryPath, legacy);
        FileBookStore store = new(LibraryPath);
        LibrarySnapshot snapshot = store.Read();

        Assert.Empty(snapshot.Shelves);
        store.Replace(snapshot);

        Assert.Equal(Encoding.UTF8.GetBytes(legacy), File.ReadAllBytes(LibraryPath + ".v2.bak"));
        Assert.Equal(3, JsonDocument.Parse(File.ReadAllText(LibraryPath)).RootElement.GetProperty("version").GetInt32());
        Assert.Equal(bookId, Assert.Single(store.Read().Books).Id);
    }

    [Fact]
    public void UnsupportedLibraryVersionIsRejected()
    {
        File.WriteAllText(LibraryPath, /*lang=json,strict*/ """{"version":4,"shelves":[],"books":[]}""");

        LibraryStoreException exception = Assert.Throws<LibraryStoreException>(() => new FileBookStore(LibraryPath).Read());

        Assert.Contains("uses version 4. Folium reads versions 1, 2, and 3.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionThreeDropsUnknownShelfReferencesAndSortsShelvesByFoldedName()
    {
        Guid bookId = Guid.NewGuid();
        Guid zetaId = Guid.NewGuid();
        Guid cafeId = Guid.NewGuid();
        Guid appleId = Guid.NewGuid();
        Guid missingId = Guid.NewGuid();
        string json = $$"""{"version":3,"shelves":[{"id":"{{zetaId}}","name":"Zeta"},{"id":"{{cafeId}}","name":"Café"},{"id":"{{appleId}}","name":"Apple"}],"books":[{"id":"{{bookId}}","title":"Dune","author":"Frank Herbert","shelfIds":["{{zetaId}}","{{missingId}}","{{cafeId}}"]}]}""";
        File.WriteAllText(LibraryPath, json);

        LibrarySnapshot snapshot = new FileBookStore(LibraryPath).Read();

        Assert.Equal(["Apple", "Café", "Zeta"], snapshot.Shelves.Select(shelf => shelf.Name), StringComparer.Ordinal);
        FrozenSet<Guid> shelfIds = Assert.Single(snapshot.Books).ShelfIds;
        Assert.Equal(2, shelfIds.Count);
        Assert.Contains(cafeId, (IReadOnlySet<Guid>)shelfIds);
        Assert.Contains(zetaId, (IReadOnlySet<Guid>)shelfIds);
        Assert.DoesNotContain(missingId, (IReadOnlySet<Guid>)shelfIds);
    }

    [Fact]
    public void ReplaceWritesShelvesAndMembershipIdsInStableOrder()
    {
        Guid lowerId = new(0x11111111, 0x1111, 0x1111, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11);
        Guid higherId = new(0x22222222, 0x2222, 0x2222, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22);
        Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert", shelfIds: [higherId, lowerId]);
        LibrarySnapshot snapshot = new([book], [new(lowerId, "Zeta"), new(higherId, "Apple")]);

        new FileBookStore(LibraryPath).Replace(snapshot);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(LibraryPath));
        JsonElement root = document.RootElement;
        Assert.Equal(["Apple", "Zeta"], root.GetProperty("shelves").EnumerateArray()
            .Select(shelf => shelf.GetProperty("name").GetString()), StringComparer.Ordinal);
        Assert.Equal([lowerId, higherId], root.GetProperty("books")[0].GetProperty("shelfIds").EnumerateArray()
            .Select(id => id.GetGuid()));
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"version":3,"shelves":[null],"books":[]}""", "contains an invalid shelf id.")]
    [InlineData(/*lang=json,strict*/ """{"version":3,"shelves":[{"id":"00000000-0000-0000-0000-000000000000","name":"Favourites"}],"books":[]}""", "contains an invalid shelf id.")]
    [InlineData(/*lang=json,strict*/ """{"version":3,"shelves":[{"id":"11111111-1111-1111-1111-111111111111","name":"Favourites"},{"id":"11111111-1111-1111-1111-111111111111","name":"Read again"}],"books":[]}""", "contains an invalid shelf id.")]
    [InlineData(/*lang=json,strict*/ """{"version":3,"shelves":[{"id":"11111111-1111-1111-1111-111111111111","name":" "}],"books":[]}""", "contains an invalid shelf name.")]
    [InlineData(/*lang=json,strict*/ """{"version":3,"shelves":[{"id":"11111111-1111-1111-1111-111111111111","name":" Favourites "}],"books":[]}""", "contains a duplicate or untrimmed shelf name.")]
    [InlineData(/*lang=json,strict*/ """{"version":3,"shelves":[{"id":"11111111-1111-1111-1111-111111111111","name":"Café"},{"id":"22222222-2222-2222-2222-222222222222","name":"CAFE"}],"books":[]}""", "contains a duplicate or untrimmed shelf name.")]
    public void InvalidShelfInVersionThreeLibraryIsRejected(string json, string error)
    {
        File.WriteAllText(LibraryPath, json);

        LibraryStoreException exception = Assert.Throws<LibraryStoreException>(() => new FileBookStore(LibraryPath).Read());

        Assert.Contains(error, exception.Message, StringComparison.Ordinal);
    }
}
