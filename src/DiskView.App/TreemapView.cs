using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DiskView.Core;

namespace DiskView.App;

public sealed class TreemapNodeEventArgs : EventArgs
{
    public TreemapNodeEventArgs(FileNode node) => Node = node;
    public FileNode Node { get; }
}

/// <summary>矩形树图控件：展示当前文件夹下各子项的空间占比。</summary>
public sealed class TreemapView : FrameworkElement
{
    private const int MaxSlices = 220;

    private static readonly Typeface RegularTypeface = new(
        new FontFamily("Microsoft YaHei UI, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static readonly Typeface SemiboldTypeface = new(
        new FontFamily("Microsoft YaHei UI, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private static readonly Brush TextBrush = Freeze("#24303F");
    private static readonly Brush SubTextBrush = Freeze("#5B6A7E");
    private static readonly Brush HoverPenBrush = Freeze("#2F6BE8");
    private static readonly Brush SeparatorBrush = Brushes.White;

    private readonly List<TreemapSlice> _slices = new();
    private TreemapSlice? _hovered;

    public static readonly DependencyProperty FolderProperty = DependencyProperty.Register(
        nameof(Folder), typeof(FileNode), typeof(TreemapView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFolderChanged));

    public TreemapView()
    {
        SnapsToDevicePixels = true;
        ToolTipService.SetInitialShowDelay(this, 450);
        ToolTipService.SetShowDuration(this, 20000);
    }

    public FileNode? Folder
    {
        get => (FileNode?)GetValue(FolderProperty);
        set => SetValue(FolderProperty, value);
    }

    public event EventHandler<TreemapNodeEventArgs>? NavigateRequested;
    public event EventHandler<TreemapNodeEventArgs>? FileSelected;
    public event EventHandler<TreemapNodeEventArgs>? FileActivated;
    public event EventHandler<TreemapNodeEventArgs>? NodeRightClicked;

    private static void OnFolderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (TreemapView)d;
        view._hovered = null;
        view.ToolTip = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Brushes.White, null, bounds);

        _slices.Clear();
        if (Folder == null || ActualWidth < 40 || ActualHeight < 40) return;

        var slices = BuildSlices(Folder);
        if (slices.Count == 0)
        {
            DrawCentered(dc, Loc.I["EmptyFolder"]);
            return;
        }

        var mapBounds = new Rect(6, 6, ActualWidth - 12, ActualHeight - 12);
        TreemapLayout.Compute(slices, mapBounds);
        _slices.AddRange(slices);

        var dpi = VisualTreeHelper.GetDpi(this);
        foreach (var slice in _slices)
        {
            DrawSlice(dc, slice, dpi.PixelsPerDip);
        }

        if (_hovered != null && _hovered.Bounds.Width > 2 && _hovered.Bounds.Height > 2)
        {
            var hoverRect = _hovered.Bounds;
            hoverRect.Inflate(-0.5, -0.5);
            dc.DrawRectangle(null, new Pen(HoverPenBrush, 2), hoverRect);
        }
    }

    private void DrawSlice(DrawingContext dc, TreemapSlice slice, double pixelsPerDip)
    {
        var rect = slice.Bounds;
        rect.Inflate(-1.2, -1.2);
        if (rect.Width <= 1 || rect.Height <= 1) return;

        Brush fill = slice.IsDirectory
            ? CategoryPalette.FolderBrush
            : slice.Node == null
                ? CategoryPalette.BrushFor(string.Empty)
                : CategoryPalette.BrushFor(slice.Node.Extension);

        dc.DrawRectangle(fill, new Pen(SeparatorBrush, 1.2), rect);

        if (rect.Width < 34 || rect.Height < 18) return;

        string percent = Fmt.Percent(slice.Size, Folder?.Size ?? 0);
        string sizeLine = $"{Fmt.Bytes(slice.Size)} · {percent}";

        var nameArea = new Rect(rect.X + 6, rect.Y + 4, rect.Width - 12, 16);
        DrawText(dc, slice.Label, 11.5, TextBrush, SemiboldTypeface, nameArea, pixelsPerDip);

        if (rect.Height >= 40 && rect.Width >= 62)
        {
            var sizeArea = new Rect(rect.X + 6, rect.Y + 21, rect.Width - 12, 14);
            DrawText(dc, sizeLine, 10, SubTextBrush, RegularTypeface, sizeArea, pixelsPerDip);
        }
    }

    private void DrawText(DrawingContext dc, string text, double fontSize, Brush brush, Typeface typeface, Rect area, double pixelsPerDip)
    {
        if (area.Width < 8 || area.Height < 8) return;
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            typeface, fontSize, brush, pixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, area.Width),
            MaxTextHeight = Math.Max(1, area.Height),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(formatted, area.TopLeft);
    }

    private void DrawCentered(DrawingContext dc, string text)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            RegularTypeface, 12, SubTextBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point((ActualWidth - formatted.Width) / 2, (ActualHeight - formatted.Height) / 2));
    }

    private List<TreemapSlice> BuildSlices(FileNode folder)
    {
        var children = new List<FileNode>(folder.SubDirectories.Count + folder.Files.Count);
        children.AddRange(folder.SubDirectories);
        children.AddRange(folder.Files);
        children.RemoveAll(static n => n.Size <= 0);
        children.Sort(static (a, b) => b.Size.CompareTo(a.Size));

        var result = new List<TreemapSlice>();
        int take = children.Count > MaxSlices ? MaxSlices - 1 : children.Count;

        for (int i = 0; i < take; i++)
        {
            var node = children[i];
            result.Add(new TreemapSlice
            {
                Node = node,
                Label = node.Name,
                Size = node.Size,
                IsDirectory = node.IsDirectory
            });
        }

        if (children.Count > take)
        {
            long rest = 0;
            for (int i = take; i < children.Count; i++) rest += children[i].Size;
            result.Add(new TreemapSlice
            {
                Node = null,
                Label = Loc.I.Format("OtherGroup", children.Count - take),
                Size = rest,
                IsDirectory = false,
                IsAggregate = true
            });
        }

        return result;
    }

    private TreemapSlice? HitTestPoint(Point point)
    {
        foreach (var slice in _slices)
        {
            if (slice.Bounds.Contains(point)) return slice;
        }
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTestPoint(e.GetPosition(this));
        if (ReferenceEquals(hit, _hovered)) return;

        _hovered = hit;
        Cursor = hit?.Node is { IsDirectory: true } ? Cursors.Hand : Cursors.Arrow;
        ToolTip = hit == null ? null : BuildTooltip(hit);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hovered == null) return;
        _hovered = null;
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var hit = HitTestPoint(e.GetPosition(this));
        if (hit?.Node == null) return;

        if (e.ClickCount == 2)
        {
            if (!hit.Node.IsDirectory) FileActivated?.Invoke(this, new TreemapNodeEventArgs(hit.Node));
            return;
        }

        if (hit.Node.IsDirectory)
            NavigateRequested?.Invoke(this, new TreemapNodeEventArgs(hit.Node));
        else
            FileSelected?.Invoke(this, new TreemapNodeEventArgs(hit.Node));

        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        var hit = HitTestPoint(e.GetPosition(this));
        if (hit?.Node == null) return;

        _hovered = hit;
        InvalidateVisual();
        NodeRightClicked?.Invoke(this, new TreemapNodeEventArgs(hit.Node));
        e.Handled = true;
    }

    private string BuildTooltip(TreemapSlice slice)
    {
        if (slice.Node == null) return $"{slice.Label}\n{Fmt.Bytes(slice.Size)}";

        string percent = Fmt.Percent(slice.Size, Folder?.Size ?? 0);
        string kind = slice.Node.IsDirectory
            ? Loc.I["FileTypeFolder"]
            : Loc.I["FileTypeFile"];
        return $"{slice.Node.Name}\n{kind} · {Fmt.Bytes(slice.Size)} · {percent}\n{slice.Node.FullPath}";
    }

    private static Brush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
