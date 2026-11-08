using Folium.Books;
using Folium.Storage;

namespace Folium.Tests;

public sealed class ImportedBookTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-import-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ImportedMetadataAndCoverSurviveRestartAndLocalEdits()
    {
        string path = Path.Combine(_directory, "library.json");
        PersonalLibrary library = new(new FileBookStore(path));
        BookMetadata metadata = new("/works/OL1W", "/books/OL2M", "Ace", "1965", "123", "First", "eng", "Description", 42, new byte[] { 1, 2, 3 });
        Book added = library.Add("Dune", "Frank Herbert", metadata);
        _ = library.Edit(added.Id, "My title", "My author");
        _ = library.SetStatus(added.Id, ReadingStatus.Reading);

        Book saved = Assert.Single(new PersonalLibrary(new FileBookStore(path)).Books);

        Assert.Equal("My title", saved.Title);
        Assert.Equal("My author", saved.Author);
        Assert.Equal(ReadingStatus.Reading, saved.Status);
        Assert.NotNull(saved.Metadata);
        Assert.Equal(metadata with { Cover = default }, saved.Metadata with { Cover = default });
        Assert.Equal(metadata.Cover.ToArray(), saved.Metadata.Cover.ToArray());
    }

    [Fact]
    public void MetadataCanBeEditedAndClearedWithoutChangingSourceIdentifiers()
    {
        string path = Path.Combine(_directory, "library.json");
        PersonalLibrary library = new(new FileBookStore(path));
        BookMetadata original = new("/works/OL1W", "/books/OL2M", "Ace", "1965", "123", "First", "eng", "Description", CoverId: null, default);
        Book added = library.Add("Dune", "Frank Herbert", original);
        _ = library.Edit(added.Id, "Dune", "Frank Herbert", original with { Publisher = "", Description = "Personal note" });

        Book saved = Assert.Single(new PersonalLibrary(new FileBookStore(path)).Books);

        Assert.Equal("", saved.Metadata!.Publisher);
        Assert.Equal("Personal note", saved.Metadata.Description);
        Assert.Equal("/works/OL1W", saved.Metadata.WorkKey);
        Assert.Equal("/books/OL2M", saved.Metadata.EditionKey);
        Assert.True(saved.Metadata.Cover.IsEmpty);
    }
}
