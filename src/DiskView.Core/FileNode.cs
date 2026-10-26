using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace DiskView.Core;

/// <summary>文件或文件夹节点。文件夹的 Size 为子项大小之和。</summary>
public sealed class FileNode : INotifyPropertyChanged
{
    private List<FileNode> _subDirectories = new();
    private List<FileNode> _files = new();
    private long _size;

    public FileNode(string name, string fullPath, bool isDirectory, long size, long modifiedTicks, bool isReparsePoint = false)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        _size = size;
        ModifiedTicks = modifiedTicks;
        IsReparsePoint = isReparsePoint;
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public bool IsReparsePoint { get; }
    public long ModifiedTicks { get; }
    public FileNode? Parent { get; internal set; }
    public bool AccessDenied { get; internal set; }
    public string? ErrorMessage { get; internal set; }

    public long Size
    {
        get => _size;
        internal set
        {
            if (_size == value) return;
            _size = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<FileNode> SubDirectories => _subDirectories;
    public IReadOnlyList<FileNode> Files => _files;
    public int ChildCount => _subDirectories.Count + _files.Count;

    public string Extension => IsDirectory ? string.Empty : Path.GetExtension(Name).ToLowerInvariant();

    internal void AddSizeSilent(long bytes) => _size += bytes;

    internal void SetChildren(List<FileNode> directories, List<FileNode> files)
    {
        _subDirectories = directories;
        _files = files;
        OnPropertyChanged(nameof(SubDirectories));
        OnPropertyChanged(nameof(Files));
        OnPropertyChanged(nameof(ChildCount));
    }

    public void SortChildren()
    {
        _subDirectories.Sort(static (a, b) => b.Size.CompareTo(a.Size));
        _files.Sort(static (a, b) => b.Size.CompareTo(a.Size));
    }

    /// <summary>用同名路径的新扫描结果替换自身内容，并把大小变化同步到所有祖先节点。</summary>
    public void ReplaceWith(FileNode source)
    {
        if (!string.Equals(FullPath, source.FullPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Node paths do not match.", nameof(source));

        long previousSize = _size;
        foreach (var dir in source._subDirectories) dir.Parent = this;
        foreach (var file in source._files) file.Parent = this;

        AccessDenied = source.AccessDenied;
        ErrorMessage = source.ErrorMessage;
        Size = source.Size;
        SetChildren(source._subDirectories, source._files);

        long delta = source.Size - previousSize;
        if (delta != 0)
        {
            for (var ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
                ancestor.Size += delta;
        }
    }

    /// <summary>从当前节点向下按相对路径查找子节点。</summary>
    public FileNode? FindDescendant(string path)
    {
        if (string.IsNullOrEmpty(path)) return this;
        string relative = path.TrimStart(Path.DirectorySeparatorChar);
        FileNode current = this;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            FileNode? next = null;
            foreach (var child in current._subDirectories)
            {
                if (string.Equals(child.Name, segment, StringComparison.OrdinalIgnoreCase))
                {
                    next = child;
                    break;
                }
            }
            if (next == null) return null;
            current = next;
        }
        return current;
    }

    public int CountFiles()
    {
        int total = _files.Count;
        foreach (var child in _subDirectories) total += child.CountFiles();
        return total;
    }

    public int CountFolders()
    {
        int total = 1;
        foreach (var child in _subDirectories) total += child.CountFolders();
        return total;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
