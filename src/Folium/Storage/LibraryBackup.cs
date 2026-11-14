using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Folium.Books;

namespace Folium.Storage;

/// <summary>Reads and writes a personal library backup. The backup is not the live library file.</summary>
public static class LibraryBackup
{
    private const string KIND = "personal-library-backup";
    private const int VERSION = 2;
    private const int MAXIMUM_BYTES = 32 * 1024 * 1024;

    /// <summary>The maximum UTF-8 size of a backup, in bytes.</summary>
    public static int MaximumBytes => MAXIMUM_BYTES;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Writes the books to backup text. The text is not written to the live library file.</summary>
    /// <param name="books">The saved books. The list must not be null.</param>
    /// <param name="maximumBytes">The maximum UTF-8 size. The default is 32 MiB.</param>
    /// <returns>The backup text.</returns>
    /// <exception cref="ArgumentNullException">The book list is null.</exception>
    /// <exception cref="LibraryBackupException">A book cannot be saved, or the backup is larger than the limit.</exception>
    public static string Export(IReadOnlyList<Book> books, int maximumBytes = MAXIMUM_BYTES)
    {
        ArgumentNullException.ThrowIfNull(books);
        return ExportSnapshot(new(books, []), maximumBytes);
    }

    /// <summary>Writes books and shelves to backup text.</summary>
    /// <param name="snapshot">The personal library to export.</param>
    /// <param name="maximumBytes">The maximum UTF-8 size. The default is 32 MiB.</param>
    /// <returns>The backup text.</returns>
    /// <exception cref="ArgumentNullException">The snapshot is null.</exception>
    /// <exception cref="LibraryBackupException">The library cannot be saved or the backup exceeds the size limit.</exception>
    public static string ExportSnapshot(LibrarySnapshot snapshot, int maximumBytes = MAXIMUM_BYTES)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        List<StoredShelf?> shelves = ToStoredShelves(snapshot.Shelves);
        BackupDocument document = new()
        {
            Kind = KIND,
            Version = VERSION,
            Books = ToStored(snapshot.Books, shelves),
            Shelves = shelves,
        };
        string json = JsonSerializer.Serialize(document, JsonOptions);
        return Encoding.UTF8.GetByteCount(json) > maximumBytes
            ? throw new LibraryBackupException("This backup is too large to export.")
            : json;
    }

    /// <summary>Reads a backup. A rejected backup does not change the live library file.</summary>
    /// <param name="json">The backup text. The text must not be null.</param>
    /// <returns>The books in the backup.</returns>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="LibraryBackupException">The backup is too large, incomplete, or not a supported backup.</exception>
    public static IReadOnlyList<Book> Read(string json)
    {
        return ReadSnapshot(json).Books;
    }

    /// <summary>Reads a version 1 or version 2 backup as one snapshot.</summary>
    /// <param name="json">The backup text.</param>
    /// <returns>The books and shelves in the backup.</returns>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="LibraryBackupException">The backup is too large, incomplete, or not supported.</exception>
    public static LibrarySnapshot ReadSnapshot(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Encoding.UTF8.GetByteCount(json) > MAXIMUM_BYTES
            ? throw new LibraryBackupException("This backup is too large to restore.")
            : ToSnapshot(Parse(json));
    }

    /// <summary>Reads backup text from a stream and stops when the byte limit is passed.</summary>
    /// <param name="stream">The backup bytes. The stream must not be null.</param>
    /// <param name="maximumBytes">The maximum number of bytes to accept. The default is 32 MiB.</param>
    /// <returns>The backup text.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="LibraryBackupException">The stream is larger than the limit.</exception>
    public static string ReadText(Stream stream, int maximumBytes = MAXIMUM_BYTES)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using MemoryStream copy = new();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (copy.Length + read > maximumBytes)
            {
                throw new LibraryBackupException("This backup is too large to restore.");
            }

            copy.Write(buffer, 0, read);
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(copy.ToArray());
        }
        catch (DecoderFallbackException exception)
        {
            throw Incomplete(exception);
        }
    }

    private static BackupDocument Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw Incomplete();
        }

        try
        {
            return JsonSerializer.Deserialize<BackupDocument>(json, JsonOptions) ?? throw Incomplete();
        }
        catch (JsonException exception)
        {
            throw Incomplete(exception);
        }
    }

    private static LibrarySnapshot ToSnapshot(BackupDocument document)
    {
        if (!string.Equals(document.Kind, KIND, StringComparison.Ordinal))
        {
            throw new LibraryBackupException("This file is not a Folium backup.");
        }

        if (document.Version is not (1 or 2))
        {
            throw new LibraryBackupException(
                string.Create(CultureInfo.InvariantCulture, $"This backup uses version {document.Version}. Folium reads versions 1 and 2."));
        }

        if (document.Books is null)
        {
            throw Incomplete();
        }

        List<Shelf> shelves = ToShelves(document);
        HashSet<Guid> shelfIds = [.. shelves.Select(shelf => shelf.Id)];
        List<Book> books = new(document.Books.Count);
        HashSet<Guid> ids = [];
        foreach (StoredBook? stored in document.Books)
        {
            books.Add(stored is null ? throw Incomplete() : ToBook(stored, ids, shelfIds));
        }

        return new(books, shelves);
    }

    private static List<Shelf> ToShelves(BackupDocument document)
    {
        if (document.Version == 1)
        {
            return [];
        }

        if (document.Shelves is null)
        {
            throw Incomplete();
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

    private static Shelf ReadShelf(StoredShelf? stored, HashSet<Guid> ids, HashSet<string> names)
    {
        if (stored is null || stored.Id == Guid.Empty || !ids.Add(stored.Id))
        {
            throw Incomplete();
        }

        ShelfName name;
        try
        {
            name = ShelfName.Parse(stored.Name);
        }
        catch (ShelfNameException exception)
        {
            throw Incomplete(exception);
        }

        return !string.Equals(name.Value, stored.Name, StringComparison.Ordinal) || !names.Add(name.FoldedValue)
            ? throw Incomplete()
            : new(stored.Id, name.Value);
    }

    private static Book ToBook(StoredBook stored, HashSet<Guid> ids, HashSet<Guid> shelfIds)
    {
        if (stored.ShelfIds?.Exists(id => !shelfIds.Contains(id)) == true)
        {
            throw Incomplete();
        }

        bool invalidId = stored.Id == Guid.Empty || !ids.Add(stored.Id);
        bool invalidInput = BookText.Problems(stored.Title, stored.Author).Count > 0;
        return (invalidId, invalidInput) switch
        {
            (true, _) => throw BadBook(),
            (_, true) => throw BadBook(),
            _ => new Book(stored.Id, stored.Title!, stored.Author!, RequireStatus(stored.ReadingStatus), stored.Metadata,
                stored.ShelfIds?.Where(shelfIds.Contains)),
        };
    }

    private static ReadingStatus RequireStatus(string? token)
    {
        return token switch
        {
            null => ReadingStatus.ToRead,
            "to-read" => ReadingStatus.ToRead,
            "reading" => ReadingStatus.Reading,
            "finished" => ReadingStatus.Finished,
            _ => throw BadBook(),
        };
    }

    private static List<StoredBook?> ToStored(IReadOnlyList<Book> books, IReadOnlyList<StoredShelf?> shelves)
    {
        HashSet<Guid> shelfIds = [.. shelves.Select(shelf => shelf!.Id)];
        List<StoredBook?> stored = new(books.Count);
        HashSet<Guid> ids = [];
        foreach (Book book in books)
        {
            stored.Add(ToStoredBook(book, ids, shelfIds));
        }

        return stored;
    }

    private static List<StoredShelf?> ToStoredShelves(IReadOnlyList<Shelf> shelves)
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

    private static StoredShelf ToStoredShelf(Shelf shelf, HashSet<Guid> ids, HashSet<string> names)
    {
        if (shelf is null)
        {
            throw BadShelf();
        }

        ShelfName name;
        try
        {
            name = ShelfName.Parse(shelf.Name);
        }
        catch (ShelfNameException exception)
        {
            throw BadShelf(exception);
        }

        return shelf.Id == Guid.Empty || !ids.Add(shelf.Id)
            || !string.Equals(name.Value, shelf.Name, StringComparison.Ordinal)
            || !names.Add(name.FoldedValue)
            ? throw BadShelf()
            : new StoredShelf { Id = shelf.Id, Name = name.Value };
    }

    private static StoredBook ToStoredBook(Book book, HashSet<Guid> ids, HashSet<Guid> shelfIds)
    {
        return book switch
        {
            null => throw BadBook(),
            _ when book.Id == Guid.Empty || !ids.Add(book.Id) || BookText.Problems(book.Title, book.Author).Count > 0
                || book.ShelfIds.Any(id => !shelfIds.Contains(id))
                => throw BadBook(),
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

    private static string Token(ReadingStatus status)
    {
        return status switch
        {
            ReadingStatus.ToRead => "to-read",
            ReadingStatus.Reading => "reading",
            ReadingStatus.Finished => "finished",
            _ => throw BadBook(),
        };
    }

    private static LibraryBackupException BadBook()
    {
        return new("This backup contains a book Folium cannot save.");
    }

    private static LibraryBackupException BadShelf(Exception? inner = null)
    {
        return new("This backup contains a shelf Folium cannot save.", inner);
    }

    private static LibraryBackupException Incomplete(Exception? inner = null)
    {
        return new("This backup is incomplete.", inner);
    }

    private sealed class BackupDocument
    {
        public string? Kind { get; set; }

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

        public List<Guid>? ShelfIds { get; set; }
    }

    private sealed class StoredShelf
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }
    }
}
