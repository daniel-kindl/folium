using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Folium.Books;
using Folium.Search;
using Folium.Storage;
using Folium.Views;

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
        [Screen.ConfirmRemove] = "Delete this book?",
        [Screen.ConfirmRestore] = "Replace your library?",
        [Screen.Search] = "Find online",
        [Screen.Backup] = "Backup and restore",
    };
    private static readonly IBookSearch SearchProvider = new OpenLibrary(new HttpClient());
    private PersonalLibrary? _library;
    private readonly IBookStore _store;
    private Screen _screen = Screen.Library;
    private bool _editing;
    private bool _importing;
    private Book? _selected;
    private ReadingStatus? _filter;
    private string SearchQueryValue { get; set; } = "";
    private string _sort = "title-asc";
    private string _titleInput = "";
    private LibrarySnapshot? _pendingRestore;
    private string _authorInput = "";
    private readonly string? _viewPath;
    private bool AddSheetOpen { get; set; }
    private bool _showLibraryMenu;
    private bool _showSortMenu;
    private bool _showShelfPicker;
    private bool _showSavedSearch;
    private Guid? _snackbarBookId;
    private string SnackbarTitle { get; set; } = "";

    private LibraryViewModel(PersonalLibrary? library, IBookSearch? search, IBookStore store, UpdateViewModel? updates)
    {
        _library = library;
        _store = store;
        if (library is not null)
        {
            AttachShelfState(library);
        }
        Search = new SearchViewModel(search ?? SearchProvider);
        Updates = updates;
        if (library is not null)
        {
            Books = library.Books;
            ShownBooks = Rows(library.Books);
        }

        ImportCommand = new Command(_ => BeginImport(), _ => ShowSearch && Search.ShowPreview && Search.Preview is not null && !Search.IsBusy);
        Search.PropertyChanged += (_, _) =>
        {
            ((Command)ImportCommand).RaiseCanExecuteChanged();
            Raise(nameof(Heading));
            Raise(nameof(AppBarHeading));
            Raise(nameof(ShowTabBar));
            Raise(nameof(ShowBack));
        };
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
        ToggleShelfPickerCommand = new Command(_ => ToggleShelfPicker(), _ => ShowLibrary);
        OpenShelfChecklistCommand = new Command(_ => ShelfState?.OpenChecklist(), _ => ShowDetails);
        ClearSearchCommand = new Command(_ => SearchQuery = "", _ => SearchQuery.Length > 0);
        AssignChromeCommands();
        if (store is FileBookStore files)
        {
            _viewPath = files.LibraryPath + ".view";
            ShowGrid = ReadGridChoice(_viewPath);
        }
    }

    private void AssignChromeCommands()
    {
        ShowAddSheetCommand = new Command(_ => OpenAddSheet(), _ => ShowLibrary && !ShowAddSheet);
        AddByHandCommand = new Command(_ => AddByHand(), _ => ShowLibrary);
        DismissAddSheetCommand = new Command(_ => CloseAddSheet(), _ => ShowAddSheet);
        ShowLibraryMenuCommand = new Command(_ => ToggleLibraryMenu(), _ => ShowLibrary);
        ShowBackupCommand = new Command(_ => ShowBackupScreen(), _ => ShowLibrary);
        ToggleSortMenuCommand = new Command(_ => ToggleSortMenu(), _ => ShowSavedBookControls);
        ToggleSavedSearchCommand = new Command(_ => ToggleSavedSearch(), _ => ShowSavedBookControls);
        ToggleGridCommand = new Command(_ => ToggleGrid(), _ => ShowSavedBookControls);
        DismissSnackbarCommand = new Command(_ => DismissSnackbar(), _ => ShowSnackbar);
        OpenSnackbarCommand = new Command(_ => OpenSnackbar(), _ => ShowSnackbar);
        SelectLibraryCommand = new Command(_ => ShowLibraryScreen(), _ => _library is not null);
        BackCommand = new Command(_ => GoBack(), _ => true);
    }

    /// <summary>Tells the view that a bound value changed.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The independent metadata search state.</summary>
    public SearchViewModel Search { get; }

    /// <summary>The update check and install controls.</summary>
    public UpdateViewModel? Updates { get; }

    /// <summary>Whether this host provides update controls.</summary>
    public bool HasUpdateSettings => Updates is not null;

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

    /// <summary>True when the selected book has a saved cover image.</summary>
    public bool HasSelectedCover => !SelectedCover.IsEmpty;

    /// <summary>The cover tone for the selected book. Empty selection uses sage.</summary>
    public string SelectedTone => _selected is null ? "sage" : CoverPlaceholder.Tone(_selected.Id);

    /// <summary>True when the selected book is marked to read.</summary>
    public bool IsSelectedToRead => _selected?.Status == ReadingStatus.ToRead;

    /// <summary>True when the selected book is being read.</summary>
    public bool IsSelectedReading => _selected?.Status == ReadingStatus.Reading;

    /// <summary>True when the selected book is finished.</summary>
    public bool IsSelectedFinished => _selected?.Status == ReadingStatus.Finished;

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

    /// <summary>Opens or closes the shelf picker.</summary>
    public ICommand ToggleShelfPickerCommand { get; }

    /// <summary>Opens the selected book's shelf checklist.</summary>
    public ICommand OpenShelfChecklistCommand { get; }

    /// <summary>Clears the library search text.</summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>Opens the add-book sheet.</summary>
    public ICommand ShowAddSheetCommand { get; private set; } = null!;

    /// <summary>Opens the form that adds a book by hand.</summary>
    public ICommand AddByHandCommand { get; private set; } = null!;

    /// <summary>Closes the add-book sheet.</summary>
    public ICommand DismissAddSheetCommand { get; private set; } = null!;

    /// <summary>Opens or closes the library menu.</summary>
    public ICommand ShowLibraryMenuCommand { get; private set; } = null!;

    /// <summary>Opens backup and restore.</summary>
    public ICommand ShowBackupCommand { get; private set; } = null!;

    /// <summary>Opens or closes the sort menu.</summary>
    public ICommand ToggleSortMenuCommand { get; private set; } = null!;

    /// <summary>Opens or closes saved-book search.</summary>
    public ICommand ToggleSavedSearchCommand { get; private set; } = null!;

    /// <summary>Switches the library between the list and the grid.</summary>
    public ICommand ToggleGridCommand { get; private set; } = null!;

    /// <summary>Closes the save snackbar.</summary>
    public ICommand DismissSnackbarCommand { get; private set; } = null!;

    /// <summary>Opens the book named by the save snackbar.</summary>
    public ICommand OpenSnackbarCommand { get; private set; } = null!;

    /// <summary>Shows the library tab.</summary>
    public ICommand SelectLibraryCommand { get; private set; } = null!;

    /// <summary>Closes the top layer, then the current screen.</summary>
    public ICommand BackCommand { get; private set; } = null!;

    /// <summary>The app bar title. The delete dialog keeps the book details title behind it.</summary>
    public string AppBarHeading => ShowConfirm ? Headings[Screen.Details] : Heading;

    /// <summary>True when book details are visible, also behind the delete dialog.</summary>
    public bool ShowDetailsLayer => _library is not null && (_screen == Screen.Details || _screen == Screen.ConfirmRemove);

    /// <summary>The name of the current screen.</summary>
    public string Heading => _screen == Screen.Search && !Search.ShowWorks ? Search.Heading : Headings[_screen];

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

    /// <summary>True when the add-book sheet is open.</summary>
    public bool ShowAddSheet => ShowLibrary && AddSheetOpen;

    /// <summary>True when the library menu is open.</summary>
    public bool ShowLibraryMenu => ShowLibrary && _showLibraryMenu;

    /// <summary>True when the sort menu is open.</summary>
    public bool ShowSortMenu => ShowSavedBookControls && _showSortMenu;

    /// <summary>True when the shelf picker is open.</summary>
    public bool ShowShelfPicker => ShowLibrary && _showShelfPicker;

    /// <summary>True when the shelf picker button can open.</summary>
    public bool ShowShelfPickerButton => ShowLibrary;

    /// <summary>The shelf list, management controls, and membership checklist.</summary>
    public ShelvesViewModel? ShelfState { get; private set; }

    /// <summary>True when saved-book search is open.</summary>
    public bool ShowSavedSearch => ShowSavedBookControls && _showSavedSearch;

    /// <summary>True when the library uses the grid.</summary>
    public bool ShowGrid { get; private set; }

    /// <summary>The label for the view switch.</summary>
    public string ViewLabel => ShowGrid ? "Show list" : "Show covers";

    /// <summary>True when the bottom tabs are visible.</summary>
    public bool ShowTabBar => ShowLibrary || (ShowSearch && Search.ShowWorks);

    /// <summary>True when the app bar shows Back.</summary>
    public bool ShowBack => _library is not null && !ShowLibrary && !ShowSearch && !ShowLoadError;

    /// <summary>True when backup and restore is open.</summary>
    public bool ShowBackup => _library is not null && _screen == Screen.Backup;

    /// <summary>True when the save snackbar is visible.</summary>
    public bool ShowSnackbar => ShowLibrary && _snackbarBookId is not null;

    /// <summary>The save snackbar text. Empty when the snackbar is closed.</summary>
    public string SnackbarMessage => _snackbarBookId is null ? "" : $"{SnackbarTitle} is saved in your library.";

    /// <summary>The visible book count.</summary>
    public string CountLabel
    {
        get
        {
            int count = ShownBooks.Count;
            string noun = count == 1 ? "book" : "books";
            List<string> filters = FilterNames(
                BookText.FoldForSearch(SearchQuery.Trim()).Length > 0,
                _filter is not null,
                ShelfState?.FilterId is not null);

            string prefix = $"{count.ToString(CultureInfo.InvariantCulture)} {noun}";
            return filters.Count == 0
                ? prefix
                : $"{prefix} {(count == 1 ? "matches" : "match")} {JoinFilters(filters)}";
        }
    }

    /// <summary>The empty-library explanation.</summary>
    public static string EmptyDetail => "Find a book on Open Library or enter the details yourself. Your library stays on this device.";

    /// <summary>The note under saved-book search.</summary>
    public static string SearchNote => "Search checks titles and authors of saved books on this device. Accents do not matter.";

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

    /// <summary>True when the library shows every reading status.</summary>
    public bool IsAllSelected => _filter is null;

    /// <summary>True when the library shows books to read.</summary>
    public bool IsToReadSelected => _filter == ReadingStatus.ToRead;

    /// <summary>True when the library shows books being read.</summary>
    public bool IsReadingSelected => _filter == ReadingStatus.Reading;

    /// <summary>True when the library shows finished books.</summary>
    public bool IsFinishedSelected => _filter == ReadingStatus.Finished;

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

    /// <summary>True when the library shows books in a list.</summary>
    public bool ShowBookList => ShowBooks && !ShowGrid;

    /// <summary>True when the library shows books as a cover grid.</summary>
    public bool ShowBookGrid => ShowBooks && ShowGrid;

    /// <summary>True when the library has books and the filter matches none.</summary>
    public bool ShowNoMatches => ShowLibrary && Books.Count > 0 && ShownBooks.Count == 0;

    /// <summary>The message when the query and filter match no books. Null when a book is visible or the library is empty.</summary>
    public string? NoMatchMessage => ShowNoMatches switch
    {
        false => null,
        true => NoMatchText(),
    };

    /// <summary>The active filter in the words shown on the list. Null when the list shows every book.</summary>
    public string? FilterLabel => (_filter, ShelfState?.FilterId) switch
    {
        (null, null) => null,
        (ReadingStatus status, null) => StatusText(status),
        (null, not null) => ShelfState.FilterLabel,
        (ReadingStatus status, not null) => $"{StatusText(status)}, {ShelfState.FilterLabel}",
    };

    /// <summary>True when the library screen shows one reading status.</summary>
    public bool ShowFilterLabel => ShowLibrary && (_filter is not null || ShelfState?.FilterId is not null);

    /// <summary>The label of the status button at large text sizes, for example "Status: All".</summary>
    public string StatusButtonLabel => $"Status: {(_filter is null ? "All" : StatusText(_filter.Value))}";

    /// <summary>The empty-library message. Null when the library has a book or another screen is open.</summary>
    public string? EmptyMessage => IsEmpty ? "No books yet" : null;

    /// <summary>True when the library is open and the library screen is current.</summary>
    public bool ShowLibrary => _library is not null && _screen == Screen.Library;

    /// <summary>True when the library is open and the add screen or the edit screen is current.</summary>
    public bool ShowEditor => _library is not null && _screen is Screen.Add or Screen.Edit;

    /// <summary>True when the library is open and the book details screen is current.</summary>
    public bool ShowDetails => _library is not null && _screen == Screen.Details;

    /// <summary>True when the library is open and the remove confirmation is current.</summary>
    public bool ShowConfirm => _library is not null && _screen == Screen.ConfirmRemove;

    /// <summary>True when a backup is waiting for replace confirmation.</summary>
    public bool ShowRestore => _screen == Screen.ConfirmRestore;

    /// <summary>The number of books in the prepared backup. Zero when no backup is waiting.</summary>
    public int RestoreCount => _pendingRestore?.Books.Count ?? 0;

    /// <summary>The titles in the prepared backup. Empty when no backup is waiting.</summary>
    public IReadOnlyList<string> RestoreTitles => _pendingRestore is null ? [] : [.. _pendingRestore.Books.Select(book => book.Title)];

    /// <summary>The backup titles on separate lines. Empty when no backup is waiting.</summary>
    public string RestorePreview => _pendingRestore is null ? "" : string.Join(Environment.NewLine, RestoreTitles);

    /// <summary>The replace warning. Empty when no backup is waiting.</summary>
    public string RestoreFileName { get; private set; } = "Backup file";

    /// <summary>The backup file, book count, and shelf count.</summary>
    public string RestoreSummary => _pendingRestore is null
        ? ""
        : $"{RestoreFileName} · {CountNoun(RestoreCount)} · {ShelfCountNoun(_pendingRestore.Shelves.Count)}";

    /// <summary>The replace warning for the books and shelves. Empty when no backup is waiting.</summary>
    public string RestoreMessage => _pendingRestore is null
        ? ""
        : $"This backup replaces the {CountNoun(RestoreCount)} and {ShelfCountNoun(_pendingRestore.Shelves.Count)} on this device. Edits made after this backup will be lost.";

    /// <summary>True when the library file did not open.</summary>
    public bool ShowLoadError => _library is null && !ShowRestore;

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

    /// <summary>The delete confirmation body.</summary>
    public string ConfirmMessage
    {
        get
        {
            bool hasCover = _selected?.Metadata?.Cover.IsEmpty == false;
            string cover = hasCover ? " and its saved cover" : "";
            return $"Folium deletes {SelectedTitle}{cover} from this device. You cannot undo this.";
        }
    }

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
        return _library is null ? null : LibraryBackup.ExportSnapshot(new(_library.Books, _library.Shelves));
    }

    /// <summary>Prepares a backup for replace confirmation. An invalid backup leaves the saved books unchanged.</summary>
    /// <param name="json">The backup text.</param>
    /// <exception cref="ArgumentNullException">The text is null. The saved books stay unchanged.</exception>
    public void PrepareRestore(string json)
    {
        try
        {
            _pendingRestore = LibraryBackup.ReadSnapshot(json);
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

    /// <summary>Shows a backup failure on the current screen. The saved books stay unchanged.</summary>
    /// <param name="message">The failure text. The text must not be blank.</param>
    /// <exception cref="ArgumentException">The message is blank.</exception>
    public void ShowBackupError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A backup failure needs a message.", nameof(message));
        }

        SaveError = message;
        Raise(nameof(SaveError));
    }

    /// <summary>Opens the library from the store. A store failure leaves the library closed and the file unchanged.</summary>
    /// <param name="store">The store that holds the saved books. Must not be null.</param>
    /// <param name="search">The metadata provider. Null uses Open Library.</param>
    /// <param name="updates">The optional update view model.</param>
    public static LibraryViewModel Open(IBookStore store, IBookSearch? search = null, UpdateViewModel? updates = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        try
        {
            return new LibraryViewModel(new PersonalLibrary(store), search, store, updates);
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            return new LibraryViewModel(library: null, search, store, updates);
        }
    }

    /// <summary>Moves to the previous screen. Returns false on the library screen so the host can leave the app.</summary>
    public bool GoBack()
    {
        return CloseTransientLayer() || CloseScreen();
    }

    private bool CloseTransientLayer()
    {
        if (Updates?.ShowSettings == true)
        {
            Updates.CloseSettingsCommand.Execute(parameter: null);
            return true;
        }

        if (CloseShelfTransientLayer())
        {
            return true;
        }

        if (ShowAddSheet || ShowLibraryMenu || ShowSortMenu)
        {
            CloseLayers();
            RaiseChrome();
            return true;
        }

        if (!ShowSavedSearch)
        {
            return false;
        }

        _showSavedSearch = false;
        SearchQuery = "";
        RaiseChrome();
        return true;
    }

    private bool CloseShelfTransientLayer()
    {
        if (ShelfState?.ShowChecklist == true)
        {
            ShelfState.CloseChecklistCommand.Execute(parameter: null);
            return true;
        }

        if (ShelfState?.ShowDeleteConfirmation == true)
        {
            ShelfState.CancelDeleteShelfCommand.Execute(parameter: null);
            return true;
        }

        if (!ShowShelfPicker)
        {
            return false;
        }

        ToggleShelfPicker();
        return true;
    }

    private bool CloseScreen()
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

        if (ShowBackup)
        {
            ShowLibraryScreen();
            return true;
        }

        if (ShowConfirm)
        {
            ShowDetailsScreen();
            return true;
        }

        if (!ShowDetails)
        {
            return false;
        }

        ShowLibraryScreen();
        return true;
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
        ShelfState?.BeginMembership(book: null);
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
        ShelfState?.BeginMembership(_selected);
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
        ShelfState?.BeginMembership(_selected);
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
            ? library.Edit(_selected!.Id, _titleInput, _authorInput, MetadataEditor?.ToMetadata(), ShelfState?.SelectedShelfIds)
            : library.Add(_titleInput, _authorInput, MetadataEditor?.ToMetadata(), ShelfState?.SelectedShelfIds);
        ShelfState?.BeginMembership(saved);
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
        _snackbarBookId = saved.Id;
        SnackbarTitle = saved.Title;
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
        ShelfState?.BeginMembership(_selected);
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
        Raise(nameof(AppBarHeading));
        Raise(nameof(ShowDetailsLayer));
        if (screen != Screen.Library)
        {
            _snackbarBookId = null;
            SnackbarTitle = "";
        }

        Raise(nameof(Heading));
        Raise(nameof(ShowLibrary));
        Raise(nameof(ShowEditor));
        Raise(nameof(ShowDetails));
        Raise(nameof(ShowConfirm));
        Raise(nameof(ShowLoadError));
        Raise(nameof(LoadError));
        Raise(nameof(ShowRestore));
        RaiseRestore();
        Raise(nameof(ShowSearch));
        Raise(nameof(ShowBackup));
        Raise(nameof(ShowTabBar));
        Raise(nameof(ShowBack));
        RaiseChrome();
        RaiseSelectedBook();
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
        ShelfState?.BeginMembership(saved);
        ClearErrors();
        RaiseSelectedBook();
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
        if (!TrySort(parameter, out string sort) || string.Equals(_sort, sort, StringComparison.Ordinal))
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

    private static bool TrySort(object? parameter, out string sort)
    {
        sort = parameter as string ?? string.Empty;
        return SortModes.Contains(sort);
    }

    private void UseBooks(IReadOnlyList<Book> books)
    {
        Books = books;
        ShownBooks = Rows(books);
        Raise(nameof(Books));
        Raise(nameof(ShownBooks));
        RaiseLibraryShape();
    }

    private void RaiseSelectedBook()
    {
        Raise(nameof(SelectedTitle));
        Raise(nameof(ConfirmMessage));
        Raise(nameof(SelectedAuthor));
        Raise(nameof(SelectedStatus));
        Raise(nameof(SelectedCover));
        Raise(nameof(HasSelectedCover));
        Raise(nameof(SelectedTone));
        Raise(nameof(IsSelectedToRead));
        Raise(nameof(IsSelectedReading));
        Raise(nameof(IsSelectedFinished));
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
        Raise(nameof(StatusButtonLabel));
        Raise(nameof(ShowBookList));
        Raise(nameof(ShowBookGrid));
        Raise(nameof(IsAllSelected));
        Raise(nameof(IsToReadSelected));
        Raise(nameof(IsReadingSelected));
        Raise(nameof(IsFinishedSelected));
        Raise(nameof(CountLabel));
    }

    private string NoMatchText()
    {
        bool hasQuery = BookText.FoldForSearch(SearchQuery.Trim()).Length > 0;
        bool hasStatus = _filter is not null;
        bool hasShelf = ShelfState?.FilterId is not null;
        return hasQuery
            ? $"No books match {JoinFilters(FilterNames(hasQuery, hasStatus, hasShelf))}."
            : NoQueryMatchText(hasStatus, hasShelf);
    }

    private static string NoQueryMatchText(bool hasStatus, bool hasShelf)
    {
        return (hasStatus, hasShelf) switch
        {
            (true, false) => "No books with this status.",
            (false, true) => "No books on this shelf.",
            (true, true) => "No books match status and shelf.",
            _ => "No books match the current filters.",
        };
    }

    private static List<string> FilterNames(bool hasQuery, bool hasStatus, bool hasShelf)
    {
        List<string> filters = [];
        if (hasQuery)
        {
            filters.Add("your search");
        }

        if (hasStatus)
        {
            filters.Add("status");
        }

        if (hasShelf)
        {
            filters.Add("shelf");
        }

        return filters;
    }

    private static string JoinFilters(List<string> filters)
    {
        return filters.Count switch
        {
            1 => filters[0],
            2 => $"{filters[0]} and {filters[1]}",
            _ => $"{filters[0]}, {filters[1]}, and {filters[2]}",
        };
    }

    private void AttachShelfState(PersonalLibrary library)
    {
        ShelvesViewModel state = new(library);
        ShelfState = state;
        state.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(ShelvesViewModel.FilterId), StringComparison.Ordinal))
            {
                ShownBooks = Rows(Books);
                Raise(nameof(ShownBooks));
                RaiseLibraryShape();
            }

            Raise(nameof(FilterLabel));
            Raise(nameof(ShowFilterLabel));
            Raise(nameof(CountLabel));
            Raise(nameof(NoMatchMessage));
        };
        state.LibraryChanged += (_, _) =>
        {
            if (ReferenceEquals(_library, library))
            {
                UseBooks(library.Books);
                if (_selected is not null)
                {
                    _selected = library.Books.FirstOrDefault(book => book.Id == _selected.Id);
                    RaiseSelectedBook();
                }
            }
        };
        Raise(nameof(ShelfState));
    }

    private List<BookRow> Rows(IReadOnlyList<Book> books)
    {
        IReadOnlyList<Book> matching = LibraryQuery.Apply(books, _filter, shelfId: ShelfState?.FilterId, query: SearchQuery, sort: _sort);
        return [.. matching.Select(book => new BookRow(book.Id, book.Title, book.Author, StatusText(book.Status))
        {
            ReadingStatus = book.Status,
            Tone = CoverPlaceholder.Tone(book.Id),
            Initial = CoverPlaceholder.Initial(book.Title),
            HasCover = book.Metadata?.Cover.IsEmpty == false,
            Cover = book.Metadata?.Cover ?? ReadOnlyMemory<byte>.Empty,
        })];
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
            ShowAddSheetCommand, AddByHandCommand, DismissAddSheetCommand, ShowLibraryMenuCommand,
            ShowBackupCommand, ToggleSortMenuCommand, ToggleSavedSearchCommand, ToggleGridCommand,
            ToggleShelfPickerCommand, OpenShelfChecklistCommand,
            DismissSnackbarCommand, OpenSnackbarCommand, SelectLibraryCommand,
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
        Backup = 7,
    }

    private void OpenAddSheet()
    {
        CloseLayers();
        AddSheetOpen = true;
        RaiseChrome();
    }

    private void AddByHand()
    {
        CloseLayers();
        ShowAdd();
    }

    private void CloseAddSheet()
    {
        AddSheetOpen = false;
        RaiseChrome();
    }

    private void ToggleLibraryMenu()
    {
        _showLibraryMenu = !_showLibraryMenu;
        _showSortMenu = false;
        _showShelfPicker = false;
        RaiseChrome();
    }

    private void ShowBackupScreen()
    {
        CloseLayers();
        SetScreen(Screen.Backup);
    }

    private void ToggleSortMenu()
    {
        _showSortMenu = !_showSortMenu;
        _showLibraryMenu = false;
        _showShelfPicker = false;
        RaiseChrome();
    }

    private void ToggleShelfPicker()
    {
        _showShelfPicker = !_showShelfPicker;
        _showSortMenu = false;
        _showLibraryMenu = false;
        RaiseChrome();
    }

    private void ToggleSavedSearch()
    {
        _showSavedSearch = !_showSavedSearch;
        if (!_showSavedSearch)
        {
            SearchQuery = "";
        }

        RaiseChrome();
    }

    private void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
        SaveGridChoice();

        Raise(nameof(ShowGrid));
        Raise(nameof(ViewLabel));
        Raise(nameof(ShowBookList));
        Raise(nameof(ShowBookGrid));
    }

    private void DismissSnackbar()
    {
        _snackbarBookId = null;
        SnackbarTitle = "";
        Raise(nameof(ShowSnackbar));
        Raise(nameof(SnackbarMessage));
    }

    private void OpenSnackbar()
    {
        Guid id = _snackbarBookId!.Value;
        DismissSnackbar();
        Open(id);
    }

    private void CloseLayers()
    {
        AddSheetOpen = false;
        _showLibraryMenu = false;
        _showSortMenu = false;
        _showShelfPicker = false;
    }

    private void RaiseChrome()
    {
        Raise(nameof(ShowAddSheet));
        Raise(nameof(ShowLibraryMenu));
        Raise(nameof(ShowSortMenu));
        Raise(nameof(ShowShelfPicker));
        Raise(nameof(ShowShelfPickerButton));
        Raise(nameof(ShowSavedSearch));
        Raise(nameof(ShowSnackbar));
        Raise(nameof(SnackbarMessage));
        Raise(nameof(ShowTabBar));
        ((Command)ShowAddSheetCommand).RaiseCanExecuteChanged();
        ((Command)DismissAddSheetCommand).RaiseCanExecuteChanged();
        ((Command)DismissSnackbarCommand).RaiseCanExecuteChanged();
        ((Command)OpenSnackbarCommand).RaiseCanExecuteChanged();
        ((Command)ToggleShelfPickerCommand).RaiseCanExecuteChanged();
    }

    /// <summary>Records the backup file name shown before replace.</summary>
    /// <param name="name">The file name. A blank name uses "Backup file". Nothing changes when no restore waits.</param>
    public void SetRestoreFileName(string name)
    {
        if (!ShowRestore)
        {
            return;
        }

        RestoreFileName = string.IsNullOrWhiteSpace(name) ? "Backup file" : name.Trim();
        Raise(nameof(RestoreFileName));
        Raise(nameof(RestoreSummary));
    }

    private void SaveGridChoice()
    {
        if (_viewPath is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(_viewPath, ShowGrid ? "grid" : "list");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The view choice is a preference. The library stays usable when the file does not write.
            Trace.TraceWarning(exception.ToString());
        }
    }

    private static bool ReadGridChoice(string path)
    {
        try
        {
            return File.Exists(path) && string.Equals(File.ReadAllText(path).Trim(), "grid", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(exception.ToString());
            return false;
        }
    }

    private static string CountNoun(int count)
    {
        return $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "book" : "books")}";
    }

    private static string ShelfCountNoun(int count)
    {
        return $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "shelf" : "shelves")}";
    }

    private void ConfirmRestore()
    {
        if (_pendingRestore is null)
        {
            return;
        }

        try
        {
            if (_library is null)
            {
                _library = PersonalLibrary.Restore(_store, _pendingRestore);
            }
            else
            {
                _library.ReplaceSnapshot(_pendingRestore);
            }

            AttachShelfState(_library);
            _pendingRestore = null;
            UseBooks(_library.Books);
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
        Raise(nameof(RestorePreview));
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
