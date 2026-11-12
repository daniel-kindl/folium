namespace Folium.Updates;

/// <summary>Reports an update failure with a stable reason for the user interface.</summary>
/// <param name="reason">The stable update failure reason.</param>
/// <param name="message">A diagnostic message.</param>
/// <param name="innerException">The exception that caused this failure, if any.</param>
public sealed class UpdateFailureException(UpdateFailureReason reason, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Creates a general update failure.</summary>
    public UpdateFailureException() : this(UpdateFailureReason.Install, "The update failed.") { }

    /// <summary>Creates a general update failure.</summary>
    /// <param name="message">A diagnostic message.</param>
    public UpdateFailureException(string message) : this(UpdateFailureReason.Install, message) { }

    /// <summary>Creates a general update failure.</summary>
    /// <param name="message">A diagnostic message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public UpdateFailureException(string message, Exception innerException)
        : this(UpdateFailureReason.Install, message, innerException) { }

    /// <summary>The stable reason for this update failure.</summary>
    public UpdateFailureReason Reason { get; } = reason;
}
