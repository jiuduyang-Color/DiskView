using System.Globalization;

namespace DiskView.Core;

public static class Fmt
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Bytes(long bytes)
    {
        if (bytes < 0) bytes = 0;
        if (bytes < 1024) return bytes.ToString("N0", CultureInfo.CurrentCulture) + " B";

        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string format = value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + Units[unit];
    }

    public static string Count(long count) => count.ToString("N0", CultureInfo.CurrentCulture);

    public static string Percent(long part, long total)
    {
        if (total <= 0 || part <= 0) return "0%";
        double percent = part * 100.0 / total;
        if (percent >= 10) return percent.ToString("0", CultureInfo.CurrentCulture) + "%";
        if (percent >= 0.1) return percent.ToString("0.0", CultureInfo.CurrentCulture) + "%";
        return "<0.1%";
    }

    public static double PercentValue(long part, long total)
    {
        if (total <= 0 || part <= 0) return 0;
        return Math.Min(100.0, part * 100.0 / total);
    }

    public static string Date(long fileTimeTicks)
    {
        if (fileTimeTicks <= 0) return string.Empty;
        try
        {
            return DateTime.FromFileTimeUtc(fileTimeTicks).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return string.Empty;
        }
    }

    public static string Duration(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes >= 1)
            return elapsed.ToString(@"m\:ss", CultureInfo.CurrentCulture);
        return elapsed.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture) + "s";
    }
}
