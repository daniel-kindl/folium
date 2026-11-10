using System.Globalization;
using System.Text;
using System.Text.Json;
using Folium.Books;

namespace Folium.Storage;

/// <summary>Stores book metadata in JSON and saved covers in private files.</summary>
public sealed class FileBookStore : IBookStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly CoverFiles _covers;

    /// <summary>Opens the library file at the given path. The path must contain text.</summary>
    /// <param name="path">The library file location. The text must not be blank.</param>
    public FileBookStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The library file needs a path.", nameof(path));
        }

        _path = path;
        _covers = new CoverFiles(path + ".covers");
    }

    /// <summary>Returns the saved books. Returns an empty list when the file is absent.</summary>
    /// <exception cref="LibraryStoreException">The file cannot be read. The file stays unchanged.</exception>
    public IReadOnlyList<Book> Read()
    {
        try
        {
            return File.Exists(_path) ? ToBooks(ReadDocument()) : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Unreadable(exception);
        }
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

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            KeepLegacyCopy();
            HashSet<string> retained = new(StringComparer.Ordinal);
            foreach (StoredBook? book in stored)
            {
                if (book!.Metadata is { Cover.IsEmpty: false } metadata)
                {
                    book.CoverFile = _covers.Save(metadata.Cover.Span);
                    _ = retained.Add(book.CoverFile);
                    book.Metadata = metadata with { Cover = default };
                }
            }

            LibraryDocument document = new() { Version = 2, Books = stored };
            AtomicFile.Write(_path, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, JsonOptions)));
            _covers.Collect(retained);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LibraryStoreException($"Folium could not write the library file at {_path}.", exception);
        }
    }

    private void KeepLegacyCopy()
    {
        if (File.Exists(_path))
        {
            LibraryDocument previous = ReadDocument();
            if (previous.Version == 1 && !File.Exists(_path + ".v1.bak"))
            {
                _ = ToBooks(previous);
                AtomicFile.Write(_path + ".v1.bak", File.ReadAllBytes(_path));
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
        return document.Version is 1 or 2
            ? document
            : throw new LibraryStoreException(
                string.Create(CultureInfo.InvariantCulture, $"The library file at {_path} uses version {document.Version}. Folium reads versions 1 and 2."));
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
            _ => new Book(stored.Id, stored.Title!, stored.Author!, RequireStatus(stored.ReadingStatus), ReadMetadata(stored)),
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

        public string? CoverFile { get; set; }
    }
}
