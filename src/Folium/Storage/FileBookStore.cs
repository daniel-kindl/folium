using System.Globalization;
using System.Text.Json;
using Folium.Books;

namespace Folium.Storage;

public sealed class FileBookStore : IBookStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;

    public FileBookStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The library file needs a path.", nameof(path));
        }

        _path = path;
    }

    public IReadOnlyList<Book> Read()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        LibraryDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<LibraryDocument>(File.ReadAllText(_path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw Unreadable(exception);
        }

        if (document is null || document.Books is null)
        {
            throw Unreadable();
        }

        if (document.Version != 1)
        {
            throw new LibraryStoreException(
                string.Create(CultureInfo.InvariantCulture, $"The library file at {_path} uses version {document.Version}. Folium reads version 1."));
        }

        List<Book> books = new(document.Books.Count);
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
            Directory.CreateDirectory(directory);
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

    private Book ToBook(StoredBook stored, HashSet<Guid> ids)
    {
        return stored.Id == Guid.Empty || !ids.Add(stored.Id)
            ? throw new LibraryStoreException($"The library file at {_path} contains a book id that Folium cannot use.")
            : BookText.Problems(stored.Title, stored.Author).Count > 0
            ? throw new LibraryStoreException($"The library file at {_path} contains a book without a title or an author.")
            : new Book(stored.Id, stored.Title!, stored.Author!);
    }

    private StoredBook ToStored(Book book, HashSet<Guid> ids)
    {
        return book is null
            ? throw new ArgumentException("The library cannot store an empty book.", nameof(book))
            : book.Id == Guid.Empty || !ids.Add(book.Id) || BookText.Problems(book.Title, book.Author).Count > 0
            ? throw new LibraryStoreException($"Folium could not write the library file at {_path}.")
            : new StoredBook { Id = book.Id, Title = book.Title, Author = book.Author };
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
    }
}
