using Folium.Books;
using Folium.Search;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class BookImportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-import-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportReviewsEditsAndPersistsMetadataAndPermittedCoverOffline()
    {
        LibraryViewModel model = await PreviewAsync(new Provider()).ConfigureAwait(true);
        Assert.True(model.ImportCommand.CanExecute(parameter: null));
        model.ImportCommand.Execute(parameter: null);
        Assert.True(model.ShowEditor);
        Assert.Empty(model.Books);
        Assert.Equal("Dune", model.TitleInput);
        Assert.Equal("Frank Herbert", model.AuthorInput);
        Assert.Equal("Ace", model.MetadataEditor!.Publisher);
        Assert.False(model.MetadataEditor.KeepCover);
        model.MetadataEditor.KeepCover = true;
        model.MetadataEditor.Publisher = "My publisher";
        model.TitleInput = "My title";
        model.SaveCommand.Execute(parameter: null);

        LibraryViewModel restarted = LibraryViewModel.Open(new FileBookStore(LibraryPath));
        Book book = Assert.Single(restarted.Books);
        Assert.Equal("My title", book.Title);
        Assert.Equal("/works/OL1W", book.Metadata!.WorkKey);
        Assert.Equal("/books/OL2M", book.Metadata.EditionKey);
        Assert.Equal("My publisher", book.Metadata.Publisher);
        Assert.Equal("1965", book.Metadata.PublicationDate);
        Assert.Equal("123", book.Metadata.Isbn);
        Assert.Equal("First", book.Metadata.Edition);
        Assert.Equal("eng", book.Metadata.Language);
        Assert.Equal("Description", book.Metadata.Description);
        Assert.Equal(42, book.Metadata.CoverId);
        Assert.Equal(new byte[] { 1, 2, 3 }, book.Metadata.Cover.ToArray());
        restarted.OpenCommand.Execute(book.Id);
        restarted.EditCommand.Execute(parameter: null);
        restarted.MetadataEditor!.Description = "Local description";
        restarted.SaveCommand.Execute(parameter: null);
        Book edited = Assert.Single(LibraryViewModel.Open(new FileBookStore(LibraryPath)).Books);
        Assert.Equal("Local description", edited.Metadata!.Description);
        Assert.Equal(book.Metadata.Cover.ToArray(), edited.Metadata.Cover.ToArray());
        restarted.EditCommand.Execute(parameter: null);
        restarted.MetadataEditor!.KeepCover = false;
        restarted.SaveCommand.Execute(parameter: null);
        Book withoutCover = Assert.Single(LibraryViewModel.Open(new FileBookStore(LibraryPath)).Books);
        Assert.True(withoutCover.Metadata!.Cover.IsEmpty);
        Assert.Equal(42, withoutCover.Metadata.CoverId);
    }

    [Fact]
    public async Task UnconfirmedCoverIsNotSaved()
    {
        LibraryViewModel model = await PreviewAsync(new Provider()).ConfigureAwait(true);
        model.ImportCommand.Execute(parameter: null);
        model.SaveCommand.Execute(parameter: null);

        Assert.True(Assert.Single(model.Books).Metadata!.Cover.IsEmpty);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingMetadataAndMissingOrFailedCoverAllowUserToCompleteAndSaveBook(bool failCover)
    {
        LibraryViewModel model = await PreviewAsync(new Provider { MissingMetadata = true, FailCover = failCover, MissingCover = !failCover }).ConfigureAwait(true);
        model.ImportCommand.Execute(parameter: null);
        model.SaveCommand.Execute(parameter: null);
        Assert.NotNull(model.InputError);
        Assert.Empty(model.Books);
        model.TitleInput = "A title";
        model.AuthorInput = "An author";
        model.SaveCommand.Execute(parameter: null);

        Book saved = Assert.Single(LibraryViewModel.Open(new FileBookStore(LibraryPath)).Books);
        Assert.Equal("A title", saved.Title);
        Assert.Equal("", saved.Metadata!.Publisher);
        Assert.True(saved.Metadata.Cover.IsEmpty);
    }

    [Fact]
    public async Task CancelImportReturnsToPreviewAndDoesNotSaveOrLeakMetadataToManualBook()
    {
        LibraryViewModel model = await PreviewAsync(new Provider()).ConfigureAwait(true);
        model.ImportCommand.Execute(parameter: null);
        model.MetadataEditor!.Publisher = "Discard this";
        model.CancelCommand.Execute(parameter: null);
        Assert.True(model.ShowSearch);
        Assert.True(model.Search.ShowPreview);
        Assert.Empty(model.Books);
        _ = model.GoBack();
        _ = model.GoBack();
        _ = model.GoBack();
        model.ShowAddCommand.Execute(parameter: null);
        Assert.Null(model.MetadataEditor);
        model.TitleInput = "Manual";
        model.AuthorInput = "Author";
        model.SaveCommand.Execute(parameter: null);
        Assert.Null(Assert.Single(model.Books).Metadata);
    }

    private string LibraryPath => Path.Combine(_directory, "library.json");

    private async Task<LibraryViewModel> PreviewAsync(Provider provider)
    {
        LibraryViewModel model = LibraryViewModel.Open(new FileBookStore(LibraryPath), provider);
        Assert.False(model.ImportCommand.CanExecute(parameter: null));
        model.ShowSearchCommand.Execute(parameter: null);
        model.Search.Query = "Dune";
        await model.Search.SearchAsync().ConfigureAwait(true);
        await model.Search.OpenWorkAsync(Assert.Single(model.Search.Works)).ConfigureAwait(true);
        await model.Search.OpenEditionAsync(Assert.Single(model.Search.Editions)).ConfigureAwait(true);
        return model;
    }

    private sealed class Provider : IBookSearch
    {
        public bool MissingMetadata { get; init; }

        public bool FailCover { get; init; }

        public bool MissingCover { get; init; }

        public Task<SearchPage<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchWork>([new("/works/OL1W", "Dune", "Frank Herbert", "1965")], HasAnotherPage: false));
        }

        public Task<SearchPage<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchEdition>([Edition()], HasAnotherPage: false));
        }

        public Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(Edition());
        }

        public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
        {
            return FailCover ? throw new SearchException("Cover failed") : Task.FromResult<byte[]?>(MissingCover ? null : [1, 2, 3]);
        }

        private SearchEdition Edition()
        {
            return MissingMetadata
                ? new("/books/OL2M", "", "", "", "", "", "", "", "", 42)
                : new("/books/OL2M", "Dune", "Frank Herbert", "Ace", "1965", "123", "First", "eng", "Description", 42);
        }
    }
}
