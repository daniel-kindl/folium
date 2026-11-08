using System.Globalization;
using System.Text.Json;
using Folium.Books;

namespace Folium.Storage;

/// <summary>Stores the book list in one JSON file.</summary>
public sealed class FileBookStore : IBookStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;

    /// <summary>Opens the library file at the given path. The path must contain text.</summary>
    /// <param name="path">The library file location. The text must not be blank.</param>
    public FileBookStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The library file needs a path.", nameof(path));
        }

        _path = path;
    }

    /// <summary>Returns the saved books. Returns an empty list when the file is absent.</summary>
    /// <exception cref="LibraryStoreException">The file cannot be read. The file stays unchanged.</exception>
    public IReadOnlyList<Book> Read()
    {
        return File.Exists(_path) ? ToBooks(ReadDocument()) : [];
    }

    /// <summary>Replaces the saved books. A failed write leaves the previous file unchanged.</summary>
    /// <param name="books">The books that replace the saved list.</param>
    /// <exception cref="LibraryStoreException">The file cannot be written. The previous file stays unchanged.</exception>
    public void Replace(IReadOnlyList<Book> books)
    {
        ArgumentNullException.ThrowIfNull(books);
        List<StoredBook?> stored = new(books.Count);
        HashSet<Guid> ids = [];
        foreach (Book book in books)
        {
            stored.Add(ToStored(book, ids));
        }

        LibraryDocument document = new() { Version = 1, Books = stored };
        string json = JsonSerializer.Serialize(document, JsonOptions);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (string.IsNullOrEmpty(directory))
        {
            throw new LibraryStoreException($"Folium could not write the library file at {_path}.");
        }

        string temporary = _path + ".tmp";
        try
        {
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LibraryStoreException($"Folium could not write the library file at {_path}.", exception);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
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
        return document.Version == 1
            ? document
            : throw new LibraryStoreException(
                string.Create(CultureInfo.InvariantCulture, $"The library file at {_path} uses version {document.Version}. Folium reads version 1."));
    }

    private LibraryDocument? Deserialize()
    {
        try
        {
            return JsonSerializer.Deserialize<LibraryDocument>(File.ReadAllText(_path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw Unreadable(exception);
        }
    }

    private List<Book> ToBooks(LibraryDocument document)
    {
        List<Book> books = new(document.Books!.Count);
        HashSet<Guid> ids = [];
        foreach (StoredBook? stored in document.Books)
        {
            if (stored is null)
            {
                throw new LibraryStoreException($"The library file at {_path} contains an empty book.");
            }

            books.Add(ToBook(stored, ids));
        }

        return books;
    }

    private Book ToBook(StoredBook stored, HashSet<Guid> ids)
    {
        bool invalidId = stored.Id == Guid.Empty || !ids.Add(stored.Id);
        bool invalidInput = BookText.Problems(stored.Title, stored.Author).Count > 0;
        return (invalidId, invalidInput) switch
        {
            (true, _) => throw new LibraryStoreException($"The library file at {_path} contains a book id that Folium cannot use."),
            (_, true) => throw new LibraryStoreException($"The library file at {_path} contains a book without a title or an author."),
            _ => new Book(stored.Id, stored.Title!, stored.Author!, RequireStatus(stored.ReadingStatus), stored.Metadata),
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
            _ => throw new LibraryStoreException($"The library file at {_path} contains a reading status that Folium cannot use."),
        };
    }

    private StoredBook ToStored(Book book, HashSet<Guid> ids)
    {
        return book switch
        {
            null => throw new ArgumentException("The library cannot store an empty book.", nameof(book)),
            _ when book.Id == Guid.Empty || !ids.Add(book.Id) || BookText.Problems(book.Title, book.Author).Count > 0
                => throw new LibraryStoreException($"Folium could not write the library file at {_path}."),
            _ => new StoredBook
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ReadingStatus = Token(book.Status),
                Metadata = book.Metadata,
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
            _ => throw new LibraryStoreException($"Folium could not write the library file at {_path}."),
        };
    }

    private LibraryStoreException Unreadable(Exception? inner = null)
    {
        return new($"Folium could not read the library file at {_path}.", inner);
    }

    private sealed class LibraryDocument
    {
        public int Version { get; set; }

        public List<StoredBook?>? Books { get; set; }
    }

    private sealed class StoredBook
    {
        public Guid Id { get; set; }

        public string? Title { get; set; }

        public string? Author { get; set; }

        public string? ReadingStatus { get; set; }

        public BookMetadata? Metadata { get; set; }
    }
}
