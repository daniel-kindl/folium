using System.Globalization;
using System.Text;
using System.Text.Json;
using Folium.Books;

namespace Folium.Storage;

/// <summary>Stores books and shelves in JSON and saved covers in private files.</summary>
public sealed class FileBookStore : IBookStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>The library file path.</summary>
    internal string LibraryPath { get; }

    private readonly CoverFiles _covers;
    private readonly Func<string, bool> _flushDirectory;

    /// <summary>Opens the library file at the given path. The path must contain text.</summary>
    /// <param name="path">The library file location. The text must not be blank.</param>
    public FileBookStore(string path)
        : this(path, DirectoryFlush.TryFlush)
    {
    }

    internal FileBookStore(string path, Func<string, bool> flushDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The library file needs a path.", nameof(path));
        }

        ArgumentNullException.ThrowIfNull(flushDirectory);
        LibraryPath = path;
        _covers = new CoverFiles(path + ".covers");
        _flushDirectory = flushDirectory;
    }

    /// <summary>Returns all saved books and shelves. Returns an empty snapshot when the file is absent.</summary>
    /// <exception cref="LibraryStoreException">The file is damaged or uses an unsupported version.</exception>
    public LibrarySnapshot Read()
    {
        try
        {
            return File.Exists(LibraryPath) ? ToSnapshot(ReadDocument()) : new([], []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Unreadable(exception);
        }
    }

    /// <summary>Replaces all saved books and shelves in one write.</summary>
    /// <param name="snapshot">The complete personal library.</param>
    /// <exception cref="ArgumentNullException">The snapshot is null.</exception>
    /// <exception cref="LibraryStoreException">The snapshot is invalid or the file cannot be written.</exception>
    public void Replace(LibrarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        List<StoredShelf?> shelves = ToStoredShelves(snapshot.Shelves);
        HashSet<Guid> shelfIds = [.. shelves.Select(shelf => shelf!.Id)];
        List<StoredBook?> books = ToStoredBooks(snapshot.Books, shelfIds);

        try
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(LibraryPath))!;
            _ = Directory.CreateDirectory(directory);
            KeepLegacyCopy();
            HashSet<string> retained = new(StringComparer.Ordinal);
            foreach (StoredBook? book in books)
            {
                if (book!.Metadata is { Cover.IsEmpty: false } metadata)
                {
                    book.CoverFile = _covers.Save(metadata.Cover.Span);
                    _ = retained.Add(book.CoverFile);
                    book.Metadata = metadata with { Cover = default };
                }
            }

            LibraryDocument document = new() { Version = 3, Books = books, Shelves = shelves };
            AtomicFile.Write(LibraryPath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, JsonOptions)));
            if (_flushDirectory(directory))
            {
                _covers.Collect(retained);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LibraryStoreException($"Folium could not write the library file at {LibraryPath}.", exception);
        }
    }

    private void KeepLegacyCopy()
    {
        if (File.Exists(LibraryPath) && TryReadValidLegacy(out int version, out byte[] legacy))
        {
            string backup = LibraryPath + string.Create(CultureInfo.InvariantCulture, $".v{version}.bak");
            if (!File.Exists(backup))
            {
                AtomicFile.Write(backup, legacy);
            }
        }
    }

    private bool TryReadValidLegacy(out int version, out byte[] legacy)
    {
        version = 0;
        legacy = [];
        try
        {
            LibraryDocument previous = ReadDocument();
            if (previous.Version is not (1 or 2))
            {
                return false;
            }

            _ = ToSnapshot(previous);
            version = previous.Version;
            legacy = File.ReadAllBytes(LibraryPath);
            return true;
        }
        catch (LibraryStoreException exception) when (exception.InnerException is not (IOException or UnauthorizedAccessException))
        {
            return false;
        }
    }

    private LibraryDocument ReadDocument()
    {
        LibraryDocument document = RequireDocument(Deserialize());
        return RequireVersion(document);
    }

    private LibraryDocument RequireDocument(LibraryDocument? document)
    {
        return document?.Books is not null
            ? document
            : throw Unreadable();
    }

    private LibraryDocument RequireVersion(LibraryDocument document)
    {
        return document.Version is 1 or 2 or 3
            ? document
            : throw new LibraryStoreException(
                string.Create(CultureInfo.InvariantCulture, $"The library file at {LibraryPath} uses version {document.Version}. Folium reads versions 1, 2, and 3."));
    }

    private LibraryDocument? Deserialize()
    {
        try
        {
            return JsonSerializer.Deserialize<LibraryDocument>(File.ReadAllText(LibraryPath), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw Unreadable(exception);
        }
    }

    private LibrarySnapshot ToSnapshot(LibraryDocument document)
    {
        List<Shelf> shelves = ToShelves(document);
        HashSet<Guid> shelfIds = [.. shelves.Select(shelf => shelf.Id)];
        List<Book> books = new(document.Books!.Count);
        HashSet<Guid> bookIds = [];
        foreach (StoredBook? stored in document.Books)
        {
            if (stored is null)
            {
                throw new LibraryStoreException($"The library file at {LibraryPath} contains an empty book.");
            }

            books.Add(ToBook(stored, bookIds, shelfIds));
        }

        return new(books, shelves);
    }

    private List<Shelf> ToShelves(LibraryDocument document)
    {
        if (document.Version < 3)
        {
            return [];
        }

        if (document.Shelves is null)
        {
            throw Unreadable();
        }

        List<Shelf> shelves = new(document.Shelves.Count);
        HashSet<Guid> ids = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (StoredShelf? stored in document.Shelves)
        {
            shelves.Add(ReadShelf(stored, ids, names));
        }

        return [.. shelves.OrderBy(shelf => ShelfName.Parse(shelf.Name).FoldedValue, StringComparer.OrdinalIgnoreCase)
            .ThenBy(shelf => shelf.Name, StringComparer.Ordinal)
            .ThenBy(shelf => shelf.Id)];
    }

    private Shelf ReadShelf(StoredShelf? stored, HashSet<Guid> ids, HashSet<string> names)
    {
        if (stored is null || stored.Id == Guid.Empty || !ids.Add(stored.Id))
        {
            throw new LibraryStoreException($"The library file at {LibraryPath} contains an invalid shelf id.");
        }

        ShelfName name;
        try
        {
            name = ShelfName.Parse(stored.Name);
        }
        catch (ShelfNameException exception)
        {
            throw new LibraryStoreException($"The library file at {LibraryPath} contains an invalid shelf name.", exception);
        }

        return !string.Equals(name.Value, stored.Name, StringComparison.Ordinal)
            || !names.Add(name.FoldedValue)
            ? throw new LibraryStoreException($"The library file at {LibraryPath} contains a duplicate or untrimmed shelf name.")
            : new(stored.Id, name.Value);
    }

    private Book ToBook(StoredBook stored, HashSet<Guid> ids, HashSet<Guid> shelfIds)
    {
        bool invalidId = stored.Id == Guid.Empty || !ids.Add(stored.Id);
        bool invalidInput = BookText.Problems(stored.Title, stored.Author).Count > 0;
        return (invalidId, invalidInput) switch
        {
            (true, _) => throw new LibraryStoreException($"The library file at {LibraryPath} contains a book id that Folium cannot use."),
            (_, true) => throw new LibraryStoreException($"The library file at {LibraryPath} contains a book without a title or an author."),
            _ => new Book(
                stored.Id,
                stored.Title!,
                stored.Author!,
                RequireStatus(stored.ReadingStatus),
                ReadMetadata(stored),
                stored.ShelfIds?.Where(shelfIds.Contains)),
        };
    }

    private BookMetadata? ReadMetadata(StoredBook stored)
    {
        return (stored.CoverFile, stored.Metadata) switch
        {
            (null, _) => stored.Metadata,
            (_, { Cover.IsEmpty: true } metadata) => metadata with { Cover = _covers.Read(stored.CoverFile) },
            _ => throw Unreadable(),
        };
    }

    private ReadingStatus RequireStatus(string? token)
    {
        return token switch
        {
            null => ReadingStatus.ToRead,
            "to-read" => ReadingStatus.ToRead,
            "reading" => ReadingStatus.Reading,
            "finished" => ReadingStatus.Finished,
            _ => throw new LibraryStoreException($"The library file at {LibraryPath} contains a reading status that Folium cannot use."),
        };
    }

    private List<StoredShelf?> ToStoredShelves(IReadOnlyList<Shelf> shelves)
    {
        List<StoredShelf?> stored = new(shelves.Count);
        HashSet<Guid> ids = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (Shelf shelf in shelves)
        {
            stored.Add(ToStoredShelf(shelf, ids, names));
        }

        return [.. stored.OrderBy(item => ShelfName.Parse(item!.Name).FoldedValue, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item!.Name, StringComparer.Ordinal)
            .ThenBy(item => item!.Id)];
    }

    private StoredShelf ToStoredShelf(Shelf shelf, HashSet<Guid> ids, HashSet<string> names)
    {
        if (shelf is null)
        {
            throw new LibraryStoreException($"Folium could not write the library file at {LibraryPath}.");
        }

        ShelfName name;
        try
        {
            name = ShelfName.Parse(shelf.Name);
        }
        catch (ShelfNameException exception)
        {
            throw new LibraryStoreException($"Folium could not write the library file at {LibraryPath}.", exception);
        }

        return shelf.Id == Guid.Empty || !ids.Add(shelf.Id)
            || !string.Equals(name.Value, shelf.Name, StringComparison.Ordinal)
            || !names.Add(name.FoldedValue)
            ? throw new LibraryStoreException($"Folium could not write the library file at {LibraryPath}.")
            : new StoredShelf { Id = shelf.Id, Name = name.Value };
    }

    private List<StoredBook?> ToStoredBooks(IReadOnlyList<Book> books, HashSet<Guid> shelfIds)
    {
        List<StoredBook?> stored = new(books.Count);
        HashSet<Guid> ids = [];
        foreach (Book book in books)
        {
            stored.Add(ToStored(book, ids, shelfIds));
        }

        return stored;
    }

    private StoredBook ToStored(Book book, HashSet<Guid> ids, HashSet<Guid> shelfIds)
    {
        return book switch
        {
            null => throw new ArgumentException("The library cannot store an empty book.", nameof(book)),
            _ when book.Id == Guid.Empty || !ids.Add(book.Id)
                || BookText.Problems(book.Title, book.Author).Count > 0
                || book.ShelfIds.Any(id => !shelfIds.Contains(id))
                => throw new LibraryStoreException($"Folium could not write the library file at {LibraryPath}."),
            _ => new StoredBook
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ReadingStatus = Token(book.Status),
                Metadata = book.Metadata,
                ShelfIds = [.. book.ShelfIds.Order()],
            },
        };
    }

    private string Token(ReadingStatus status)
    {
        return status switch
        {
            ReadingStatus.ToRead => "to-read",
            ReadingStatus.Reading => "reading",
            ReadingStatus.Finished => "finished",
            _ => throw new LibraryStoreException($"Folium could not write the library file at {LibraryPath}."),
        };
    }

    private LibraryStoreException Unreadable(Exception? inner = null)
    {
        return new($"Folium could not read the library file at {LibraryPath}.", inner);
    }

    private sealed class LibraryDocument
    {
        public int Version { get; set; }

        public List<StoredBook?>? Books { get; set; }

        public List<StoredShelf?>? Shelves { get; set; }
    }

    private sealed class StoredBook
    {
        public Guid Id { get; set; }

        public string? Title { get; set; }

        public string? Author { get; set; }

        public string? ReadingStatus { get; set; }

        public BookMetadata? Metadata { get; set; }

        public string? CoverFile { get; set; }

        public List<Guid>? ShelfIds { get; set; }
    }

    private sealed class StoredShelf
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }
    }
}
