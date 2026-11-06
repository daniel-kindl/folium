using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class LibraryViewModelTests : IDisposable
{
    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (string directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EmptyLibraryExplainsThatNoBooksAreSaved()
    {
        LibraryViewModel viewModel = Open();

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
        LibraryViewModel viewModel = Open();
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = "   ";
        viewModel.AuthorInput = "Frank Herbert";

        viewModel.SaveCommand.Execute(parameter: null);

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
        LibraryViewModel viewModel = Open();
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = "Dune";
        viewModel.AuthorInput = " ";

        viewModel.SaveCommand.Execute(parameter: null);

        Assert.Equal("Enter an author.", viewModel.InputError);
        Assert.Equal("Dune", viewModel.TitleInput);
        Assert.Empty(viewModel.Books);
    }

    [Fact]
    public void AddRejectsBlankTitleAndAuthor()
    {
        LibraryViewModel viewModel = Open();
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = "";
        viewModel.AuthorInput = "";

        viewModel.SaveCommand.Execute(parameter: null);

        Assert.Equal("Enter a title and an author.", viewModel.InputError);
        Assert.Empty(viewModel.Books);
    }

    [Fact]
    public void AddShowsTheSavedBookAfterRestart()
    {
        (LibraryViewModel viewModel, string path) = OpenAtPath();
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = "  Dune  ";
        viewModel.AuthorInput = "  Frank Herbert  ";

        viewModel.SaveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
        Assert.False(viewModel.IsEmpty);
        Assert.True(viewModel.ShowBooks);
        Book saved = Assert.Single(viewModel.Books);
        Assert.Equal("Dune", saved.Title);
        Assert.Equal("Frank Herbert", saved.Author);

        Book reopened = Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books);
        Assert.Equal(saved, reopened);
    }

    [Fact]
    public void AddAllowsTwoBooksWithTheSameTitleAndAuthor()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        Add(viewModel, "Dune", "Frank Herbert");

        Assert.Equal(2, viewModel.Books.Count);
        Assert.NotEqual(viewModel.Books[0].Id, viewModel.Books[1].Id);
    }

    [Fact]
    public void OpenShowsTheSelectedBook()
    {
        LibraryViewModel viewModel = Open();
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
        LibraryViewModel viewModel = Open();

        viewModel.OpenCommand.Execute(Guid.NewGuid());

        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public void EditSavesTheNewTitleAndAuthor()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.EditCommand.Execute(parameter: null);
        viewModel.TitleInput = " Dune Messiah ";

        viewModel.SaveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Dune Messiah", viewModel.SelectedTitle);
        Assert.Equal("Frank Herbert", viewModel.SelectedAuthor);
        Assert.Equal("Dune Messiah", Assert.Single(viewModel.Books).Title);
    }

    [Fact]
    public void CancelEditKeepsTheSavedBook()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.EditCommand.Execute(parameter: null);
        viewModel.TitleInput = "Changed";

        viewModel.CancelCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Dune", viewModel.SelectedTitle);
        Assert.Equal("Dune", Assert.Single(viewModel.Books).Title);
    }

    [Fact]
    public void KeepBookDoesNotRemoveIt()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.AskRemoveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowConfirm);
        Assert.Equal("Remove this book?", viewModel.Heading);
        Assert.Equal("Folium deletes this book from your library.", viewModel.ConfirmMessage);
        Assert.Equal("Dune", viewModel.SelectedTitle);

        viewModel.KeepBookCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowDetails);
        _ = Assert.Single(viewModel.Books);
    }

    [Fact]
    public void ConfirmRemoveDeletesTheBookPermanently()
    {
        (LibraryViewModel viewModel, string path) = OpenAtPath();
        Add(viewModel, "Dune", "Frank Herbert");
        Add(viewModel, "Neuromancer", "William Gibson");
        Guid removeId = viewModel.Books[0].Id;
        viewModel.OpenCommand.Execute(removeId);
        viewModel.AskRemoveCommand.Execute(parameter: null);

        viewModel.ConfirmRemoveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal("Neuromancer", Assert.Single(viewModel.Books).Title);
        Assert.Equal("Neuromancer", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
        Assert.DoesNotContain(viewModel.Books, book => book.Id == removeId);
    }

    [Fact]
    public void BackMovesFromConfirmDetailsAndEditorToThePreviousScreen()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.AskRemoveCommand.Execute(parameter: null);

        Assert.True(viewModel.GoBack());
        Assert.True(viewModel.ShowDetails);
        _ = Assert.Single(viewModel.Books);

        viewModel.EditCommand.Execute(parameter: null);
        viewModel.TitleInput = "Changed";
        Assert.True(viewModel.GoBack());
        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Dune", viewModel.SelectedTitle);

        Assert.True(viewModel.GoBack());
        Assert.True(viewModel.ShowLibrary);

        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = "Neuromancer";
        Assert.True(viewModel.GoBack());
        Assert.True(viewModel.ShowLibrary);
        _ = Assert.Single(viewModel.Books);
        Assert.False(viewModel.GoBack());
    }

    [Fact]
    public void UnreadableLibraryDoesNotOfferChanges()
    {
        string path = NewPath();
        File.WriteAllText(path, "{");
        byte[] bytes = File.ReadAllBytes(path);

        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(path));

        Assert.True(viewModel.ShowLoadError);
        Assert.False(viewModel.ShowLibrary);
        Assert.False(viewModel.IsEmpty);
        Assert.Equal("Folium could not open your library. The saved file is unchanged.", viewModel.LoadError);
        viewModel.ShowAddCommand.Execute(parameter: null);
        Assert.False(viewModel.ShowEditor);
        Assert.False(viewModel.GoBack());
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void FailedSaveKeepsTheEnteredTextAndTheSavedBooks()
    {
        string path = NewPath();
        LibraryViewModel viewModel = LibraryViewModel.Open(new FailOnSecondWriteStore(new FileBookStore(path)));
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = "Neuromancer";
        viewModel.AuthorInput = "William Gibson";

        viewModel.SaveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowEditor);
        Assert.Equal("Neuromancer", viewModel.TitleInput);
        Assert.Equal("William Gibson", viewModel.AuthorInput);
        Assert.Equal("Folium could not save this book. The text is still in the form.", viewModel.SaveError);
        Assert.Equal("Dune", Assert.Single(viewModel.Books).Title);
        Assert.Equal("Dune", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
    }

    [Fact]
    public void EditScreenIsNamedEditBook()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.EditCommand.Execute(parameter: null);

        Assert.Equal("Edit book", viewModel.Heading);
        Assert.True(viewModel.ShowEditor);
    }

    [Fact]
    public void EditWithoutABookStaysOnTheLibrary()
    {
        LibraryViewModel viewModel = Open();

        viewModel.EditCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public void OpenIgnoresAValueThatIsNotABookId()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");

        viewModel.OpenCommand.Execute(parameter: "Dune");

        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public void SaveFromTheLibraryDoesNotAddABook()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");

        viewModel.SaveCommand.Execute(parameter: null);

        Assert.Equal("Dune", Assert.Single(viewModel.Books).Title);
        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public void AskRemoveWithoutABookStaysOnTheLibrary()
    {
        LibraryViewModel viewModel = Open();

        viewModel.AskRemoveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public void ConfirmRemoveWithoutABookStaysOnTheLibrary()
    {
        LibraryViewModel viewModel = Open();

        viewModel.ConfirmRemoveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public void FailedRemoveKeepsTheBookOnTheConfirmScreen()
    {
        string path = NewPath();
        LibraryViewModel viewModel = LibraryViewModel.Open(new FailOnSecondWriteStore(new FileBookStore(path)));
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.AskRemoveCommand.Execute(parameter: null);

        viewModel.ConfirmRemoveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowConfirm);
        Assert.Equal("Folium could not remove this book.", viewModel.SaveError);
        Assert.Equal("Dune", Assert.Single(viewModel.Books).Title);
        Assert.Equal("Dune", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
    }

    [Fact]
    public void TypingClearsAnInputError()
    {
        LibraryViewModel viewModel = Open();
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.SaveCommand.Execute(parameter: null);

        viewModel.TitleInput = "Dune";

        Assert.Null(viewModel.InputError);
        Assert.Equal("Dune", viewModel.TitleInput);
    }

    [Fact]
    public void BackClearsAnInputError()
    {
        LibraryViewModel viewModel = Open();
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.SaveCommand.Execute(parameter: null);

        Assert.True(viewModel.GoBack());

        Assert.Null(viewModel.InputError);
        Assert.True(viewModel.ShowLibrary);
    }

    [Fact]
    public void ScreenChangeNotifiesTheCommands()
    {
        LibraryViewModel viewModel = Open();
        int changes = 0;
        viewModel.ShowAddCommand.CanExecuteChanged += (_, _) => changes++;

        viewModel.ShowAddCommand.Execute(parameter: null);

        Assert.True(changes > 0);
        Assert.True(viewModel.ShowEditor);
    }

    [Fact]
    public void TitleInputNotifiesTheView()
    {
        LibraryViewModel viewModel = Open();
        string? propertyName = null;
        viewModel.PropertyChanged += (_, args) => propertyName = args.PropertyName;

        viewModel.TitleInput = "Dune";

        Assert.Equal(nameof(LibraryViewModel.TitleInput), propertyName);
    }

    [Fact]
    public void NewBookStartsAsToRead()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");

        Assert.Equal("To read", Assert.Single(viewModel.ShownBooks).Status);
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        Assert.Equal("To read", viewModel.SelectedStatus);
        Assert.Equal(ReadingStatus.ToRead, Assert.Single(viewModel.Books).Status);
    }

    [Fact]
    public void DetailsChangeStatusAndRestartShowsIt()
    {
        (LibraryViewModel viewModel, string path) = OpenAtPath();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.SetStatusCommand.Execute("finished");

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Finished", viewModel.SelectedStatus);
        LibraryViewModel reopened = LibraryViewModel.Open(new FileBookStore(path));
        reopened.OpenCommand.Execute(reopened.Books[0].Id);
        Assert.Equal("Finished", reopened.SelectedStatus);
        Assert.Equal("Finished", Assert.Single(reopened.ShownBooks).Status);
    }

    [Fact]
    public void FilterShowsBooksWithTheSelectedStatus()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        Add(viewModel, "Neuromancer", "William Gibson");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.SetStatusCommand.Execute("reading");
        Assert.True(viewModel.GoBack());

        viewModel.FilterCommand.Execute("reading");

        BookRow row = Assert.Single(viewModel.ShownBooks);
        Assert.Equal("Dune", row.Title);
        Assert.Equal("Frank Herbert", row.Author);
        Assert.Equal("Reading", row.Status);
        Assert.Equal(2, viewModel.Books.Count);
        Assert.Equal("Reading", viewModel.FilterLabel);
        Assert.True(viewModel.ShowFilterLabel);
        Assert.False(viewModel.ShowNoMatches);
        Assert.Null(viewModel.NoMatchMessage);
    }

    [Fact]
    public void FilterWithNoMatchesExplainsTheEmptyStatus()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");

        viewModel.FilterCommand.Execute("finished");

        Assert.Empty(viewModel.ShownBooks);
        Assert.False(viewModel.IsEmpty);
        Assert.False(viewModel.ShowBooks);
        Assert.Null(viewModel.EmptyMessage);
        Assert.True(viewModel.ShowNoMatches);
        Assert.Equal("No books with this status.", viewModel.NoMatchMessage);
        Assert.Equal("Finished", viewModel.FilterLabel);
    }

    [Fact]
    public void AllFilterShowsEveryBook()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        Add(viewModel, "Neuromancer", "William Gibson");
        viewModel.FilterCommand.Execute("finished");

        viewModel.FilterCommand.Execute("all");

        Assert.Equal(2, viewModel.ShownBooks.Count);
        Assert.Null(viewModel.FilterLabel);
        Assert.False(viewModel.ShowFilterLabel);
        Assert.False(viewModel.ShowNoMatches);
    }

    [Fact]
    public void UnknownFilterKeepsTheCurrentList()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.FilterCommand.Execute("reading");

        viewModel.FilterCommand.Execute("started");

        Assert.Empty(viewModel.ShownBooks);
        Assert.Equal("Reading", viewModel.FilterLabel);
    }

    [Fact]
    public void UnknownStatusCommandKeepsTheSavedStatus()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.SetStatusCommand.Execute("started");

        Assert.Equal("To read", viewModel.SelectedStatus);
        Assert.Equal(ReadingStatus.ToRead, Assert.Single(viewModel.Books).Status);
        Assert.True(viewModel.ShowDetails);
        Assert.Null(viewModel.SaveError);
    }

    [Fact]
    public void SetStatusFromTheLibraryDoesNothing()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");

        viewModel.SetStatusCommand.Execute("finished");

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal(ReadingStatus.ToRead, Assert.Single(viewModel.Books).Status);
    }

    [Fact]
    public void FailedStatusChangeStaysOnDetails()
    {
        string path = NewPath();
        LibraryViewModel viewModel = LibraryViewModel.Open(new FailOnSecondWriteStore(new FileBookStore(path)));
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.SetStatusCommand.Execute("finished");

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("To read", viewModel.SelectedStatus);
        Assert.Equal("Folium could not save this status.", viewModel.SaveError);
        Assert.Equal(ReadingStatus.ToRead, Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Status);
    }

    [Fact]
    public void MissingBookDuringStatusChangeReturnsToTheLibrary()
    {
        string path = NewPath();
        LibraryViewModel viewModel = LibraryViewModel.Open(new MissingOnSecondWriteStore(new FileBookStore(path)));
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.SetStatusCommand.Execute("reading");

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal("Folium could not find that book.", viewModel.SaveError);
        Assert.Equal(ReadingStatus.ToRead, Assert.Single(viewModel.Books).Status);
    }

    [Fact]
    public void EditKeepsTheReadingStatusOnTheDetailsScreen()
    {
        LibraryViewModel viewModel = Open();
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);
        viewModel.SetStatusCommand.Execute("reading");
        viewModel.EditCommand.Execute(parameter: null);
        viewModel.TitleInput = "Dune Messiah";

        viewModel.SaveCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Reading", viewModel.SelectedStatus);
        Assert.Equal("Dune Messiah", viewModel.SelectedTitle);
        Assert.Equal(ReadingStatus.Reading, Assert.Single(viewModel.Books).Status);
    }

    private LibraryViewModel Open()
    {
        return OpenAtPath().ViewModel;
    }

    private (LibraryViewModel ViewModel, string Path) OpenAtPath()
    {
        string path = NewPath();
        return (LibraryViewModel.Open(new FileBookStore(path)), path);
    }

    private string NewPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "folium-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        _directories.Add(directory);
        return Path.Combine(directory, "library.json");
    }

    private static void Add(LibraryViewModel viewModel, string title, string author)
    {
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = title;
        viewModel.AuthorInput = author;
        viewModel.SaveCommand.Execute(parameter: null);
    }

    private sealed class MissingOnSecondWriteStore(IBookStore store) : IBookStore
    {
        private int _writes;

        public IReadOnlyList<Book> Read()
        {
            return store.Read();
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            _writes++;
            if (_writes > 1)
            {
                throw new MissingBookException(books[0].Id);
            }

            store.Replace(books);
        }
    }

    private sealed class FailOnSecondWriteStore(IBookStore store) : IBookStore
    {
        private int _writes;

        public IReadOnlyList<Book> Read()
        {
            return store.Read();
        }

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
