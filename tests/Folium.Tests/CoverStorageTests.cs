using System.Security.Cryptography;
using System.Text.Json;
using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class CoverStorageTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Guid FixtureId = new(0x528922e1, 0x3829, 0x46bd, 0xbb, 0xc3, 0xdd, 0xa7, 0xca, 0xe8, 0xd7, 0x7c);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-covers-" + Guid.NewGuid().ToString("N"));
    private string LibraryPath => Path.Combine(_directory, "library.json");
    private string Covers => LibraryPath + ".covers";
    private static Book Fixture => new(FixtureId, "Title", "Author", ReadingStatus.Reading,
        new BookMetadata("/works/OL1W", "/books/OL1M", "Publisher", "2001", "isbn", "edition", "eng", "note", 123, new byte[] { 1, 2, 3 }));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void CoversAreSeparateAndStatusEditsDoNotRewriteThem()
    {
        FileBookStore store = new(LibraryPath);
        Book book = Fixture;
        store.Replace([book]);
        string cover = Assert.Single(Directory.GetFiles(Covers));
        DateTime stamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(cover, stamp);
        store.Replace([book with { Status = ReadingStatus.Finished }]);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(cover));
        Assert.Equal(book.Metadata!.Cover.ToArray(), File.ReadAllBytes(cover));
        Assert.DoesNotContain("AQID", File.ReadAllText(LibraryPath), StringComparison.Ordinal);
        Book reopened = Assert.Single(new FileBookStore(LibraryPath).Read());
        Assert.Equal(ReadingStatus.Finished, reopened.Status);
        Assert.Equal(book.Metadata.Cover.ToArray(), reopened.Metadata!.Cover.ToArray());
    }

    [Fact]
    public void LegacyMigrationRetainsExactRollbackAndPortableBackup()
    {
        Book book = Fixture;
        WriteLegacy(book);
        byte[] legacy = File.ReadAllBytes(LibraryPath);
        FileBookStore store = new(LibraryPath);
        Book original = Assert.Single(store.Read());
        Assert.Equal(legacy, File.ReadAllBytes(LibraryPath));
        store.Replace([original]);
        Assert.Equal(legacy, File.ReadAllBytes(LibraryPath + ".v1.bak"));
        Assert.Equal(2, JsonDocument.Parse(File.ReadAllText(LibraryPath)).RootElement.GetProperty("version").GetInt32());
        Book restored = Assert.Single(LibraryBackup.Read(LibraryBackup.Export(store.Read())));
        Assert.Equal(book with { Metadata = null }, restored with { Metadata = null });
        Assert.Equal(book.Metadata! with { Cover = default }, restored.Metadata! with { Cover = default });
        Assert.Equal(book.Metadata.Cover.ToArray(), restored.Metadata.Cover.ToArray());
    }

    [Fact]
    public void InterruptedMigrationCanBeRetriedWithoutLosingLegacyData()
    {
        WriteLegacy(Fixture);
        byte[] legacy = File.ReadAllBytes(LibraryPath);
        _ = Directory.CreateDirectory(LibraryPath + ".tmp");
        FileBookStore store = new(LibraryPath);
        _ = Assert.Throws<LibraryStoreException>(() => store.Replace(store.Read()));
        Assert.Equal(legacy, File.ReadAllBytes(LibraryPath));
        Assert.Equal(Fixture.Id, Assert.Single(store.Read()).Id);
        Directory.Delete(LibraryPath + ".tmp");
        store.Replace(store.Read());
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read()).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void FailedMetadataCommitKeepsOldCoversAndLaterRemovesOrphans()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture]);
        byte[] previous = File.ReadAllBytes(LibraryPath);
        Book replacement = Fixture with { Metadata = Fixture.Metadata! with { Cover = new byte[] { 4, 5, 6 } } };
        _ = Directory.CreateDirectory(LibraryPath + ".tmp");
        _ = Assert.Throws<LibraryStoreException>(() => store.Replace([replacement]));
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read()).Metadata!.Cover.ToArray());
        Directory.Delete(LibraryPath + ".tmp");
        store.Replace([replacement]);
        _ = Assert.Single(Directory.GetFiles(Covers));
        Assert.Equal(new byte[] { 4, 5, 6 }, Assert.Single(store.Read()).Metadata!.Cover.ToArray());
        store.Replace([]);
        Assert.Empty(Directory.GetFiles(Covers));
        Assert.Empty(store.Read());
    }

    [Fact]
    public void InterruptedCoverWriteLeavesPreviousLibraryUsable()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture with { Metadata = null }]);
        byte[] previous = File.ReadAllBytes(LibraryPath);
        _ = Directory.CreateDirectory(Covers);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Fixture.Metadata!.Cover.Span));
        _ = Directory.CreateDirectory(Path.Combine(Covers, hash + ".tmp"));
        _ = Assert.Throws<LibraryStoreException>(() => store.Replace([Fixture]));
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.Null(Assert.Single(store.Read()).Metadata);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingOrCorruptCoverFailsWithoutChangingSavedData(bool missing)
    {
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture]);
        byte[] document = File.ReadAllBytes(LibraryPath);
        string cover = Assert.Single(Directory.GetFiles(Covers));
        if (missing)
        {
            File.Delete(cover);
        }
        else
        {
            File.WriteAllBytes(cover, [9]);
        }

        _ = Assert.Throws<LibraryStoreException>(store.Read);
        Assert.Equal(document, File.ReadAllBytes(LibraryPath));
        File.WriteAllBytes(cover, Fixture.Metadata!.Cover.ToArray());
        Assert.Equal(Fixture.Id, Assert.Single(store.Read()).Id);
    }

    [Fact]
    public void OrphanedBlobsAndInterruptedTemporaryFilesAreCollectedAfterCommit()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture]);
        string orphan = Path.Combine(Covers, new string('a', 64));
        File.WriteAllBytes(orphan, [9]);
        File.WriteAllBytes(orphan + ".tmp", [8]);
        string unrelated = Path.Combine(Covers, "unknown-file");
        File.WriteAllBytes(unrelated, [7]);
        store.Replace([Fixture]);
        Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(unrelated));
        Assert.False(File.Exists(orphan));
        Assert.False(File.Exists(orphan + ".tmp"));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read()).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void FullDiskDuringCoverWriteKeepsPreviousLibrary()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The disk-full device exists only on Linux.");
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture with { Metadata = null }]);
        byte[] previous = File.ReadAllBytes(LibraryPath);
        _ = Directory.CreateDirectory(Covers);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Fixture.Metadata!.Cover.Span));
        _ = File.CreateSymbolicLink(Path.Combine(Covers, hash + ".tmp"), "/dev/full");
        LibraryStoreException error = Assert.Throws<LibraryStoreException>(() => store.Replace([Fixture]));
        _ = Assert.IsType<IOException>(error.InnerException, exactMatch: false);
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.Null(Assert.Single(store.Read()).Metadata);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APortableBackupCanRepairMissingOrCorruptCovers(bool missing)
    {
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture]);
        string backup = LibraryBackup.Export(store.Read());
        string cover = Assert.Single(Directory.GetFiles(Covers));
        if (missing)
        {
            File.Delete(cover);
        }
        else
        {
            File.WriteAllBytes(cover, [9]);
        }
        _ = Assert.Throws<LibraryStoreException>(store.Read);
        store.Replace(LibraryBackup.Read(backup));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read()).Metadata!.Cover.ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoadErrorAllowsConfirmedBackupRecoveryWithoutWritingOnPreview(bool missing)
    {
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture]);
        string backup = LibraryBackup.Export(store.Read());
        byte[] document = File.ReadAllBytes(LibraryPath);
        string cover = Assert.Single(Directory.GetFiles(Covers));
        if (missing)
        {
            File.Delete(cover);
        }
        else
        {
            File.WriteAllBytes(cover, [9]);
        }

        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(LibraryPath));
        Assert.True(viewModel.ShowLoadError);
        viewModel.PrepareRestore(backup);
        Assert.True(viewModel.ShowRestore);
        Assert.Equal(document, File.ReadAllBytes(LibraryPath));
        viewModel.KeepLibraryCommand.Execute(parameter: null);
        Assert.True(viewModel.ShowLoadError);
        Assert.Equal(document, File.ReadAllBytes(LibraryPath));
        viewModel.PrepareRestore(backup);
        viewModel.ConfirmRestoreCommand.Execute(parameter: null);
        Assert.True(viewModel.ShowLibrary);
        Assert.False(viewModel.ShowLoadError);
        Assert.Equal(Fixture.Id, Assert.Single(viewModel.Books).Id);
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read()).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void SharedCoversRemainUntilTheLastReferenceIsRemoved()
    {
        FileBookStore store = new(LibraryPath);
        Book second = Fixture with { Id = Guid.NewGuid() };
        store.Replace([Fixture, second]);
        _ = Assert.Single(Directory.GetFiles(Covers));
        store.Replace([second]);
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read()).Metadata!.Cover.ToArray());
        store.Replace([second with { Metadata = second.Metadata! with { Cover = default } }]);
        Assert.Empty(Directory.GetFiles(Covers));
        Assert.True(Assert.Single(store.Read()).Metadata!.Cover.IsEmpty);
    }

    [Fact]
    public void FailedRollbackCopyPreventsMigration()
    {
        WriteLegacy(Fixture);
        byte[] previous = File.ReadAllBytes(LibraryPath);
        _ = Directory.CreateDirectory(LibraryPath + ".v1.bak.tmp");
        FileBookStore store = new(LibraryPath);
        _ = Assert.Throws<LibraryStoreException>(() => store.Replace(store.Read()));
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.False(Directory.Exists(Covers));
    }

    [Fact]
    public void FullDiskDuringMetadataCommitKeepsOldDocumentAndCover()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The disk-full device exists only on Linux.");
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture]);
        byte[] previous = File.ReadAllBytes(LibraryPath);
        _ = File.CreateSymbolicLink(LibraryPath + ".tmp", "/dev/full");
        _ = Assert.Throws<LibraryStoreException>(() => store.Replace([]));
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read()).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void InvalidCoverReferenceCannotReadOutsideTheCoverDirectory()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace([Fixture]);
        string document = File.ReadAllText(LibraryPath);
        string hash = Path.GetFileName(Assert.Single(Directory.GetFiles(Covers)));
        File.WriteAllText(LibraryPath, document.Replace(hash, "../outside", StringComparison.Ordinal));
        LibraryStoreException error = Assert.Throws<LibraryStoreException>(store.Read);
        Assert.Equal("The library contains an invalid saved cover reference.", error.Message);
    }

    private void WriteLegacy(Book book)
    {
        _ = Directory.CreateDirectory(_directory);
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(new
        {
            version = 1,
            books = new[] { new { book.Id, book.Title, book.Author, readingStatus = "reading", book.Metadata } },
        }, JsonOptions));
    }
}
