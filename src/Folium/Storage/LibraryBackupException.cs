namespace Folium.Storage;

/// <summary>A personal library backup cannot be read or written.</summary>
public sealed class LibraryBackupException : Exception
{
    /// <summary>Reports that the backup is unavailable.</summary>
    public LibraryBackupException()
        : base("The personal library backup is unavailable.")
    {
    }

    /// <summary>Reports a backup failure. The message must contain text.</summary>
    /// <param name="message">Text that explains the failure. The text must not be blank.</param>
    public LibraryBackupException(string message)
        : base(message)
    {
        RequireMessage(message);
    }

    /// <summary>Reports a backup failure and its cause. The message must contain text.</summary>
    /// <param name="message">Text that explains the failure. The text must not be blank.</param>
    /// <param name="innerException">The cause of the failure.</param>
    public LibraryBackupException(string message, Exception? innerException)
        : base(message, innerException)
    {
        RequireMessage(message);
    }

    private static void RequireMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A backup failure needs a message.", nameof(message));
        }
    }
}
