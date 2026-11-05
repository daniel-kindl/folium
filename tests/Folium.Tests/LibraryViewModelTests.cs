using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class LibraryViewModelTests : IDisposable
{
    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EmptyLibraryExplainsThatNoBooksAreSaved()
    {
        var viewModel = Open();

        Assert.Equal("Library", viewModel.Heading);
        Assert.True(viewModel.ShowLibrary);
        Assert.True(viewModel.IsEmpty);
        Assert.False(viewModel.ShowBooks);
        Assert.Equal("No books yet.", viewModel.EmptyMessage);
        Assert.False(viewModel.ShowEditor);
        Assert.False(viewModel.ShowDetails);
        Assert.False(viewModel.ShowConfirm);
        Assert.False(viewModel.ShowLoadError);
    }

    [Fact]
    public void AddRejectsBlankTitleAndKeepsTheEnteredText()
    {
        var viewModel = Open();
        viewModel.ShowAddCommand.Execute(null);
        viewModel.TitleInput = "   ";
        viewModel.AuthorInput = "Frank Herbert";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Add a book", viewModel.Heading);
        Assert.True(viewModel.ShowEditor);
        Assert.Equal("Enter a title.", viewModel.InputError);
        Assert.Equal("   ", viewModel.TitleInput);
        Assert.Equal("Frank Herbert", viewModel.AuthorInput);
        Assert.Empty(viewModel.Books);
    }

    [Fact]
    public void AddRejectsBlankAuthorAndKeepsTheEnteredText()
    {
        var viewModel = Open();
        viewModel.ShowAddCommand.Execute(null);
        viewModel.TitleInput = "Dune";
        viewModel.AuthorInput = " ";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Enter an author.", viewModel.InputError);
        Assert.Equal("Dune", viewModel.TitleInput);
        Assert.Empty(viewModel.Books);
    }

    [Fact]
    public void AddRejectsBlankTitleAndAuthor()
    {
        var viewModel = Open();
        viewModel.ShowAddCommand.Execute(null);
        viewModel.TitleInput = "";
        viewModel.AuthorInput = "";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Enter a title and an author.", viewModel.InputError);
        Assert.Empty(viewModel.Books);
    }

    [Fact]
    public void AddShowsTheSavedBookAfterRestart()
    {
        var (viewModel, path) = OpenAtPath();
        viewModel.ShowAddCommand.Execute(null);
        viewModel.TitleInput = "  Dune  ";
        viewModel.AuthorInput = "  Frank Herbert  ";

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.ShowLibrary);
        Assert.False(viewModel.IsEmpty);
        Assert.True(viewModel.ShowBooks);
        var saved = Assert.Single(viewModel.Books);
        Assert.Equal("Dune", saved.Title);
        Assert.Equal("Frank Herbert", saved.Author);

        var reopened = Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books);
        Assert.Equal(saved, reopened);
    }

    [Fact]
    public void AddAllowsTwoBooksWithTheSameTitleAndAuthor()
    {
        var viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        Add(viewModel, "Dune", "Frank Herbert");

        Assert.Equal(2, viewModel.Books.Count);
        Assert.NotEqual(viewModel.Books[0].Id, viewModel.Books[1].Id);
    }

    [Fact]
    public void OpenShowsTheSelectedBook()
    {
        var viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");

        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Book", viewModel.Heading);
        Assert.Equal("Dune", viewModel.SelectedTitle);
        Assert.Equal("Frank Herbert", viewModel.SelectedAuthor);
    }

    [Fact]
    public void OpenUnknownBookStaysOnTheLibrary()
    {
        var viewModel = Open();

        viewModel.OpenCommand.Execute(Guid.NewGuid());

        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public void EditSavesTheNewTitleAndAuthor()
    {
        var viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.EditCommand.Execute(null);
        viewModel.TitleInput = " Dune Messiah ";

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Dune Messiah", viewModel.SelectedTitle);
        Assert.Equal("Frank Herbert", viewModel.SelectedAuthor);
        Assert.Equal("Dune Messiah", Assert.Single(viewModel.Books).Title);
    }

    [Fact]
    public void CancelEditKeepsTheSavedBook()
    {
        var viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.EditCommand.Execute(null);
        viewModel.TitleInput = "Changed";

        viewModel.CancelCommand.Execute(null);

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Dune", viewModel.SelectedTitle);
        Assert.Equal("Dune", Assert.Single(viewModel.Books).Title);
    }

    [Fact]
    public void KeepBookDoesNotRemoveIt()
    {
        var viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.AskRemoveCommand.Execute(null);

        Assert.True(viewModel.ShowConfirm);
        Assert.Equal("Remove this book?", viewModel.Heading);
        Assert.Equal("Folium deletes this book from your library.", viewModel.ConfirmMessage);
        Assert.Equal("Dune", viewModel.SelectedTitle);

        viewModel.KeepBookCommand.Execute(null);

        Assert.True(viewModel.ShowDetails);
        Assert.Single(viewModel.Books);
    }

    [Fact]
    public void ConfirmRemoveDeletesTheBookPermanently()
    {
        var (viewModel, path) = OpenAtPath();
        Add(viewModel, "Dune", "Frank Herbert");
        Add(viewModel, "Neuromancer", "William Gibson");
        var removeId = viewModel.Books[0].Id;
        viewModel.OpenCommand.Execute(removeId);
        viewModel.AskRemoveCommand.Execute(null);

        viewModel.ConfirmRemoveCommand.Execute(null);

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal("Neuromancer", Assert.Single(viewModel.Books).Title);
        Assert.Equal("Neuromancer", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
        Assert.DoesNotContain(viewModel.Books, book => book.Id == removeId);
    }

    [Fact]
    public void BackMovesFromConfirmDetailsAndEditorToThePreviousScreen()
    {
        var viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.AskRemoveCommand.Execute(null);

        Assert.True(viewModel.GoBack());
        Assert.True(viewModel.ShowDetails);
        Assert.Single(viewModel.Books);

        viewModel.EditCommand.Execute(null);
        viewModel.TitleInput = "Changed";
        Assert.True(viewModel.GoBack());
        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Dune", viewModel.SelectedTitle);

        Assert.True(viewModel.GoBack());
        Assert.True(viewModel.ShowLibrary);

        viewModel.ShowAddCommand.Execute(null);
        viewModel.TitleInput = "Neuromancer";
        Assert.True(viewModel.GoBack());
        Assert.True(viewModel.ShowLibrary);
        Assert.Single(viewModel.Books);
        Assert.False(viewModel.GoBack());
    }

    [Fact]
    public void UnreadableLibraryDoesNotOfferChanges()
    {
        var path = NewPath();
        File.WriteAllText(path, "{");
        var bytes = File.ReadAllBytes(path);

        var viewModel = LibraryViewModel.Open(new FileBookStore(path));

        Assert.True(viewModel.ShowLoadError);
        Assert.False(viewModel.ShowLibrary);
        Assert.False(viewModel.IsEmpty);
        Assert.Equal("Folium could not open your library. The saved file is unchanged.", viewModel.LoadError);
        viewModel.ShowAddCommand.Execute(null);
        Assert.False(viewModel.ShowEditor);
        Assert.False(viewModel.GoBack());
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void FailedSaveKeepsTheEnteredTextAndTheSavedBooks()
    {
        var path = NewPath();
        var viewModel = LibraryViewModel.Open(new FailOnSecondWriteStore(new FileBookStore(path)));
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.ShowAddCommand.Execute(null);
        viewModel.TitleInput = "Neuromancer";
        viewModel.AuthorInput = "William Gibson";

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.ShowEditor);
        Assert.Equal("Neuromancer", viewModel.TitleInput);
        Assert.Equal("William Gibson", viewModel.AuthorInput);
        Assert.Equal("Folium could not save this book. The text is still in the form.", viewModel.SaveError);
        Assert.Equal("Dune", Assert.Single(viewModel.Books).Title);
        Assert.Equal("Dune", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
    }

    [Fact]
    public void TitleInputNotifiesTheView()
    {
        var viewModel = Open();
        string? propertyName = null;
        viewModel.PropertyChanged += (_, args) => propertyName = args.PropertyName;

        viewModel.TitleInput = "Dune";

        Assert.Equal(nameof(LibraryViewModel.TitleInput), propertyName);
    }

    private LibraryViewModel Open() => OpenAtPath().ViewModel;

    private (LibraryViewModel ViewModel, string Path) OpenAtPath()
    {
        var path = NewPath();
        return (LibraryViewModel.Open(new FileBookStore(path)), path);
    }

    private string NewPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "folium-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _directories.Add(directory);
        return Path.Combine(directory, "library.json");
    }

    private static void Add(LibraryViewModel viewModel, string title, string author)
    {
        viewModel.ShowAddCommand.Execute(null);
        viewModel.TitleInput = title;
        viewModel.AuthorInput = author;
        viewModel.SaveCommand.Execute(null);
    }

    private sealed class FailOnSecondWriteStore(IBookStore store) : IBookStore
    {
        private int _writes;

        public IReadOnlyList<Book> Read() => store.Read();

        public void Replace(IReadOnlyList<Book> books)
        {
            _writes++;
            if (_writes > 1)
            {
                throw new LibraryStoreException("Folium could not write the library file.");
            }

            store.Replace(books);
        }
    }
}
