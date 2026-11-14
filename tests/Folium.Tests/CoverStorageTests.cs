using System.Runtime.Versioning;
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
        store.Replace(new([book], []));
        string cover = Assert.Single(Directory.GetFiles(Covers));
        DateTime stamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(cover, stamp);
        store.Replace(new([book with { Status = ReadingStatus.Finished }], []));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(cover));
        Assert.Equal(book.Metadata!.Cover.ToArray(), File.ReadAllBytes(cover));
        Assert.DoesNotContain("AQID", File.ReadAllText(LibraryPath), StringComparison.Ordinal);
        Book reopened = Assert.Single(new FileBookStore(LibraryPath).Read().Books);
        Assert.Equal(ReadingStatus.Finished, reopened.Status);
        Assert.Equal(book.Metadata.Cover.ToArray(), reopened.Metadata!.Cover.ToArray());
    }

    [Fact]
    public void ShelfEditsDoNotRewriteUnchangedCovers()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture], []));
        PersonalLibrary library = new(store);
        string cover = Assert.Single(Directory.GetFiles(Covers));
        byte[] contents = File.ReadAllBytes(cover);
        DateTime stamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(cover, stamp);

        Shelf shelf = library.CreateShelf("Favourites");
        AssertCoverUnchanged(cover, contents, stamp);
        _ = library.SetShelves(FixtureId, [shelf.Id]);
        AssertCoverUnchanged(cover, contents, stamp);
        _ = library.RenameShelf(shelf.Id, "Keepers");
        AssertCoverUnchanged(cover, contents, stamp);
        library.DeleteShelf(shelf.Id);
        AssertCoverUnchanged(cover, contents, stamp);
    }

    [Fact]
    public void LegacyMigrationRetainsExactRollbackAndPortableBackup()
    {
        Book book = Fixture;
        WriteLegacy(book);
        byte[] legacy = File.ReadAllBytes(LibraryPath);
        FileBookStore store = new(LibraryPath);
        Book original = Assert.Single(store.Read().Books);
        Assert.Equal(legacy, File.ReadAllBytes(LibraryPath));
        store.Replace(new([original], []));
        Assert.Equal(legacy, File.ReadAllBytes(LibraryPath + ".v1.bak"));
        Assert.Equal(3, JsonDocument.Parse(File.ReadAllText(LibraryPath)).RootElement.GetProperty("version").GetInt32());
        Book restored = Assert.Single(LibraryBackup.Read(LibraryBackup.ExportSnapshot(store.Read())));
        Assert.Equal(book with { Metadata = null }, restored with { Metadata = null });
        Assert.Equal(book.Metadata! with { Cover = default }, restored.Metadata! with { Cover = default });
        Assert.Equal(book.Metadata.Cover.ToArray(), restored.Metadata.Cover.ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MigrationRollbackRetainsCoversAfterBookDeleteOrReplacement(bool migrateFirst, bool replaceCover)
    {
        WriteLegacyV2(Fixture);
        FileBookStore store = new(LibraryPath);
        Book original = Assert.Single(store.Read().Books);
        byte[] originalCover = original.Metadata!.Cover.ToArray();
        if (migrateFirst)
        {
            store.Replace(new([original], []));
        }

        string orphan = Path.Combine(Covers, new string('a', 64));
        File.WriteAllBytes(orphan, [9]);
        if (replaceCover)
        {
            Book replacement = original with { Metadata = original.Metadata with { Cover = new byte[] { 4, 5, 6 } } };
            store.Replace(new([replacement], []));
        }
        else
        {
            store.Replace(new([], []));
        }

        Assert.False(File.Exists(orphan));
        File.Copy(LibraryPath + ".v2.bak", LibraryPath, overwrite: true);

        Book rolledBack = Assert.Single(new FileBookStore(LibraryPath).Read().Books);
        Assert.Equal(FixtureId, rolledBack.Id);
        Assert.Equal(originalCover, rolledBack.Metadata!.Cover.ToArray());
    }

    [Theory]
    [InlineData((int)RollbackDocumentCase.MalformedJson)]
    [InlineData((int)RollbackDocumentCase.NullDocument)]
    [InlineData((int)RollbackDocumentCase.WrongVersion)]
    [InlineData((int)RollbackDocumentCase.MissingBooks)]
    [InlineData((int)RollbackDocumentCase.NullBook)]
    public void InvalidMigrationBackupSkipsCoverCollection(int documentCase)
    {
        WriteLegacyV2(Fixture);
        FileBookStore store = new(LibraryPath);
        Book original = Assert.Single(store.Read().Books);
        store.Replace(new([original], []));
        string originalCoverPath = Path.Combine(
            Covers,
            Convert.ToHexStringLower(SHA256.HashData(Fixture.Metadata!.Cover.Span)));
        File.WriteAllText(LibraryPath + ".v2.bak", InvalidRollbackDocument((RollbackDocumentCase)documentCase));
        Book replacement = original with { Metadata = original.Metadata! with { Cover = new byte[] { 4, 5, 6 } } };

        store.Replace(new([replacement], []));

        Assert.True(File.Exists(originalCoverPath));
        Assert.Equal(new byte[] { 4, 5, 6 }, Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
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
        Assert.Equal(Fixture.Id, Assert.Single(store.Read().Books).Id);
        Directory.Delete(LibraryPath + ".tmp");
        store.Replace(store.Read());
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void FailedMetadataCommitKeepsOldCoversAndLaterRemovesOrphans()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture], []));
        byte[] previous = File.ReadAllBytes(LibraryPath);
        Book replacement = Fixture with { Metadata = Fixture.Metadata! with { Cover = new byte[] { 4, 5, 6 } } };
        _ = Directory.CreateDirectory(LibraryPath + ".tmp");
        _ = Assert.Throws<LibraryStoreException>(() => store.Replace(new([replacement], [])));
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
        Directory.Delete(LibraryPath + ".tmp");
        store.Replace(new([replacement], []));
        _ = Assert.Single(Directory.GetFiles(Covers));
        Assert.Equal(new byte[] { 4, 5, 6 }, Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
        store.Replace(new([], []));
        Assert.Empty(Directory.GetFiles(Covers));
        Assert.Empty(store.Read().Books);
    }

    [Fact]
    public void InterruptedCoverWriteLeavesPreviousLibraryUsable()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture with { Metadata = null }], []));
        byte[] previous = File.ReadAllBytes(LibraryPath);
        _ = Directory.CreateDirectory(Covers);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Fixture.Metadata!.Cover.Span));
        _ = Directory.CreateDirectory(Path.Combine(Covers, hash + ".tmp"));
        _ = Assert.Throws<LibraryStoreException>(() => store.Replace(new([Fixture], [])));
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.Null(Assert.Single(store.Read().Books).Metadata);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingOrCorruptCoverFailsWithoutChangingSavedData(bool missing)
    {
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture], []));
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
        AssertCoverUnchanged(cover, missing);
    }

    [Fact]
    public void OrphanedBlobsAndInterruptedTemporaryFilesAreCollectedAfterCommit()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture], []));
        string orphan = Path.Combine(Covers, new string('a', 64));
        File.WriteAllBytes(orphan, [9]);
        File.WriteAllBytes(orphan + ".tmp", [8]);
        string unrelated = Path.Combine(Covers, "unknown-file");
        File.WriteAllBytes(unrelated, [7]);
        store.Replace(new([Fixture], []));
        Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(unrelated));
        Assert.False(File.Exists(orphan));
        Assert.False(File.Exists(orphan + ".tmp"));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void FullDiskDuringCoverWriteKeepsPreviousLibrary()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The disk-full device exists only on Linux.");
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture with { Metadata = null }], []));
        byte[] previous = File.ReadAllBytes(LibraryPath);
        _ = Directory.CreateDirectory(Covers);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Fixture.Metadata!.Cover.Span));
        _ = File.CreateSymbolicLink(Path.Combine(Covers, hash + ".tmp"), "/dev/full");
        LibraryStoreException error = Assert.Throws<LibraryStoreException>(() => store.Replace(new([Fixture], [])));
        _ = Assert.IsType<IOException>(error.InnerException, exactMatch: false);
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.Null(Assert.Single(store.Read().Books).Metadata);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APortableBackupCanRepairMissingOrCorruptCovers(bool missing)
    {
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture], []));
        string backup = LibraryBackup.ExportSnapshot(store.Read());
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
        store.Replace(LibraryBackup.ReadSnapshot(backup));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoadErrorAllowsConfirmedBackupRecoveryWithoutWritingOnPreview(bool missing)
    {
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture], []));
        string backup = LibraryBackup.ExportSnapshot(store.Read());
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
        AssertCoverUnchanged(cover, missing);
        viewModel.KeepLibraryCommand.Execute(parameter: null);
        Assert.True(viewModel.ShowLoadError);
        Assert.Equal(document, File.ReadAllBytes(LibraryPath));
        AssertCoverUnchanged(cover, missing);
        viewModel.PrepareRestore(backup);
        viewModel.ConfirmRestoreCommand.Execute(parameter: null);
        Assert.True(viewModel.ShowLibrary);
        Assert.False(viewModel.ShowLoadError);
        Assert.Equal(Fixture.Id, Assert.Single(viewModel.Books).Id);
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void SharedCoversRemainUntilTheLastReferenceIsRemoved()
    {
        FileBookStore store = new(LibraryPath);
        Book second = Fixture with { Id = Guid.NewGuid() };
        store.Replace(new([Fixture, second], []));
        _ = Assert.Single(Directory.GetFiles(Covers));
        store.Replace(new([second], []));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
        store.Replace(new([second with { Metadata = second.Metadata! with { Cover = default } }], []));
        Assert.Empty(Directory.GetFiles(Covers));
        Assert.True(Assert.Single(store.Read().Books).Metadata!.Cover.IsEmpty);
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
        store.Replace(new([Fixture], []));
        byte[] previous = File.ReadAllBytes(LibraryPath);
        _ = File.CreateSymbolicLink(LibraryPath + ".tmp", "/dev/full");
        _ = Assert.Throws<LibraryStoreException>(() => store.Replace(new([], [])));
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void InvalidCoverReferenceCannotReadOutsideTheCoverDirectory()
    {
        FileBookStore store = new(LibraryPath);
        store.Replace(new([Fixture], []));
        string document = File.ReadAllText(LibraryPath);
        string hash = Path.GetFileName(Assert.Single(Directory.GetFiles(Covers)));
        File.WriteAllText(LibraryPath, document.Replace(hash, "../outside", StringComparison.Ordinal));
        LibraryStoreException error = Assert.Throws<LibraryStoreException>(store.Read);
        Assert.Equal("The library contains an invalid saved cover reference.", error.Message);
    }

    [Fact]
    public void CleanupWaitsWhenTheLibraryDirectoryCannotBeFlushed()
    {
        FileBookStore store = new(LibraryPath, _ => false);
        store.Replace(new([Fixture], []));
        string orphan = Path.Combine(Covers, new string('a', 64));
        File.WriteAllBytes(orphan, [9]);
        store.Replace(new([Fixture], []));
        Assert.True(File.Exists(orphan));
        Assert.Equal(Fixture.Metadata!.Cover.ToArray(), Assert.Single(store.Read().Books).Metadata!.Cover.ToArray());
    }

    [Fact]
    public void UnreadableVersion1StaysInPlace()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Unix file modes are required.");
        }

        WriteLegacy(Fixture with { Metadata = null });
        byte[] previous = File.ReadAllBytes(LibraryPath);
        if (OperatingSystem.IsLinux())
        {
            DenyRead(LibraryPath);
        }

        try
        {
            FileBookStore store = new(LibraryPath);
            _ = Assert.Throws<LibraryStoreException>(() => store.Replace(new([], [])));
        }
        finally
        {
            if (OperatingSystem.IsLinux())
            {
                AllowRead(LibraryPath);
            }
        }

        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        Assert.False(File.Exists(LibraryPath + ".v1.bak"));
    }

    [SupportedOSPlatform("linux")]
    private static void DenyRead(string path)
    {
        File.SetUnixFileMode(path, UnixFileMode.UserWrite);
    }

    [SupportedOSPlatform("linux")]
    private static void AllowRead(string path)
    {
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public void ConfirmedBackupReplacesVersion1WithAnUnusableStatus()
    {
        WriteLegacy(Fixture with { Metadata = null }, "later");
        byte[] previous = File.ReadAllBytes(LibraryPath);
        string backup = LibraryBackup.Export([Fixture with { Metadata = null }]);
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(LibraryPath));

        Assert.True(viewModel.ShowLoadError);
        viewModel.PrepareRestore(backup);
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        viewModel.ConfirmRestoreCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal(FixtureId, Assert.Single(viewModel.Books).Id);
        Assert.False(File.Exists(LibraryPath + ".v1.bak"));
        Assert.Equal(FixtureId, Assert.Single(new FileBookStore(LibraryPath).Read().Books).Id);
    }

    [Fact]
    public void ConfirmedBackupReplacesJsonThatDoesNotDescribeALibrary()
    {
        _ = Directory.CreateDirectory(_directory);
        File.WriteAllText(LibraryPath, "null");
        byte[] previous = File.ReadAllBytes(LibraryPath);
        string backup = LibraryBackup.Export([Fixture with { Metadata = null }]);
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(LibraryPath));

        Assert.True(viewModel.ShowLoadError);
        viewModel.PrepareRestore(backup);
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        viewModel.KeepLibraryCommand.Execute(parameter: null);
        Assert.Equal(previous, File.ReadAllBytes(LibraryPath));
        viewModel.PrepareRestore(backup);
        viewModel.ConfirmRestoreCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
        Assert.False(File.Exists(LibraryPath + ".v1.bak"));
        Assert.Equal(FixtureId, Assert.Single(new FileBookStore(LibraryPath).Read().Books).Id);
    }

    private static void AssertCoverUnchanged(string cover, byte[] contents, DateTime stamp)
    {
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(cover));
        Assert.Equal(contents, File.ReadAllBytes(cover));
    }

    private static void AssertCoverUnchanged(string cover, bool missing)
    {
        if (missing)
        {
            Assert.False(File.Exists(cover));
            return;
        }

        Assert.Equal([9], File.ReadAllBytes(cover));
    }

    private void WriteLegacy(Book book, string readingStatus = "reading")
    {
        _ = Directory.CreateDirectory(_directory);
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(new
        {
            version = 1,
            books = new[] { new { book.Id, book.Title, book.Author, readingStatus, book.Metadata } },
        }, JsonOptions));
    }

    private void WriteLegacyV2(Book book)
    {
        _ = Directory.CreateDirectory(_directory);
        byte[] cover = book.Metadata!.Cover.ToArray();
        string coverFile = Convert.ToHexStringLower(SHA256.HashData(cover));
        _ = Directory.CreateDirectory(Covers);
        File.WriteAllBytes(Path.Combine(Covers, coverFile), cover);
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(new
        {
            version = 2,
            books = new[]
            {
                new
                {
                    book.Id,
                    book.Title,
                    book.Author,
                    readingStatus = "reading",
                    metadata = book.Metadata with { Cover = default },
                    coverFile,
                },
            },
        }, JsonOptions));
    }

    private static string InvalidRollbackDocument(RollbackDocumentCase documentCase)
    {
        return documentCase switch
        {
            RollbackDocumentCase.MalformedJson => "{",
            RollbackDocumentCase.NullDocument => "null",
            RollbackDocumentCase.WrongVersion => JsonSerializer.Serialize(new { version = 1, books = Array.Empty<object>() }, JsonOptions),
            RollbackDocumentCase.MissingBooks => JsonSerializer.Serialize(new { version = 2, books = (object[]?)null }, JsonOptions),
            RollbackDocumentCase.NullBook => JsonSerializer.Serialize(new { version = 2, books = new object?[] { null } }, JsonOptions),
            _ => throw new ArgumentOutOfRangeException(nameof(documentCase)),
        };
    }

    private enum RollbackDocumentCase
    {
        MalformedJson = 0,
        NullDocument = 1,
        WrongVersion = 2,
        MissingBooks = 3,
        NullBook = 4,
    }
}
