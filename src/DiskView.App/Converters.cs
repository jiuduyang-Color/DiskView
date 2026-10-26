using System.Globalization;
using System.Windows.Data;
using DiskView.Core;

namespace DiskView.App;

public sealed class SizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is long bytes ? Fmt.Bytes(bytes) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class PercentConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0d;
        long part = values[0] is long p ? p : 0;
        long total = values[1] is long t ? t : 0;
        return Fmt.PercentValue(part, total);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
