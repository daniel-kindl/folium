using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using Folium.Books;
using Folium.Search;
using Folium.Storage;

namespace Folium.ViewModels;

/// <summary>The screen state for the personal library.</summary>
public sealed class LibraryViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<string> SortModes = new(StringComparer.Ordinal)
    {
        "title-asc",
        "title-desc",
        "author-asc",
        "author-desc",
    };

    private static readonly Dictionary<Screen, string> Headings = new()
    {
        [Screen.Library] = "Library",
        [Screen.Add] = "Add a book",
        [Screen.Edit] = "Edit book",
        [Screen.Details] = "Book",
        [Screen.ConfirmRemove] = "Remove this book?",
        [Screen.ConfirmRestore] = "Replace your library?",
        [Screen.Search] = "Search",
    };
    private static readonly IBookSearch SearchProvider = new OpenLibrary(new HttpClient());
    private readonly PersonalLibrary? _library;
    private Screen _screen = Screen.Library;
    private bool _editing;
    private bool _importing;
    private Book? _selected;
    private ReadingStatus? _filter;
    private string SearchQueryValue { get; set; } = "";
    private string _sort = "title-asc";
    private string _titleInput = "";
    private IReadOnlyList<Book>? _pendingRestore;
    private string _authorInput = "";

    private LibraryViewModel(PersonalLibrary? library, IBookSearch? search)
    {
        _library = library;
        Search = new SearchViewModel(search ?? SearchProvider);
        if (library is not null)
        {
            Books = library.Books;
            ShownBooks = Rows(library.Books);
        }

        ImportCommand = new Command(_ => BeginImport(), _ => ShowSearch && Search.ShowPreview && Search.Preview is not null && !Search.IsBusy);
        Search.PropertyChanged += (_, _) => ((Command)ImportCommand).RaiseCanExecuteChanged();
        ShowSearchCommand = new Command(_ => SetScreen(Screen.Search), _ => ShowLibrary);
        ShowAddCommand = new Command(_ => ShowAdd(), _ => ShowLibrary);
        SaveCommand = new Command(_ => Save(), _ => ShowEditor);
        CancelCommand = new Command(_ => GoBack(), _ => ShowEditor);
        OpenCommand = new Command(Open, _ => ShowLibrary);
        EditCommand = new Command(_ => ShowEdit(), _ => ShowDetails);
        AskRemoveCommand = new Command(_ => AskRemove(), _ => ShowDetails);
        ConfirmRemoveCommand = new Command(_ => ConfirmRemove(), _ => ShowConfirm);
        KeepBookCommand = new Command(_ => ShowDetailsScreen(), _ => ShowConfirm);
        SetStatusCommand = new Command(SetStatus, _ => ShowDetails);
        ConfirmRestoreCommand = new Command(_ => ConfirmRestore(), _ => ShowRestore);
        KeepLibraryCommand = new Command(_ => CancelRestore(), _ => ShowRestore);
        FilterCommand = new Command(ApplyFilter, _ => ShowLibrary);
        SortCommand = new Command(ApplySort, _ => ShowLibrary);
        ClearSearchCommand = new Command(_ => SearchQuery = "", _ => SearchQuery.Length > 0);
    }

    /// <summary>Tells the view that a bound value changed.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The independent metadata search state.</summary>
    public SearchViewModel Search { get; }

    /// <summary>Reviews the current edition in the book editor.</summary>
    public ICommand ImportCommand { get; }

    /// <summary>The optional edition metadata being edited.</summary>
    public BookMetadataEditor? MetadataEditor { get; private set; }

    /// <summary>The selected book's saved edition metadata.</summary>
    public BookMetadata? SelectedMetadata => _selected?.Metadata;

    /// <summary>True when the selected book contains edition metadata.</summary>
    public bool HasSelectedMetadata => SelectedMetadata is not null;

    /// <summary>The selected book's local cover, or empty bytes.</summary>
    public ReadOnlyMemory<byte> SelectedCover => SelectedMetadata?.Cover ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Opens metadata search.</summary>
    public ICommand ShowSearchCommand { get; }

    /// <summary>True when metadata search is open.</summary>
    public bool ShowSearch => _screen == Screen.Search;

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

    /// <summary>Replaces the personal library with the prepared backup.</summary>
    public ICommand ConfirmRestoreCommand { get; }

    /// <summary>Leaves the restore confirmation and keeps the current books.</summary>
    public ICommand KeepLibraryCommand { get; }

    /// <summary>Shows all books or one reading status. The parameter is all, to-read, reading, or finished.</summary>
    public ICommand FilterCommand { get; }

    /// <summary>Sorts the visible books by title or author. The parameter is title-asc, title-desc, author-asc, or author-desc.</summary>
    public ICommand SortCommand { get; }

    /// <summary>Clears the library search text.</summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>The name of the current screen.</summary>
    public string Heading => Headings[_screen];

    /// <summary>The books on the library screen.</summary>
    public IReadOnlyList<Book> Books { get; private set; } = [];

    /// <summary>The books that match the current filter.</summary>
    public IReadOnlyList<BookRow> ShownBooks { get; private set; } = [];

    /// <summary>The local title or author search text.</summary>
    public string SearchQuery
    {
        get => SearchQueryValue;
        set
        {
            string query = value ?? "";
            if (string.Equals(SearchQueryValue, query, StringComparison.Ordinal))
            {
                return;
            }

            SearchQueryValue = query;
            ShownBooks = Rows(Books);
            Raise(nameof(SearchQuery));
            Raise(nameof(ShowClearSearch));
            Raise(nameof(ShownBooks));
            RaiseLibraryShape();
            ((Command)ClearSearchCommand).RaiseCanExecuteChanged();
        }
    }

    /// <summary>True when the search field contains text that can be cleared.</summary>
    public bool ShowClearSearch => SearchQuery.Length > 0;

    /// <summary>The selected sort mode in the words shown in the control.</summary>
    /// <exception cref="InvalidOperationException">The selected sort mode is not valid.</exception>
    public string SortLabel => _sort switch
    {
        "title-asc" => "Title A–Z",
        "title-desc" => "Title Z–A",
        "author-asc" => "Author A–Z",
        "author-desc" => "Author Z–A",
        _ => throw new InvalidOperationException("The library sort value is not valid."),
    };

    /// <summary>True when the library screen is open and has no books.</summary>
    public bool IsEmpty => ShowLibrary && Books.Count == 0;

    /// <summary>True when saved-book search and sort can apply to at least one book.</summary>
    public bool ShowSavedBookControls => ShowLibrary && Books.Count > 0;

    /// <summary>True when the library is sorted by title from A to Z.</summary>
    public bool IsTitleAscending => string.Equals(_sort, "title-asc", StringComparison.Ordinal);

    /// <summary>True when the library is sorted by title from Z to A.</summary>
    public bool IsTitleDescending => string.Equals(_sort, "title-desc", StringComparison.Ordinal);

    /// <summary>True when the library is sorted by author from A to Z.</summary>
    public bool IsAuthorAscending => string.Equals(_sort, "author-asc", StringComparison.Ordinal);

    /// <summary>True when the library is sorted by author from Z to A.</summary>
    public bool IsAuthorDescending => string.Equals(_sort, "author-desc", StringComparison.Ordinal);

    /// <summary>True when the library screen shows at least one book.</summary>
    public bool ShowBooks => ShowLibrary && ShownBooks.Count > 0;

    /// <summary>True when the library has books and the filter matches none.</summary>
    public bool ShowNoMatches => ShowLibrary && Books.Count > 0 && ShownBooks.Count == 0;

    /// <summary>The message when the query and filter match no books. Null when a book is visible or the library is empty.</summary>
    public string? NoMatchMessage => ShowNoMatches switch
    {
        false => null,
        true when NormalizeSearchText(SearchQuery.Trim()).Length == 0 => "No books with this status.",
        true when _filter is null => "No books match your search.",
        _ => "No books match your search and status.",
    };

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

    /// <summary>True when a backup is waiting for replace confirmation.</summary>
    public bool ShowRestore => _library is not null && _screen == Screen.ConfirmRestore;

    /// <summary>The number of books in the prepared backup. Zero when no backup is waiting.</summary>
    public int RestoreCount => _pendingRestore?.Count ?? 0;

    /// <summary>The titles in the prepared backup. Empty when no backup is waiting.</summary>
    public IReadOnlyList<string> RestoreTitles => _pendingRestore is null ? [] : [.. _pendingRestore.Select(book => book.Title)];

    /// <summary>The replace warning. Empty when no backup is waiting.</summary>
    public string RestoreMessage => _pendingRestore is null
        ? ""
        : string.Create(
            CultureInfo.InvariantCulture,
            $"The backup has {RestoreCount} {(RestoreCount == 1 ? "book" : "books")}. Replacing the library removes the books on this device.");

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

    /// <summary>Writes a backup of the open library. Returns null when the library is closed.</summary>
    /// <returns>The backup text, or null when the library is closed.</returns>
    /// <exception cref="LibraryBackupException">The open library cannot be exported.</exception>
    public string? CreateBackup()
    {
        return _library is null ? null : LibraryBackup.Export(_library.Books);
    }

    /// <summary>Prepares a backup for replace confirmation. An invalid backup leaves the saved books unchanged.</summary>
    /// <param name="json">The backup text.</param>
    /// <exception cref="ArgumentNullException">The text is null. The saved books stay unchanged.</exception>
    public void PrepareRestore(string json)
    {
        if (_library is null)
        {
            return;
        }

        try
        {
            _pendingRestore = LibraryBackup.Read(json);
            ClearErrors();
            SetScreen(Screen.ConfirmRestore);
        }
        catch (LibraryBackupException exception)
        {
            Trace.TraceError(exception.ToString());
            _pendingRestore = null;
            SaveError = exception.Message;
            Raise(nameof(SaveError));
            RaiseRestore();
        }
    }

    /// <summary>Opens the library from the store. A store failure leaves the library closed and the file unchanged.</summary>
    /// <param name="store">The store that holds the saved books. Must not be null.</param>
    /// <param name="search">The metadata provider. Null uses Open Library.</param>
    public static LibraryViewModel Open(IBookStore store, IBookSearch? search = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        try
        {
            return new LibraryViewModel(new PersonalLibrary(store), search);
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            return new LibraryViewModel(library: null, search);
        }
    }

    /// <summary>Moves to the previous screen. Returns false on the library screen so the host can leave the app.</summary>
    public bool GoBack()
    {
        if (ShowSearch)
        {
            if (!Search.GoBack())
            {
                ShowLibraryScreen();
            }

            return true;
        }

        if (ShowEditor)
        {
            CancelEditor();
            return true;
        }

        if (ShowRestore)
        {
            CancelRestore();
            return true;
        }

        if (ShowConfirm)
        {
            ShowDetailsScreen();
            return true;
        }

        if (ShowDetails)
        {
            ShowLibraryScreen();
            return true;
        }

        return false;
    }

    private void CancelEditor()
    {
        if (_importing)
        {
            _importing = false;
            MetadataEditor = null;
            ClearErrors();
            SetScreen(Screen.Search);
        }
        else if (_editing)
        {
            ShowDetailsScreen();
        }
        else
        {
            ShowLibraryScreen();
        }
    }

    private void BeginImport()
    {
        if (Search.Preview is not SearchEdition edition)
        {
            return;
        }

        ShowAdd();
        _importing = true;
        TitleInput = edition.Title;
        AuthorInput = edition.Authors;
        BookMetadata metadata = new(Search.WorkKey, edition.Key, edition.Publisher,
            edition.PublicationDate, edition.Isbn, edition.Edition, edition.Language,
            edition.Description, edition.CoverId, Search.Cover.ToArray());
        MetadataEditor = new BookMetadataEditor(metadata, keepCover: false);
        Raise(nameof(MetadataEditor));
    }

    private void ShowAdd()
    {
        _importing = false;
        MetadataEditor = null;
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
        MetadataEditor = _selected.Metadata is null ? null : new BookMetadataEditor(_selected.Metadata, keepCover: true);
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
            ? library.Edit(_selected!.Id, _titleInput, _authorInput, MetadataEditor?.ToMetadata())
            : library.Add(_titleInput, _authorInput, MetadataEditor?.ToMetadata());
        UseBooks(library.Books);
        ClearErrors();
        if (_editing)
        {
            _selected = saved;
            ShowDetailsScreen();
            return;
        }

        _importing = false;
        MetadataEditor = null;
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
        Raise(nameof(ShowRestore));
        RaiseRestore();
        Raise(nameof(ShowSearch));
        Raise(nameof(SelectedTitle));
        Raise(nameof(SelectedAuthor));
        Raise(nameof(SelectedStatus));
        Raise(nameof(MetadataEditor));
        Raise(nameof(SelectedMetadata));
        Raise(nameof(HasSelectedMetadata));
        Raise(nameof(SelectedCover));
        Raise(nameof(LoadError));
        RaiseLibraryShape();
        RaiseCommands();
    }

    private void SetStatus(object? parameter)
    {
        if (_library is not PersonalLibrary library || !TrySelectedStatus(parameter, out ReadingStatus status))
        {
            return;
        }

        try
        {
            StoreSelectedStatus(library, status);
        }
        catch (MissingBookException exception)
        {
            ShowMissingBook(library, exception);
        }
        catch (LibraryStoreException exception)
        {
            ShowStatusSaveError(exception);
        }
    }

    private bool TrySelectedStatus(object? parameter, out ReadingStatus status)
    {
        status = default;
        return _selected is not null && parameter is string token && TryStatus(token, out status);
    }

    private void StoreSelectedStatus(PersonalLibrary library, ReadingStatus status)
    {
        Book saved = library.SetStatus(_selected!.Id, status);
        UseBooks(library.Books);
        _selected = saved;
        ClearErrors();
        Raise(nameof(SelectedStatus));
    }

    private void ShowStatusSaveError(LibraryStoreException exception)
    {
        Trace.TraceError(exception.ToString());
        SaveError = "Folium could not save this status.";
        Raise(nameof(SaveError));
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

    private void ApplySort(object? parameter)
    {
        if (parameter is not string sort || !SortModes.Contains(sort))
        {
            return;
        }

        if (string.Equals(_sort, sort, StringComparison.Ordinal))
        {
            return;
        }

        _sort = sort;
        ShownBooks = Rows(Books);
        Raise(nameof(SortLabel));
        Raise(nameof(IsTitleAscending));
        Raise(nameof(IsTitleDescending));
        Raise(nameof(IsAuthorAscending));
        Raise(nameof(IsAuthorDescending));
        Raise(nameof(ShownBooks));
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
        Raise(nameof(ShowSavedBookControls));
        Raise(nameof(ShowBooks));
        Raise(nameof(EmptyMessage));
        Raise(nameof(ShowNoMatches));
        Raise(nameof(NoMatchMessage));
        Raise(nameof(FilterLabel));
        Raise(nameof(ShowFilterLabel));
    }

    private List<BookRow> Rows(IReadOnlyList<Book> books)
    {
        string query = NormalizeSearchText(SearchQuery.Trim());
        IEnumerable<Book> matching = books.Where(Shows);
        if (query.Length > 0)
        {
            matching = matching.Where(book => ContainsSearchText(book.Title, query) || ContainsSearchText(book.Author, query));
        }

        bool byTitle = _sort.StartsWith("title", StringComparison.Ordinal);
        bool descending = _sort.EndsWith("desc", StringComparison.Ordinal);
        IOrderedEnumerable<Book> sorted = descending
            ? matching.OrderByDescending(book => FoldedSortKey(book, byTitle), StringComparer.OrdinalIgnoreCase)
                .ThenBy(book => SortText(book, byTitle), StringComparer.Ordinal)
                .ThenBy(book => book.Id)
            : matching.OrderBy(book => FoldedSortKey(book, byTitle), StringComparer.OrdinalIgnoreCase)
                .ThenBy(book => SortText(book, byTitle), StringComparer.Ordinal)
                .ThenBy(book => book.Id);

        return [.. sorted.Select(book => new BookRow(book.Id, book.Title, book.Author, StatusText(book.Status)))];
    }

    private static string FoldedSortKey(Book book, bool byTitle)
    {
        return NormalizeSearchText(SortText(book, byTitle));
    }

    private static string SortText(Book book, bool byTitle)
    {
        return byTitle ? book.Title : book.Author;
    }

    private static bool ContainsSearchText(string value, string query)
    {
        return NormalizeSearchText(value).Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchText(string value)
    {
        StringBuilder normalized = new();
        foreach (Rune rune in WithoutUnpairedSurrogates(value).Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
            {
                _ = normalized.Append(rune.ToString());
            }
        }

        return normalized.ToString();
    }

    private static string WithoutUnpairedSurrogates(string value)
    {
        StringBuilder builder = new(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char unit = value[index];
            if (char.IsHighSurrogate(unit))
            {
                if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    _ = builder.Append(unit).Append(value[index + 1]);
                    index++;
                }

                continue;
            }

            if (!char.IsLowSurrogate(unit))
            {
                builder.Append(unit);
            }
        }

        return builder.ToString();
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
            ConfirmRestoreCommand, KeepLibraryCommand,
            FilterCommand, SortCommand, ClearSearchCommand, ShowSearchCommand, ImportCommand,
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
        Search = 5,
        ConfirmRestore = 6,
    }

    private void ConfirmRestore()
    {
        if (_library is not PersonalLibrary library || _pendingRestore is null)
        {
            return;
        }

        try
        {
            library.ReplaceAll(_pendingRestore);
            _pendingRestore = null;
            UseBooks(library.Books);
            ShowLibraryScreen();
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            SaveError = "Folium could not replace your library. The current books are unchanged.";
            Raise(nameof(SaveError));
        }
    }

    private void CancelRestore()
    {
        _pendingRestore = null;
        ShowLibraryScreen();
    }

    private void RaiseRestore()
    {
        Raise(nameof(RestoreCount));
        Raise(nameof(RestoreTitles));
        Raise(nameof(RestoreMessage));
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
