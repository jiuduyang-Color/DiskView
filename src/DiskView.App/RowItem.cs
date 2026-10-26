using System.Windows.Media;
using DiskView.Core;

namespace DiskView.App;

/// <summary>文件列表中的一行。</summary>
public sealed class RowItem
{
    private readonly long _parentSize;

    public RowItem(FileNode node, long parentSize)
    {
        Node = node;
        _parentSize = parentSize;
    }

    public FileNode Node { get; }
    public string Name => Node.Name;
    public long Size => Node.Size;
    public string SizeText => Fmt.Bytes(Node.Size);
    public double Percent => Fmt.PercentValue(Node.Size, _parentSize);
    public string PercentText => Fmt.Percent(Node.Size, _parentSize);
    public bool IsDirectory => Node.IsDirectory;
    public string ModifiedText => Fmt.Date(Node.ModifiedTicks);
    public DateTime Modified => Node.ModifiedTicks > 0
        ? DateTime.FromFileTimeUtc(Node.ModifiedTicks).ToLocalTime()
        : DateTime.MinValue;

    public string TypeText
    {
        get
        {
            if (Node.IsDirectory) return Loc.I["FileTypeFolder"];
            string extension = Node.Extension.TrimStart('.');
            return string.IsNullOrEmpty(extension) ? Loc.I["FileTypeFile"] : extension.ToUpperInvariant();
        }
    }

    public Brush CategoryBrush => CategoryPalette.BrushFor(Node.Extension);
}

/// <summary>按文件扩展名分类的配色，用于树图与文件列表。</summary>
public static class CategoryPalette
{
    public static readonly Brush FolderBrush = Freeze("#D3E2FB");
    public static readonly Brush FolderBorder = Freeze("#A9C6F3");

    private static readonly Brush Video = Freeze("#FFD6C4");
    private static readonly Brush Image = Freeze("#CDEDDA");
    private static readonly Brush Audio = Freeze("#E7D9FA");
    private static readonly Brush Archive = Freeze("#FFE8BA");
    private static readonly Brush Document = Freeze("#D2E6FA");
    private static readonly Brush Code = Freeze("#DAE4EC");
    private static readonly Brush Executable = Freeze("#E6D9D1");
    private static readonly Brush Other = Freeze("#E6EAF1");

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".mpg", ".mpeg", ".rmvb" };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".svg", ".ico", ".heic", ".psd", ".raw" };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".ape", ".alac" };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab", ".zst" };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".doc", ".docx", ".pdf", ".txt", ".ppt", ".pptx", ".xls", ".xlsx", ".md", ".rtf", ".csv", ".epub", ".odt" };

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".java", ".kt", ".js", ".ts", ".jsx", ".tsx", ".py", ".c", ".cpp", ".h", ".hpp", ".go", ".rs",
          ".php", ".rb", ".html", ".css", ".json", ".xml", ".yml", ".yaml", ".sql", ".sh", ".bat", ".ps1", ".sln", ".csproj" };

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".dll", ".sys", ".msi", ".appx", ".bin", ".so", ".lib" };

    public static Brush BrushFor(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return Other;
        if (VideoExtensions.Contains(extension)) return Video;
        if (ImageExtensions.Contains(extension)) return Image;
        if (AudioExtensions.Contains(extension)) return Audio;
        if (ArchiveExtensions.Contains(extension)) return Archive;
        if (DocumentExtensions.Contains(extension)) return Document;
        if (CodeExtensions.Contains(extension)) return Code;
        if (ExecutableExtensions.Contains(extension)) return Executable;
        return Other;
    }

    private static Brush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
