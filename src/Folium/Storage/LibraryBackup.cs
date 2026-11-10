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
    private const int VERSION = 1;
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
        BackupDocument document = new()
        {
            Kind = KIND,
            Version = VERSION,
            Books = ToStored(books),
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
        ArgumentNullException.ThrowIfNull(json);
        return Encoding.UTF8.GetByteCount(json) > MAXIMUM_BYTES
            ? throw new LibraryBackupException("This backup is too large to restore.")
            : ToBooks(Parse(json));
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

    private static List<Book> ToBooks(BackupDocument document)
    {
        if (!string.Equals(document.Kind, KIND, StringComparison.Ordinal))
        {
            throw new LibraryBackupException("This file is not a Folium backup.");
        }

        if (document.Version != VERSION)
        {
            throw new LibraryBackupException(
                string.Create(CultureInfo.InvariantCulture, $"This backup uses version {document.Version}. Folium reads version 1."));
        }

        if (document.Books is null)
        {
            throw Incomplete();
        }

        List<Book> books = new(document.Books.Count);
        HashSet<Guid> ids = [];
        foreach (StoredBook? stored in document.Books)
        {
            books.Add(stored is null ? throw Incomplete() : ToBook(stored, ids));
        }

        return books;
    }

    private static Book ToBook(StoredBook stored, HashSet<Guid> ids)
    {
        bool invalidId = stored.Id == Guid.Empty || !ids.Add(stored.Id);
        bool invalidInput = BookText.Problems(stored.Title, stored.Author).Count > 0;
        return (invalidId, invalidInput) switch
        {
            (true, _) => throw BadBook(),
            (_, true) => throw BadBook(),
            _ => new Book(stored.Id, stored.Title!, stored.Author!, RequireStatus(stored.ReadingStatus), stored.Metadata),
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

    private static List<StoredBook?> ToStored(IReadOnlyList<Book> books)
    {
        List<StoredBook?> stored = new(books.Count);
        HashSet<Guid> ids = [];
        foreach (Book book in books)
        {
            stored.Add(ToStoredBook(book, ids));
        }

        return stored;
    }

    private static StoredBook ToStoredBook(Book book, HashSet<Guid> ids)
    {
        return book switch
        {
            null => throw BadBook(),
            _ when book.Id == Guid.Empty || !ids.Add(book.Id) || BookText.Problems(book.Title, book.Author).Count > 0
                => throw BadBook(),
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

    private static LibraryBackupException Incomplete(Exception? inner = null)
    {
        return new("This backup is incomplete.", inner);
    }

    private sealed class BackupDocument
    {
        public string? Kind { get; set; }

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
