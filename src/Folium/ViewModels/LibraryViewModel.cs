using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Folium.Books;
using Folium.Storage;

namespace Folium.ViewModels;

public sealed class LibraryViewModel : INotifyPropertyChanged
{
    private readonly PersonalLibrary? _library;
    private Screen _screen = Screen.Library;
    private bool _editing;
    private Book? _selected;
    private string _titleInput = "";
    private string _authorInput = "";
    private string? _inputError;
    private string? _saveError;
    private IReadOnlyList<Book> _books = [];

    private LibraryViewModel(PersonalLibrary? library)
    {
        _library = library;
        if (library is not null)
        {
            _books = library.Books;
        }

        ShowAddCommand = new Command(_ => ShowAdd(), _ => ShowLibrary);
        SaveCommand = new Command(_ => Save(), _ => ShowEditor);
        CancelCommand = new Command(_ => GoBack(), _ => ShowEditor);
        OpenCommand = new Command(Open, _ => ShowLibrary);
        EditCommand = new Command(_ => ShowEdit(), _ => ShowDetails);
        AskRemoveCommand = new Command(_ => AskRemove(), _ => ShowDetails);
        ConfirmRemoveCommand = new Command(_ => ConfirmRemove(), _ => ShowConfirm);
        KeepBookCommand = new Command(_ => ShowDetailsScreen(), _ => ShowConfirm);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ShowAddCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand OpenCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand AskRemoveCommand { get; }

    public ICommand ConfirmRemoveCommand { get; }

    public ICommand KeepBookCommand { get; }

    public string Heading => _screen switch
    {
        Screen.Add => "Add a book",
        Screen.Edit => "Edit book",
        Screen.Details => "Book",
        Screen.ConfirmRemove => "Remove this book?",
        _ => "Library",
    };

    public IReadOnlyList<Book> Books => _books;

    public bool IsEmpty => ShowLibrary && _books.Count == 0;

    public bool ShowBooks => ShowLibrary && _books.Count > 0;

    public string? EmptyMessage => IsEmpty ? "No books yet." : null;

    public bool ShowLibrary => _library is not null && _screen == Screen.Library;

    public bool ShowEditor => _library is not null && _screen is Screen.Add or Screen.Edit;

    public bool ShowDetails => _library is not null && _screen == Screen.Details;

    public bool ShowConfirm => _library is not null && _screen == Screen.ConfirmRemove;

    public bool ShowLoadError => _library is null;

    public string? LoadError => ShowLoadError
        ? "Folium could not open your library. The saved file is unchanged."
        : null;

    public string SelectedTitle => _selected?.Title ?? "";

    public string SelectedAuthor => _selected?.Author ?? "";

    public string ConfirmMessage => "Folium deletes this book from your library.";

    public string? InputError => _inputError;

    public string? SaveError => _saveError;

    public string TitleInput
    {
        get => _titleInput;
        set => SetInput(ref _titleInput, value, nameof(TitleInput));
    }

    public string AuthorInput
    {
        get => _authorInput;
        set => SetInput(ref _authorInput, value, nameof(AuthorInput));
    }

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

        _selected = _books.FirstOrDefault(book => book.Id == id);
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
        if (_library is null || !ShowEditor)
        {
            return;
        }

        try
        {
            if (_editing && _selected is null)
            {
                return;
            }

            Book saved = _editing
                ? _library.Edit(_selected!.Id, _titleInput, _authorInput)
                : _library.Add(_titleInput, _authorInput);
            _books = _library.Books;
            Raise(nameof(Books));
            ClearErrors();
            if (_editing)
            {
                _selected = saved;
                ShowDetailsScreen();
            }
            else
            {
                ShowLibraryScreen();
            }
        }
        catch (BookInputException exception)
        {
            _saveError = null;
            _inputError = Describe(exception);
            Raise(nameof(SaveError));
            Raise(nameof(InputError));
        }
        catch (MissingBookException exception)
        {
            Trace.TraceError(exception.ToString());
            _books = _library.Books;
            Raise(nameof(Books));
            _saveError = "Folium could not find that book.";
            _inputError = null;
            Raise(nameof(SaveError));
            Raise(nameof(InputError));
            ShowLibraryScreen();
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            _inputError = null;
            _saveError = "Folium could not save this book. The text is still in the form.";
            Raise(nameof(InputError));
            Raise(nameof(SaveError));
        }
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
            _books = _library.Books;
            Raise(nameof(Books));
            ShowLibraryScreen();
        }
        catch (MissingBookException exception)
        {
            Trace.TraceError(exception.ToString());
            _books = _library.Books;
            Raise(nameof(Books));
            _saveError = "Folium could not find that book.";
            Raise(nameof(SaveError));
            ShowLibraryScreen();
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            _saveError = "Folium could not remove this book.";
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
        if (_inputError is null && _saveError is null)
        {
            return;
        }

        _inputError = null;
        _saveError = null;
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
        Raise(nameof(IsEmpty));
        Raise(nameof(ShowBooks));
        Raise(nameof(EmptyMessage));
        Raise(nameof(SelectedTitle));
        Raise(nameof(SelectedAuthor));
        Raise(nameof(LoadError));
        RaiseCommands();
    }

    private void SetInput(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Raise(propertyName);
        if (_inputError is not null || _saveError is not null)
        {
            _inputError = null;
            _saveError = null;
            Raise(nameof(InputError));
            Raise(nameof(SaveError));
        }
    }

    private void SetText(ref string field, string value, string propertyName)
    {
        if (field == value)
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
            AskRemoveCommand, ConfirmRemoveCommand, KeepBookCommand,
        })
        {
            if (command is Command relay)
            {
                relay.RaiseCanExecuteChanged();
            }
        }
    }

    private void Raise(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string Describe(BookInputException exception)
    {
        bool missingTitle = exception.Problems.Contains(BookInputProblem.MissingTitle);
        bool missingAuthor = exception.Problems.Contains(BookInputProblem.MissingAuthor);
        if (missingTitle && missingAuthor)
        {
            return "Enter a title and an author.";
        }

        if (missingTitle)
        {
            return "Enter a title.";
        }

        if (missingAuthor)
        {
            return "Enter an author.";
        }

        return "Folium could not save this book.";
    }

    private enum Screen
    {
        Library,
        Add,
        Edit,
        Details,
        ConfirmRemove,
    }

    private sealed class Command : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool> _canExecute;

        public Command(Action<object?> execute, Func<object?, bool> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute(parameter);

        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                _execute(parameter);
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
