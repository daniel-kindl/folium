using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Folium.Views;

/// <summary>Displays private cover bytes and releases the bitmap when detached.</summary>
public sealed class LocalCover : Image, IDisposable
{
    /// <summary>The bytes of the saved cover.</summary>
    public static readonly StyledProperty<ReadOnlyMemory<byte>> BytesProperty =
        AvaloniaProperty.Register<LocalCover, ReadOnlyMemory<byte>>(nameof(Bytes));

    private Bitmap? _bitmap;

    /// <summary>The private cover bytes. Empty or invalid bytes display no image.</summary>
    public ReadOnlyMemory<byte> Bytes
    {
        get => GetValue(BytesProperty);
        set => SetValue(BytesProperty, value);
    }

    /// <summary>Decodes a new cover when the bound bytes change.</summary>
    /// <param name="change">The property change.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == BytesProperty)
        {
            UpdateCover();
        }
    }

    /// <summary>Restores the image after the control is attached again.</summary>
    /// <param name="e">The visual tree event.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateCover();
    }

    /// <summary>Releases the decoded image.</summary>
    /// <param name="e">The visual tree event.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ClearCover();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Releases the current bitmap.</summary>
    public void Dispose()
    {
        ClearCover();
        GC.SuppressFinalize(this);
    }

    private void UpdateCover()
    {
        ClearCover();
        if (Bytes.IsEmpty)
        {
            return;
        }

        try
        {
            using MemoryStream stream = new(Bytes.ToArray());
            _bitmap = new Bitmap(stream);
            Source = _bitmap;
        }
        catch (ArgumentException exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    private void ClearCover()
    {
        Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
