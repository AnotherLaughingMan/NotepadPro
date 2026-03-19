using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace NotepadPro.Converters;

public sealed class DepthToIndentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var depth = value is int d ? Math.Max(0, d) : 0;
        var perLevel = 12.0;
        var baseOffset = -6.0;

        if (parameter is string raw && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            perLevel = parsed;
        }

        return new Thickness((depth * perLevel) + baseOffset, 0, 0, 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return 0;
    }
}
