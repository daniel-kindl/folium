using System.Text;
using System.Text.Json;
using Folium.Books;
using Folium.Storage;

namespace Folium.Tests;

public sealed class LibraryBackupTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-backup-" + Guid.NewGuid().ToString("N"));

    public LibraryBackupTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RoundTripPreservesBooksCoversAndDistinctIds()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        BookMetadata metadata = new(
            "/works/OL1W",
            "/books/OL2M",
            "Ace",
            "1965",
            "9780441172719",
            "First",
            "eng",
            "A desert planet.",
            42,
            new byte[] { 1, 2, 3, 4 });
        Book first = new(firstId, "Dune", "Frank Herbert", ReadingStatus.Reading, metadata);
        Book second = new(secondId, "Dune", "Frank Herbert", ReadingStatus.Finished);
        string backup = LibraryBackup.Export([first, second]);

        IReadOnlyList<Book> restored = LibraryBackup.Read(backup);

        Assert.Equal(2, restored.Count);
        Assert.Equal(firstId, restored[0].Id);
        Assert.Equal(secondId, restored[1].Id);
        Assert.Equal("Dune", restored[0].Title);
        Assert.Equal(ReadingStatus.Reading, restored[0].Status);
        Assert.Equal(ReadingStatus.Finished, restored[1].Status);
        Assert.Equal(metadata with { Cover = default }, restored[0].Metadata! with { Cover = default });
        Assert.Equal(metadata.Cover.ToArray(), restored[0].Metadata!.Cover.ToArray());
        Assert.Null(restored[1].Metadata);
        AssertLiveStoreCanReadTheSameBooks(backup, first, second);
    }

    [Fact]
    public void EmptyBackupRoundTrips()
    {
        string backup = LibraryBackup.Export([]);

        Assert.Empty(LibraryBackup.Read(backup));
        Assert.Contains("\"kind\": \"personal-library-backup\"", backup, StringComparison.Ordinal);
        Assert.Contains("\"version\": 1", backup, StringComparison.Ordinal);
    }

    [Fact]
    public void ToReadStatusSurvivesWhenTheTokenIsPresent()
    {
        Book book = new(Guid.NewGuid(), "Neuromancer", "William Gibson", ReadingStatus.ToRead);

        string backup = LibraryBackup.Export([book]);
        Book restored = Assert.Single(LibraryBackup.Read(backup));

        Assert.Equal(ReadingStatus.ToRead, restored.Status);
        Assert.Contains("\"readingStatus\": \"to-read\"", backup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData(/*lang=json,strict*/ """{"kind":"personal-library-backup","version":1}""")]
    [InlineData(/*lang=json,strict*/ """{"kind":"personal-library-backup","version":1,"books":[null]}""")]
    public void IncompleteBackupIsRejected(string json)
    {
        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(json));

        Assert.Equal("This backup is incomplete.", exception.Message);
    }

    [Fact]
    public void TruncatedBackupIsRejected()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "truncated-backup.json"));

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(json));

        Assert.Equal("This backup is incomplete.", exception.Message);
        _ = Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public void RawLibraryFileIsNotABackup()
    {
        const string JSON = /*lang=json,strict*/ """{"version":1,"books":[]}""";

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(JSON));

        Assert.Equal("This file is not a Folium backup.", exception.Message);
    }

    [Fact]
    public void WrongKindIsRejected()
    {
        const string JSON = /*lang=json,strict*/ """{"kind":"library.json","version":1,"books":[]}""";

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(JSON));

        Assert.Equal("This file is not a Folium backup.", exception.Message);
    }

    [Fact]
    public void UnsupportedVersionIsRejected()
    {
        const string JSON = /*lang=json,strict*/ """{"kind":"personal-library-backup","version":2,"books":[]}""";

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(JSON));

        Assert.Equal("This backup uses version 2. Folium reads version 1.", exception.Message);
    }

    [Fact]
    public void DuplicateIdsAreRejected()
    {
        Guid id = Guid.NewGuid();
        string json = "{\"kind\":\"personal-library-backup\",\"version\":1,\"books\":["
            + "{\"id\":\"" + id + "\",\"title\":\"Dune\",\"author\":\"Frank Herbert\",\"readingStatus\":\"to-read\"},"
            + "{\"id\":\"" + id + "\",\"title\":\"Dune\",\"author\":\"Frank Herbert\",\"readingStatus\":\"reading\"}]}";

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(json));

        Assert.Equal("This backup contains a book Folium cannot save.", exception.Message);
    }

    [Fact]
    public void BlankTitleIsRejected()
    {
        Guid id = Guid.NewGuid();
        string json = BackupWithBook(id, " ", "Frank Herbert", "to-read");

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(json));

        Assert.Equal("This backup contains a book Folium cannot save.", exception.Message);
    }

    [Fact]
    public void UnknownStatusIsRejected()
    {
        string json = BackupWithBook(Guid.NewGuid(), "Dune", "Frank Herbert", "started");

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(json));

        Assert.Equal("This backup contains a book Folium cannot save.", exception.Message);
    }

    [Fact]
    public void EmptyBookIdIsRejected()
    {
        string json = BackupWithBook(Guid.Empty, "Dune", "Frank Herbert", "to-read");

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(json));

        Assert.Equal("This backup contains a book Folium cannot save.", exception.Message);
    }

    [Fact]
    public void ExportRejectsANullList()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => LibraryBackup.Export(null!));

        Assert.Equal("books", exception.ParamName);
    }

    [Fact]
    public void ReadRejectsNullText()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => LibraryBackup.Read(null!));

        Assert.Equal("json", exception.ParamName);
    }

    [Fact]
    public void BackupAtTheSizeLimitRoundTrips()
    {
        Guid id = Guid.NewGuid();
        Book seed = new(id, "Dune", "Frank Herbert", ReadingStatus.ToRead, MetadataWithDescription(""));
        int baseLength = Encoding.UTF8.GetByteCount(LibraryBackup.Export([seed]));
        int pad = LibraryBackup.MaximumBytes - baseLength;
        Book padded = new(id, "Dune", "Frank Herbert", ReadingStatus.ToRead, MetadataWithDescription(new string('x', pad)));

        string backup = LibraryBackup.Export([padded]);

        Assert.Equal(32 * 1024 * 1024, LibraryBackup.MaximumBytes);
        Assert.Equal(LibraryBackup.MaximumBytes, Encoding.UTF8.GetByteCount(backup));
        Assert.Equal(pad, Assert.Single(LibraryBackup.Read(backup)).Metadata!.Description.Length);
    }

    [Fact]
    public void BackupOverTheSizeLimitIsRejected()
    {
        string json = new('x', LibraryBackup.MaximumBytes + 1);

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Read(json));

        Assert.Equal("This backup is too large to restore.", exception.Message);
    }

    [Fact]
    public void ExportOverTheSizeLimitDoesNotReturnText()
    {
        Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert", Metadata: MetadataWithDescription(new string('x', 64)));

        LibraryBackupException exception = Assert.Throws<LibraryBackupException>(() => LibraryBackup.Export([book], maximumBytes: 32));

        Assert.Equal("This backup is too large to export.", exception.Message);
    }

    private void AssertLiveStoreCanReadTheSameBooks(string backup, params Book[] expected)
    {
        using JsonDocument document = JsonDocument.Parse(backup);
        string library = "{\"version\":1,\"books\":" + document.RootElement.GetProperty("books").GetRawText() + "}";
        string path = Path.Combine(_directory, "library.json");
        File.WriteAllText(path, library);
        IReadOnlyList<Book> stored = new FileBookStore(path).Read();

        Assert.Equal(expected.Length, stored.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Id, stored[index].Id);
            Assert.Equal(expected[index].Title, stored[index].Title);
            Assert.Equal(expected[index].Author, stored[index].Author);
            Assert.Equal(expected[index].Status, stored[index].Status);
            Assert.Equal(expected[index].Metadata?.CoverId, stored[index].Metadata?.CoverId);
            Assert.Equal(expected[index].Metadata?.WorkKey, stored[index].Metadata?.WorkKey);
            Assert.Equal(expected[index].Metadata?.Cover.ToArray() ?? [], stored[index].Metadata?.Cover.ToArray() ?? []);
        }
    }

    private static BookMetadata MetadataWithDescription(string description)
    {
        return new("/works/OL1W", "/books/OL2M", "Ace", "1965", "9780441172719", "First", "eng", description, CoverId: null, default);
    }

    private static string BackupWithBook(Guid id, string title, string author, string status)
    {
        return "{\"kind\":\"personal-library-backup\",\"version\":1,\"books\":[{\"id\":\""
            + id
            + "\",\"title\":\""
            + title
            + "\",\"author\":\""
            + author
            + "\",\"readingStatus\":\""
            + status
            + "\"}]}";
    }
}
