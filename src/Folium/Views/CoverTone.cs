using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Folium.Views;

/// <summary>A cover placeholder frame. Its fill and ink follow the theme of the control, also after a theme change.</summary>
public sealed class CoverTone : Border
{
    /// <summary>Defines the <see cref="Tone"/> property.</summary>
    public static readonly StyledProperty<string?> ToneProperty =
        AvaloniaProperty.Register<CoverTone, string?>(nameof(Tone));

    /// <summary>Defines the <see cref="Ink"/> property.</summary>
    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<CoverTone, IBrush?>(nameof(Ink));

    private IDisposable? _fill;
    private IDisposable? _ink;

    static CoverTone()
    {
        _ = ToneProperty.Changed.AddClassHandler<CoverTone>((cover, _) => cover.BindTone());
    }

    /// <summary>Creates a placeholder that uses the stone tone until a tone is set.</summary>
    public CoverTone()
    {
        BindTone();
    }

    /// <summary>The tone name, for example sage. A blank or unknown name uses stone.</summary>
    public string? Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>The text color for the tone. Text on the placeholder binds to this brush.</summary>
    public IBrush? Ink
    {
        get => GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    /// <summary>Returns the resource key of a tone brush.</summary>
    /// <param name="tone">The tone name. A blank or unknown name uses stone.</param>
    /// <param name="ink">True for the ink brush. False for the fill brush.</param>
    public static string BrushKey(string? tone, bool ink)
    {
        string name = tone is not null && CoverPlaceholder.IsTone(tone) ? tone : "stone";
        return $"FoliumCover{char.ToUpperInvariant(name[0])}{name[1..]}{(ink ? "Ink" : "")}Brush";
    }

    private void BindTone()
    {
        _fill?.Dispose();
        _ink?.Dispose();
        _fill = Bind(BackgroundProperty, this.GetResourceObservable(BrushKey(Tone, ink: false)));
        _ink = Bind(InkProperty, this.GetResourceObservable(BrushKey(Tone, ink: true)));
    }
}
