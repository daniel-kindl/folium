using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Folium.Books;
using Folium.Storage;

namespace Folium.ViewModels;

/// <summary>The screen state for the personal library.</summary>
public sealed class LibraryViewModel : INotifyPropertyChanged
{
    private readonly PersonalLibrary? _library;
    private Screen _screen = Screen.Library;
    private bool _editing;
    private Book? _selected;
    private ReadingStatus? _filter;
    private string _titleInput = "";
    private string _authorInput = "";

    private LibraryViewModel(PersonalLibrary? library)
    {
        _library = library;
        if (library is not null)
        {
            Books = library.Books;
            ShownBooks = Rows(library.Books);
        }

        ShowAddCommand = new Command(_ => ShowAdd(), _ => ShowLibrary);
        SaveCommand = new Command(_ => Save(), _ => ShowEditor);
        CancelCommand = new Command(_ => GoBack(), _ => ShowEditor);
        OpenCommand = new Command(Open, _ => ShowLibrary);
        EditCommand = new Command(_ => ShowEdit(), _ => ShowDetails);
        AskRemoveCommand = new Command(_ => AskRemove(), _ => ShowDetails);
        ConfirmRemoveCommand = new Command(_ => ConfirmRemove(), _ => ShowConfirm);
        KeepBookCommand = new Command(_ => ShowDetailsScreen(), _ => ShowConfirm);
        SetStatusCommand = new Command(SetStatus, _ => ShowDetails);
        FilterCommand = new Command(ApplyFilter, _ => ShowLibrary);
    }

    /// <summary>Tells the view that a bound value changed.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Opens the add-book screen.</summary>
    public ICommand ShowAddCommand { get; }

    /// <summary>Saves the book from the editor.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Leaves the editor without a save.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Opens the book whose identifier is the command parameter.</summary>
    public ICommand OpenCommand { get; }

    /// <summary>Opens the editor for the selected book.</summary>
    public ICommand EditCommand { get; }

    /// <summary>Opens the remove confirmation for the selected book.</summary>
    public ICommand AskRemoveCommand { get; }

    /// <summary>Removes the selected book after confirmation.</summary>
    public ICommand ConfirmRemoveCommand { get; }

    /// <summary>Leaves the remove confirmation and keeps the book.</summary>
    public ICommand KeepBookCommand { get; }

    /// <summary>Sets the reading status of the selected book. The parameter is to-read, reading, or finished.</summary>
    public ICommand SetStatusCommand { get; }

    /// <summary>Shows all books or one reading status. The parameter is all, to-read, reading, or finished.</summary>
    public ICommand FilterCommand { get; }

    /// <summary>The name of the current screen.</summary>
    public string Heading => _screen switch
    {
        Screen.Add => "Add a book",
        Screen.Edit => "Edit book",
        Screen.Details => "Book",
        Screen.ConfirmRemove => "Remove this book?",
        Screen.Library => "Library",
        _ => "Library",
    };

    /// <summary>The books on the library screen.</summary>
    public IReadOnlyList<Book> Books { get; private set; } = [];

    /// <summary>The books that match the current filter.</summary>
    public IReadOnlyList<BookRow> ShownBooks { get; private set; } = [];

    /// <summary>True when the library screen is open and has no books.</summary>
    public bool IsEmpty => ShowLibrary && Books.Count == 0;

    /// <summary>True when the library screen shows at least one book.</summary>
    public bool ShowBooks => ShowLibrary && ShownBooks.Count > 0;

    /// <summary>True when the library has books and the filter matches none.</summary>
    public bool ShowNoMatches => ShowLibrary && Books.Count > 0 && ShownBooks.Count == 0;

    /// <summary>The message when the filter matches no books. Null when a book is visible or the library is empty.</summary>
    public string? NoMatchMessage => ShowNoMatches ? "No books with this status." : null;

    /// <summary>The active filter in the words shown on the list. Null when the list shows every book.</summary>
    public string? FilterLabel => _filter is null ? null : StatusText(_filter.Value);

    /// <summary>True when the library screen shows one reading status.</summary>
    public bool ShowFilterLabel => ShowLibrary && _filter is not null;

    /// <summary>The empty-library message. Null when the library has a book or another screen is open.</summary>
    public string? EmptyMessage => IsEmpty ? "No books yet." : null;

    /// <summary>True when the library is open and the library screen is current.</summary>
    public bool ShowLibrary => _library is not null && _screen == Screen.Library;

    /// <summary>True when the library is open and the add screen or the edit screen is current.</summary>
    public bool ShowEditor => _library is not null && _screen is Screen.Add or Screen.Edit;

    /// <summary>True when the library is open and the book details screen is current.</summary>
    public bool ShowDetails => _library is not null && _screen == Screen.Details;

    /// <summary>True when the library is open and the remove confirmation is current.</summary>
    public bool ShowConfirm => _library is not null && _screen == Screen.ConfirmRemove;

    /// <summary>True when the library file did not open.</summary>
    public bool ShowLoadError => _library is null;

    /// <summary>The message when the library file did not open. Null when the library is open.</summary>
    public string? LoadError => ShowLoadError
        ? "Folium could not open your library. The saved file is unchanged."
        : null;

    /// <summary>The title of the selected book. Empty when no book is selected.</summary>
    public string SelectedTitle => _selected?.Title ?? "";

    /// <summary>The author of the selected book. Empty when no book is selected.</summary>
    public string SelectedAuthor => _selected?.Author ?? "";

    /// <summary>The reading status of the selected book. Empty when no book is selected.</summary>
    public string SelectedStatus => _selected is null ? "" : StatusText(_selected.Status);

    /// <summary>The confirm view binds to this member on the view-model instance.</summary>
    public string ConfirmMessage { get; } = "Folium deletes this book from your library.";

    /// <summary>The message for invalid title or author text. Null when the text is valid.</summary>
    public string? InputError { get; private set; }

    /// <summary>The message when a save or a removal fails. Null after a successful change.</summary>
    public string? SaveError { get; private set; }

    /// <summary>The title text in the editor. A change clears the input error and the save error.</summary>
    public string TitleInput
    {
        get => _titleInput;
        set => SetInput(ref _titleInput, value, nameof(TitleInput));
    }

    /// <summary>The author text in the editor. A change clears the input error and the save error.</summary>
    public string AuthorInput
    {
        get => _authorInput;
        set => SetInput(ref _authorInput, value, nameof(AuthorInput));
    }

    /// <summary>Opens the library from the store. A store failure leaves the library closed and the file unchanged.</summary>
    /// <param name="store">The store that holds the saved books. Must not be null.</param>
    public static LibraryViewModel Open(IBookStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        try
        {
            return new LibraryViewModel(new PersonalLibrary(store));
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            return new LibraryViewModel(library: null);
        }
    }

    /// <summary>Moves to the previous screen. Returns false on the library screen so the host can leave the app.</summary>
    public bool GoBack()
    {
        if (ShowConfirm || (_editing && ShowEditor))
        {
            ShowDetailsScreen();
            return true;
        }

        if (ShowEditor || ShowDetails)
        {
            ShowLibraryScreen();
            return true;
        }

        return false;
    }

    private void ShowAdd()
    {
        _editing = false;
        _selected = null;
        SetText(ref _titleInput, "", nameof(TitleInput));
        SetText(ref _authorInput, "", nameof(AuthorInput));
        ClearErrors();
        SetScreen(Screen.Add);
    }

    private void ShowEdit()
    {
        if (_selected is null)
        {
            return;
        }

        _editing = true;
        SetText(ref _titleInput, _selected.Title, nameof(TitleInput));
        SetText(ref _authorInput, _selected.Author, nameof(AuthorInput));
        ClearErrors();
        SetScreen(Screen.Edit);
    }

    private void Open(object? parameter)
    {
        if (parameter is not Guid id || _library is null)
        {
            return;
        }

        _selected = Books.FirstOrDefault(book => book.Id == id);
        if (_selected is null)
        {
            return;
        }

        _editing = false;
        ClearErrors();
        SetScreen(Screen.Details);
    }

    private void Save()
    {
        if (_library is not PersonalLibrary library)
        {
            return;
        }

        SaveFromEditor(library);
    }

    private void SaveFromEditor(PersonalLibrary library)
    {
        if (!ShowEditor)
        {
            return;
        }

        try
        {
            StoreEditorBook(library);
        }
        catch (BookInputException exception)
        {
            ShowInputError(exception);
        }
        catch (MissingBookException exception)
        {
            ShowMissingBook(library, exception);
        }
        catch (LibraryStoreException exception)
        {
            ShowStoreSaveError(exception);
        }
    }

    private void StoreEditorBook(PersonalLibrary library)
    {
        if (_editing && _selected is null)
        {
            return;
        }

        Book saved = _editing
            ? library.Edit(_selected!.Id, _titleInput, _authorInput)
            : library.Add(_titleInput, _authorInput);
        UseBooks(library.Books);
        ClearErrors();
        if (_editing)
        {
            _selected = saved;
            ShowDetailsScreen();
            return;
        }

        ShowLibraryScreen();
    }

    private void ShowInputError(BookInputException exception)
    {
        SaveError = null;
        InputError = Describe(exception);
        Raise(nameof(SaveError));
        Raise(nameof(InputError));
    }

    private void ShowMissingBook(PersonalLibrary library, MissingBookException exception)
    {
        Trace.TraceError(exception.ToString());
        UseBooks(library.Books);
        ShowLibraryScreen();
        SaveError = "Folium could not find that book.";
        Raise(nameof(SaveError));
    }

    private void ShowStoreSaveError(LibraryStoreException exception)
    {
        Trace.TraceError(exception.ToString());
        InputError = null;
        SaveError = "Folium could not save this book. The text is still in the form.";
        Raise(nameof(InputError));
        Raise(nameof(SaveError));
    }

    private void AskRemove()
    {
        if (_selected is null)
        {
            return;
        }

        SetScreen(Screen.ConfirmRemove);
    }

    private void ConfirmRemove()
    {
        if (_library is null || _selected is null)
        {
            return;
        }

        try
        {
            _library.Remove(_selected.Id);
            UseBooks(_library.Books);
            ShowLibraryScreen();
        }
        catch (MissingBookException exception)
        {
            Trace.TraceError(exception.ToString());
            UseBooks(_library.Books);
            SaveError = "Folium could not find that book.";
            Raise(nameof(SaveError));
            ShowLibraryScreen();
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            SaveError = "Folium could not remove this book.";
            Raise(nameof(SaveError));
        }
    }

    private void ShowLibraryScreen()
    {
        _editing = false;
        _selected = null;
        ClearErrors();
        SetScreen(Screen.Library);
    }

    private void ShowDetailsScreen()
    {
        _editing = false;
        ClearErrors();
        SetScreen(Screen.Details);
    }

    private void ClearErrors()
    {
        if (InputError is null && SaveError is null)
        {
            return;
        }

        InputError = null;
        SaveError = null;
        Raise(nameof(InputError));
        Raise(nameof(SaveError));
    }

    private void SetScreen(Screen screen)
    {
        _screen = screen;
        Raise(nameof(Heading));
        Raise(nameof(ShowLibrary));
        Raise(nameof(ShowEditor));
        Raise(nameof(ShowDetails));
        Raise(nameof(ShowConfirm));
        Raise(nameof(SelectedTitle));
        Raise(nameof(SelectedAuthor));
        Raise(nameof(SelectedStatus));
        Raise(nameof(LoadError));
        RaiseLibraryShape();
        RaiseCommands();
    }

    private void SetStatus(object? parameter)
    {
        if (_library is not PersonalLibrary library || _selected is null || parameter is not string token)
        {
            return;
        }

        if (!TryStatus(token, out ReadingStatus status))
        {
            return;
        }

        try
        {
            Book saved = library.SetStatus(_selected.Id, status);
            UseBooks(library.Books);
            _selected = saved;
            ClearErrors();
            Raise(nameof(SelectedStatus));
        }
        catch (MissingBookException exception)
        {
            ShowMissingBook(library, exception);
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            SaveError = "Folium could not save this status.";
            Raise(nameof(SaveError));
        }
    }

    private void ApplyFilter(object? parameter)
    {
        if (!TryFilter(parameter, out ReadingStatus? status))
        {
            return;
        }

        _filter = status;
        ShownBooks = Rows(Books);
        Raise(nameof(ShownBooks));
        RaiseLibraryShape();
    }

    private void UseBooks(IReadOnlyList<Book> books)
    {
        Books = books;
        ShownBooks = Rows(books);
        Raise(nameof(Books));
        Raise(nameof(ShownBooks));
        RaiseLibraryShape();
    }

    private void RaiseLibraryShape()
    {
        Raise(nameof(IsEmpty));
        Raise(nameof(ShowBooks));
        Raise(nameof(EmptyMessage));
        Raise(nameof(ShowNoMatches));
        Raise(nameof(NoMatchMessage));
        Raise(nameof(FilterLabel));
        Raise(nameof(ShowFilterLabel));
    }

    private List<BookRow> Rows(IReadOnlyList<Book> books)
    {
        List<BookRow> rows = [];
        foreach (Book book in books)
        {
            if (Shows(book))
            {
                rows.Add(new BookRow(book.Id, book.Title, book.Author, StatusText(book.Status)));
            }
        }

        return rows;
    }

    private bool Shows(Book book)
    {
        return _filter is null || book.Status == _filter.Value;
    }

    private static bool TryFilter(object? parameter, out ReadingStatus? status)
    {
        status = null;
        if (parameter is not string token)
        {
            return false;
        }

        if (string.Equals(token, "all", StringComparison.Ordinal))
        {
            return true;
        }

        if (!TryStatus(token, out ReadingStatus parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }

    private static bool TryStatus(string token, out ReadingStatus status)
    {
        switch (token)
        {
            case "to-read":
                status = ReadingStatus.ToRead;
                return true;
            case "reading":
                status = ReadingStatus.Reading;
                return true;
            case "finished":
                status = ReadingStatus.Finished;
                return true;
            default:
                status = default;
                return false;
        }
    }

    private static string StatusText(ReadingStatus status)
    {
        return status switch
        {
            ReadingStatus.ToRead => "To read",
            ReadingStatus.Reading => "Reading",
            ReadingStatus.Finished => "Finished",
            _ => throw new ArgumentOutOfRangeException(nameof(status), "The reading status is not a defined value."),
        };
    }

    private void SetInput(ref string field, string value, string propertyName)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        Raise(propertyName);
        if (InputError is not null || SaveError is not null)
        {
            InputError = null;
            SaveError = null;
            Raise(nameof(InputError));
            Raise(nameof(SaveError));
        }
    }

    private void SetText(ref string field, string value, string propertyName)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        Raise(propertyName);
    }

    private void RaiseCommands()
    {
        foreach (ICommand command in new[]
        {
            ShowAddCommand, SaveCommand, CancelCommand, OpenCommand, EditCommand,
            AskRemoveCommand, ConfirmRemoveCommand, KeepBookCommand, SetStatusCommand,
            FilterCommand,
        })
        {
            if (command is Command relay)
            {
                relay.RaiseCanExecuteChanged();
            }
        }
    }

    private void Raise(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string Describe(BookInputException exception)
    {
        bool missingTitle = exception.Problems.Contains(BookInputProblem.MissingTitle);
        bool missingAuthor = exception.Problems.Contains(BookInputProblem.MissingAuthor);
        return (missingTitle, missingAuthor) switch
        {
            (true, true) => "Enter a title and an author.",
            (true, false) => "Enter a title.",
            (false, true) => "Enter an author.",
            _ => "Folium could not save this book.",
        };
    }

    private enum Screen
    {
        Library = 0,
        Add = 1,
        Edit = 2,
        Details = 3,
        ConfirmRemove = 4,
    }

    private sealed class Command(Action<object?> execute, Func<object?, bool> canExecute) : ICommand
    {
        private readonly Action<object?> _execute = execute;
        private readonly Func<object?, bool> _canExecute = canExecute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                _execute(parameter);
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
