using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Folium.Books;
using Folium.Storage;

namespace Folium.ViewModels;

/// <summary>Manages shelves, the shelf filter, and book membership choices.</summary>
public sealed class ShelvesViewModel : INotifyPropertyChanged
{
    private readonly PersonalLibrary _library;
    private readonly HashSet<Guid> _selectedShelfIds = [];
    private readonly HashSet<Guid> _originalShelfIds = [];
    private Guid? _bookId;
    private Shelf? _pendingDelete;
    private string _shelfNameInput = string.Empty;

    /// <summary>Creates the shelf controls for a personal library.</summary>
    /// <param name="library">The library that owns the shelves.</param>
    public ShelvesViewModel(PersonalLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        _library = library;
        CreateShelfCommand = new Command(_ => CreateShelf(), _ => true);
        RenameShelfCommand = new Command(_ => RenameShelf(), _ => ManagedShelfId is not null);
        AskDeleteShelfCommand = new Command(AskDeleteShelf, _ => ManagedShelfId is not null);
        ConfirmDeleteShelfCommand = new Command(_ => ConfirmDeleteShelf(), _ => ShowDeleteConfirmation);
        CancelDeleteShelfCommand = new Command(_ => CancelDeleteShelf(), _ => ShowDeleteConfirmation);
        SelectFilterCommand = new Command(SelectFilter, _ => true);
        SelectManagedShelfCommand = new Command(SelectManagedShelf, _ => true);
        ToggleMembershipCommand = new Command(ToggleMembership, _ => true);
        SaveMembershipCommand = new Command(_ => SaveMembership(), _ => _bookId is not null);
        CloseChecklistCommand = new Command(_ => CloseChecklist(), _ => ShowChecklist);
    }

    /// <summary>Tells the view that a bound value changed.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Signals that a library write succeeded.</summary>
    public event EventHandler? LibraryChanged;

    /// <summary>The current shelves, ordered by folded name.</summary>
    public IReadOnlyList<Shelf> Shelves => _library.Shelves;

    /// <summary>The single shelf filter. Null means all shelves.</summary>
    public Guid? FilterId { get; private set; }
    /// <summary>The current shelf filter label.</summary>
    public string FilterLabel => FilterId is Guid id
        ? Shelves.First(shelf => shelf.Id == id).Name
        : "All shelves";

    /// <summary>The shelf selected for rename or delete.</summary>
    public Guid? ManagedShelfId { get; private set; }

    private void SetManagedShelfId(Guid? value)
    {
        if (ManagedShelfId != value)
        {
            ManagedShelfId = value;
            ShelfNameInput = Shelves.FirstOrDefault(shelf => shelf.Id == value)?.Name ?? string.Empty;
            Raise(nameof(ManagedShelfId));
            ((Command)RenameShelfCommand).RaiseCanExecuteChanged();
            ((Command)AskDeleteShelfCommand).RaiseCanExecuteChanged();
        }
    }

    /// <summary>The shelf name in the create or rename field.</summary>
    public string ShelfNameInput
    {
        get => _shelfNameInput;
        set
        {
            string name = value ?? string.Empty;
            if (string.Equals(_shelfNameInput, name, StringComparison.Ordinal))
            {
                return;
            }

            _shelfNameInput = name;
            ClearShelfError();
            Raise(nameof(ShelfNameInput));
        }
    }

    /// <summary>The shelf name or storage error. Null when there is no error.</summary>
    public string? ShelfNameError { get; private set; }

    /// <summary>True when the delete confirmation is open.</summary>
    public bool ShowDeleteConfirmation => _pendingDelete is not null;

    /// <summary>The shelf selected for delete confirmation.</summary>
    public string DeleteMessage => _pendingDelete is null
        ? string.Empty
        : $"Delete the shelf {_pendingDelete.Name}? Books on this shelf will stay in your library.";

    /// <summary>The shelves selected for the current book.</summary>
    public IReadOnlySet<Guid> SelectedShelfIds => _selectedShelfIds.ToFrozenSet();

    /// <summary>The membership checklist options.</summary>
    public IReadOnlyList<ShelfChoice> MembershipChoices => [.. Shelves.Select(shelf => new ShelfChoice(
        shelf.Id,
        shelf.Name,
        _selectedShelfIds.Contains(shelf.Id)))];

    /// <summary>The selected book's shelf names.</summary>
    public string BookShelvesLabel => string.Join(", ", Shelves
        .Where(shelf => _selectedShelfIds.Contains(shelf.Id))
        .Select(shelf => shelf.Name));

    /// <summary>True when the current book has at least one shelf.</summary>
    public bool HasBookShelves => _selectedShelfIds.Count > 0;

    /// <summary>True when the current book has no shelves.</summary>
    public bool HasNoBookShelves => !HasBookShelves;

    /// <summary>True when the membership checklist is open.</summary>
    public bool ShowChecklist { get; private set; }

    /// <summary>Creates a shelf from the name field.</summary>
    public ICommand CreateShelfCommand { get; }

    /// <summary>Renames the selected shelf.</summary>
    public ICommand RenameShelfCommand { get; }

    /// <summary>Opens delete confirmation for the selected shelf.</summary>
    public ICommand AskDeleteShelfCommand { get; }

    /// <summary>Deletes the shelf after confirmation.</summary>
    public ICommand ConfirmDeleteShelfCommand { get; }

    /// <summary>Closes delete confirmation.</summary>
    public ICommand CancelDeleteShelfCommand { get; }

    /// <summary>Selects one shelf or all shelves for the library filter.</summary>
    public ICommand SelectFilterCommand { get; }

    /// <summary>Selects a shelf for rename or delete.</summary>
    public ICommand SelectManagedShelfCommand { get; }

    /// <summary>Toggles a shelf in the membership checklist.</summary>
    public ICommand ToggleMembershipCommand { get; }

    /// <summary>Saves checklist changes for the current book.</summary>
    public ICommand SaveMembershipCommand { get; }

    /// <summary>Closes the membership checklist.</summary>
    public ICommand CloseChecklistCommand { get; }

    /// <summary>Loads membership choices for a book or starts an empty checklist.</summary>
    /// <param name="book">The book to load, or null for a new book.</param>
    public void BeginMembership(Book? book)
    {
        _bookId = book?.Id;
        _selectedShelfIds.Clear();
        if (book is not null)
        {
            _selectedShelfIds.UnionWith(book.ShelfIds);
        }

        _originalShelfIds.Clear();
        _originalShelfIds.UnionWith(_selectedShelfIds);

        if (ShowChecklist)
        {
            ShowChecklist = false;
            Raise(nameof(ShowChecklist));
        }
        RaiseMembership();
        ((Command)SaveMembershipCommand).RaiseCanExecuteChanged();
        ((Command)CloseChecklistCommand).RaiseCanExecuteChanged();
    }

    /// <summary>Opens the membership checklist.</summary>
    public void OpenChecklist()
    {
        ShowChecklist = true;
        Raise(nameof(ShowChecklist));
        ((Command)SaveMembershipCommand).RaiseCanExecuteChanged();
        ((Command)CloseChecklistCommand).RaiseCanExecuteChanged();
    }

    private void CreateShelf()
    {
        try
        {
            Shelf shelf = _library.CreateShelf(ShelfNameInput);
            SetManagedShelfId(shelf.Id);
            ShelfNameInput = string.Empty;
            ClearShelfError();
            RaiseShelfState();
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ShelfNameException exception)
        {
            ShelfNameError = exception.Message;
            Raise(nameof(ShelfNameError));
        }
        catch (LibraryStoreException exception)
        {
            ShelfNameError = "Folium could not save this shelf.";
            Trace.TraceError(exception.ToString());
            Raise(nameof(ShelfNameError));
        }
    }

    private void RenameShelf()
    {
        if (ManagedShelfId is not Guid id)
        {
            return;
        }

        try
        {
            _ = _library.RenameShelf(id, ShelfNameInput);
            ClearShelfError();
            RaiseShelfState();
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ShelfNameException exception)
        {
            ShelfNameError = exception.Message;
            Raise(nameof(ShelfNameError));
        }
        catch (LibraryStoreException exception)
        {
            ShelfNameError = "Folium could not save this shelf.";
            Trace.TraceError(exception.ToString());
            Raise(nameof(ShelfNameError));
        }
    }

    private void AskDeleteShelf(object? parameter)
    {
        Guid? id = parameter is Guid selected ? selected : ManagedShelfId;
        _pendingDelete = id is Guid shelfId ? Shelves.FirstOrDefault(shelf => shelf.Id == shelfId) : null;
        Raise(nameof(ShowDeleteConfirmation));
        Raise(nameof(DeleteMessage));
        ((Command)ConfirmDeleteShelfCommand).RaiseCanExecuteChanged();
        ((Command)CancelDeleteShelfCommand).RaiseCanExecuteChanged();
    }

    private void ConfirmDeleteShelf()
    {
        if (_pendingDelete is null)
        {
            return;
        }

        try
        {
            Guid id = _pendingDelete.Id;
            _library.DeleteShelf(id);
            _pendingDelete = null;
            _ = _selectedShelfIds.Remove(id);
            _ = _originalShelfIds.Remove(id);
            if (FilterId == id)
            {
                FilterId = null;
                Raise(nameof(FilterId));
                Raise(nameof(FilterLabel));
            }

            if (ManagedShelfId == id)
            {
                ManagedShelfId = null;
                _shelfNameInput = string.Empty;
                Raise(nameof(ManagedShelfId));
                Raise(nameof(ShelfNameInput));
            }

            ClearShelfError();
            RaiseShelfState();
            Raise(nameof(ShowDeleteConfirmation));
            Raise(nameof(DeleteMessage));
            ((Command)ConfirmDeleteShelfCommand).RaiseCanExecuteChanged();
            ((Command)CancelDeleteShelfCommand).RaiseCanExecuteChanged();
            ((Command)RenameShelfCommand).RaiseCanExecuteChanged();
            ((Command)AskDeleteShelfCommand).RaiseCanExecuteChanged();
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (LibraryStoreException exception)
        {
            ShelfNameError = "Folium could not delete this shelf.";
            Trace.TraceError(exception.ToString());
            Raise(nameof(ShelfNameError));
        }
    }

    private void CancelDeleteShelf()
    {
        _pendingDelete = null;
        Raise(nameof(ShowDeleteConfirmation));
        Raise(nameof(DeleteMessage));
        ((Command)ConfirmDeleteShelfCommand).RaiseCanExecuteChanged();
        ((Command)CancelDeleteShelfCommand).RaiseCanExecuteChanged();
    }

    private void SelectFilter(object? parameter)
    {
        Guid? id = ResolveFilterId(parameter);
        if (!IsKnownFilter(id) || FilterId == id)
        {
            return;
        }

        FilterId = id;
        Raise(nameof(FilterId));
        Raise(nameof(FilterLabel));
    }

    private Guid? ResolveFilterId(object? parameter)
    {
        return parameter is null || Equals(parameter, "all")
            ? null
            : parameter switch
            {
                Guid id => id,
                string token when Guid.TryParse(token, out Guid parsed) => parsed,
                _ => FilterId,
            };
    }

    private bool IsKnownFilter(Guid? id)
    {
        return id is null || Shelves.Any(shelf => shelf.Id == id);
    }

    private void SelectManagedShelf(object? parameter)
    {
        if (parameter is Guid id && Shelves.Any(shelf => shelf.Id == id))
        {
            SetManagedShelfId(id);
        }
    }

    private void ToggleMembership(object? parameter)
    {
        if (parameter is not Guid id || !Shelves.Any(shelf => shelf.Id == id))
        {
            return;
        }

        if (!_selectedShelfIds.Add(id))
        {
            _ = _selectedShelfIds.Remove(id);
        }

        RaiseMembership();
    }

    private void SaveMembership()
    {
        if (_bookId is not Guid id)
        {
            return;
        }

        try
        {
            Book book = _library.SetShelves(id, _selectedShelfIds);
            _bookId = book.Id;
            _originalShelfIds.Clear();
            _originalShelfIds.UnionWith(_selectedShelfIds);
            ClearShelfError();
            ShowChecklist = false;
            RaiseMembership();
            Raise(nameof(ShowChecklist));
            ((Command)SaveMembershipCommand).RaiseCanExecuteChanged();
            ((Command)CloseChecklistCommand).RaiseCanExecuteChanged();
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (LibraryStoreException exception)
        {
            ShelfNameError = "Folium could not save these shelves.";
            Trace.TraceError(exception.ToString());
            Raise(nameof(ShelfNameError));
        }
    }

    private void CloseChecklist()
    {
        _selectedShelfIds.Clear();
        _selectedShelfIds.UnionWith(_originalShelfIds);
        ShowChecklist = false;
        RaiseMembership();
        Raise(nameof(ShowChecklist));
        ((Command)CloseChecklistCommand).RaiseCanExecuteChanged();
    }

    private void RaiseShelfState()
    {
        Raise(nameof(Shelves));
        Raise(nameof(FilterLabel));
        RaiseMembership();
    }

    private void ClearShelfError()
    {
        if (ShelfNameError is not null)
        {
            ShelfNameError = null;
            Raise(nameof(ShelfNameError));
        }
    }

    private void RaiseMembership()
    {
        Raise(nameof(SelectedShelfIds));
        Raise(nameof(MembershipChoices));
        Raise(nameof(BookShelvesLabel));
        Raise(nameof(HasBookShelves));
        Raise(nameof(HasNoBookShelves));
    }

    private void Raise(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class Command(Action<object?> execute, Func<object?, bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
