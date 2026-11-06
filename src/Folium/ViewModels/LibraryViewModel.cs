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

    private LibraryViewModel(PersonalLibrary? library)
    {
        _library = library;
        if (library is not null)
        {
            Books = library.Books;
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
        Screen.Library => "Library",
        _ => "Library",
    };

    public IReadOnlyList<Book> Books { get; private set; } = [];

    public bool IsEmpty => ShowLibrary && Books.Count == 0;

    public bool ShowBooks => ShowLibrary && Books.Count > 0;

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

    /// <summary>The confirm view binds to this member on the view-model instance.</summary>
    public string ConfirmMessage { get; } = "Folium deletes this book from your library.";

    public string? InputError { get; private set; }

    public string? SaveError { get; private set; }

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
            Books = _library.Books;
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
            SaveError = null;
            InputError = Describe(exception);
            Raise(nameof(SaveError));
            Raise(nameof(InputError));
        }
        catch (MissingBookException exception)
        {
            Trace.TraceError(exception.ToString());
            Books = _library.Books;
            Raise(nameof(Books));
            SaveError = "Folium could not find that book.";
            InputError = null;
            Raise(nameof(SaveError));
            Raise(nameof(InputError));
            ShowLibraryScreen();
        }
        catch (LibraryStoreException exception)
        {
            Trace.TraceError(exception.ToString());
            InputError = null;
            SaveError = "Folium could not save this book. The text is still in the form.";
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
            Books = _library.Books;
            Raise(nameof(Books));
            ShowLibraryScreen();
        }
        catch (MissingBookException exception)
        {
            Trace.TraceError(exception.ToString());
            Books = _library.Books;
            Raise(nameof(Books));
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
            AskRemoveCommand, ConfirmRemoveCommand, KeepBookCommand,
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
