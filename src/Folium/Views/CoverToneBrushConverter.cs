using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace Folium.Views;

/// <summary>Maps a cover tone name to its theme brush.</summary>
public sealed class CoverToneBrushConverter : IValueConverter
{
    /// <summary>Returns the tone brush. The parameter "ink" selects the ink brush.</summary>
    /// <param name="value">The tone name. An unknown name uses stone.</param>
    /// <param name="targetType">The requested target type. This converter ignores it.</param>
    /// <param name="parameter">Pass "ink" for the ink brush. Any other value selects the tone fill.</param>
    /// <param name="culture">The conversion culture. This converter ignores it.</param>
    /// <returns>The theme brush for the tone, or null when the theme has no matching brush.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string tone = value as string ?? "";
        if (tone.Length == 0)
        {
            tone = "stone";
        }

        bool ink = string.Equals(parameter as string, "ink", StringComparison.Ordinal);
        string inkName = ink ? "Ink" : "";
        string key = $"FoliumCover{char.ToUpperInvariant(tone[0])}{tone[1..]}{inkName}Brush";
        Application? application = Application.Current;
        return application?.TryGetResource(key, application.ActualThemeVariant ?? ThemeVariant.Default, out object? brush) == true
            ? brush
            : null;
    }

    /// <summary>This converter does not write a tone back to the book.</summary>
    /// <param name="value">The brush. This converter ignores it.</param>
    /// <param name="targetType">The requested target type. This converter ignores it.</param>
    /// <param name="parameter">The converter parameter. This converter ignores it.</param>
    /// <param name="culture">The conversion culture. This converter ignores it.</param>
    /// <returns>This method does not return.</returns>
    /// <exception cref="NotSupportedException">The tone is chosen from the book id, not from the brush.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("The cover tone comes from the book id.");
    }
}
