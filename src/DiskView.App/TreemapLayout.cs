using System.Windows;
using DiskView.Core;

namespace DiskView.App;

internal sealed class TreemapSlice
{
    public FileNode? Node { get; init; }
    public string Label { get; init; } = string.Empty;
    public long Size { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsAggregate { get; init; }
    public Rect Bounds { get; set; }
}

internal static class TreemapLayout
{
    /// <summary>用 squarified 算法把切片铺满矩形。</summary>
    public static void Compute(IReadOnlyList<TreemapSlice> slices, Rect bounds)
    {
        var positive = slices.Where(s => s.Size > 0).OrderByDescending(s => s.Size).ToList();
        if (positive.Count == 0 || bounds.Width <= 1 || bounds.Height <= 1) return;

        long total = positive.Sum(s => s.Size);
        if (total <= 0) return;

        double density = bounds.Width * bounds.Height / total;
        var items = positive.Select(s => (Slice: s, Area: s.Size * density)).ToList();

        int index = 0;
        Rect remaining = bounds;
        while (index < items.Count && remaining.Width > 0.5 && remaining.Height > 0.5)
        {
            var row = new List<(TreemapSlice Slice, double Area)>();
            double rowArea = 0;
            double shortSide = Math.Min(remaining.Width, remaining.Height);
            double currentWorst = double.MaxValue;

            while (index < items.Count)
            {
                var candidate = items[index];
                double candidateSum = rowArea + candidate.Area;
                double candidateWorst = WorstWith(row, candidate.Area, candidateSum, shortSide);
                if (row.Count == 0 || candidateWorst <= currentWorst)
                {
                    row.Add(candidate);
                    rowArea = candidateSum;
                    currentWorst = candidateWorst;
                    index++;
                }
                else
                {
                    break;
                }
            }

            remaining = LayoutRow(row, rowArea, remaining);
        }
    }

    private static double WorstWith(List<(TreemapSlice Slice, double Area)> row, double candidateArea, double sum, double shortSide)
    {
        if (sum <= 0 || shortSide <= 0) return double.MaxValue;
        double max = candidateArea;
        double min = candidateArea;
        foreach (var item in row)
        {
            if (item.Area > max) max = item.Area;
            if (item.Area < min) min = item.Area;
        }

        double sideSquared = shortSide * shortSide;
        double sumSquared = sum * sum;
        return Math.Max(sideSquared * max / sumSquared, sumSquared / (sideSquared * min));
    }

    private static Rect LayoutRow(List<(TreemapSlice Slice, double Area)> row, double rowArea, Rect rect)
    {
        if (row.Count == 0 || rowArea <= 0) return rect;

        if (rect.Width >= rect.Height)
        {
            double thickness = Math.Min(rect.Width, rowArea / Math.Max(rect.Height, 0.0001));
            if (thickness <= 0) return rect;

            double y = rect.Y;
            foreach (var (slice, area) in row)
            {
                double height = Math.Max(0, Math.Min(area / thickness, rect.Bottom - y));
                slice.Bounds = new Rect(rect.X, y, thickness, height);
                y += height;
            }

            return new Rect(rect.X + thickness, rect.Y, Math.Max(0, rect.Width - thickness), rect.Height);
        }
        else
        {
            double thickness = Math.Min(rect.Height, rowArea / Math.Max(rect.Width, 0.0001));
            if (thickness <= 0) return rect;

            double x = rect.X;
            foreach (var (slice, area) in row)
            {
                double width = Math.Max(0, Math.Min(area / thickness, rect.Right - x));
                slice.Bounds = new Rect(x, rect.Y, width, thickness);
                x += width;
            }

            return new Rect(rect.X, rect.Y + thickness, rect.Width, Math.Max(0, rect.Height - thickness));
        }
    }
}
