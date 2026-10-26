using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DiskView.Core;

namespace DiskView.App;

public partial class MainWindow : Window
{
    private readonly DiskScanner _scanner = new();
    private readonly List<FileNode> _context = new();

    private CancellationTokenSource? _scanCts;
    private int _scanGeneration;
    private int _lastDriveIndex;
    private bool _suppressDriveEvent;
    private bool _suppressTreeSync;
    private bool _busy;

    private FileNode? _root;
    private FileNode? _current;

    private long _totalFiles;
    private long _totalFolders;
    private TimeSpan _scanElapsed;
    private int _accessDenied;
    private string _message = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        LoadDrives();
        UpdateStatusBar();
    }

    private IntPtr OwnerHandle => new WindowInteropHelper(this).Handle;

    private IReadOnlyList<FileNode> Targets
    {
        get
        {
            if (_context.Count > 0) return _context;
            var selected = GetSelectedNodes();
            if (selected.Count > 0) return selected;

            // 仅在目录树获得键盘焦点时才把当前目录作为操作对象，避免误删正在浏览的目录
            if (FolderTree.IsKeyboardFocusWithin && _current is { Parent: not null }) return new[] { _current };
            return Array.Empty<FileNode>();
        }
    }

    private FileNode? TargetFolder
    {
        get
        {
            foreach (var node in _context)
                if (node.IsDirectory) return node;
            return _current;
        }
    }

    // ---------------------------------------------------------------- 磁盘与扫描

    private void LoadDrives()
    {
        var items = new List<DriveItem>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network)) continue;

                string display = $"{drive.Name}  {Fmt.Bytes(drive.AvailableFreeSpace)} / {Fmt.Bytes(drive.TotalSize)}";
                items.Add(new DriveItem { Path = drive.RootDirectory.FullName, Display = display });
            }
            catch (IOException)
            {
                // 忽略无法读取的驱动器
            }
            catch (UnauthorizedAccessException)
            {
                // 忽略无权访问的驱动器
            }
        }

        items.Add(new DriveItem { Path = string.Empty, Display = Loc.I["BrowseFolder"], IsBrowse = true });

        _suppressDriveEvent = true;
        DriveCombo.ItemsSource = items;
        DriveCombo.SelectedIndex = items.Count > 1 ? 0 : 0;
        _lastDriveIndex = DriveCombo.SelectedIndex;
        _suppressDriveEvent = false;
    }

    private void DriveCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDriveEvent) return;
        if (DriveCombo.SelectedItem is not DriveItem item) return;

        if (item.IsBrowse)
        {
            _suppressDriveEvent = true;
            DriveCombo.SelectedIndex = _lastDriveIndex;
            _suppressDriveEvent = false;
            BrowseAndScan();
            return;
        }

        _lastDriveIndex = DriveCombo.SelectedIndex;
        StartScan(item.Path);
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (DriveCombo.SelectedItem is DriveItem { IsBrowse: false } item)
            StartScan(item.Path);
        else if (_root != null)
            StartScan(_root.FullPath);
        else
            BrowseAndScan();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current != null)
            _ = RefreshFolderAsync(_current);
        else if (DriveCombo.SelectedItem is DriveItem { IsBrowse: false } item)
            StartScan(item.Path);
    }

    private void BrowseAndScan()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Loc.I["BrowseFolder"] };
        if (dialog.ShowDialog(this) == true)
            StartScan(dialog.FolderName);
    }

    private async void StartScan(string path)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        int generation = ++_scanGeneration;
        _busy = true;
        _context.Clear();
        _message = string.Empty;
        ShowScanOverlay(path, Loc.I["Scanning"]);

        var progress = new Progress<ScanStats>(stats =>
        {
            if (generation == _scanGeneration) UpdateScanProgress(stats);
        });

        try
        {
            var result = await Task.Run(() => _scanner.Scan(path, progress, token), token);
            if (generation != _scanGeneration) return;
            ApplyScanResult(result);
        }
        catch (OperationCanceledException)
        {
            if (generation == _scanGeneration)
            {
                _message = Loc.I["ScanCancelled"];
                UpdateStatusBar();
            }
        }
        catch (Exception ex)
        {
            if (generation == _scanGeneration)
                PromptDialog.Info(this, Loc.I["Error"], Loc.I.Format("ScanFailed", ex.Message));
        }
        finally
        {
            if (generation == _scanGeneration)
            {
                _busy = false;
                HideScanOverlay();
            }
        }
    }

    private async Task RefreshFolderAsync(FileNode folder, string? followPath = null)
    {
        if (_busy) return;
        _busy = true;

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        int generation = ++_scanGeneration;
        ShowScanOverlay(folder.FullPath, Loc.I["Refresh"]);

        try
        {
            var scanner = new DiskScanner();
            var result = await Task.Run(() => scanner.Scan(folder.FullPath, null, token), token);
            if (generation != _scanGeneration) return;

            folder.ReplaceWith(result.Root);
            for (var ancestor = folder.Parent; ancestor != null; ancestor = ancestor.Parent)
                ancestor.SortChildren();

            FileNode? target = null;
            if (followPath != null) target = FindByPath(folder, followPath);
            if (target == null && _current != null)
            {
                target = FindByPath(folder, _current.FullPath);
                if (target == null && IsInside(folder.FullPath, _current.FullPath)) target = _current;
            }
            SetCurrent(target ?? folder, true);
        }
        catch (OperationCanceledException)
        {
            _message = Loc.I["ScanCancelled"];
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            PromptDialog.Info(this, Loc.I["Error"], Loc.I.Format("ScanFailed", ex.Message));
        }
        finally
        {
            if (generation == _scanGeneration)
            {
                _busy = false;
                HideScanOverlay();
            }
        }
    }

    private void ApplyScanResult(ScanResult result)
    {
        _root = result.Root;
        _totalFiles = result.TotalFiles;
        _totalFolders = result.TotalFolders;
        _scanElapsed = result.Elapsed;
        _accessDenied = result.AccessDeniedCount;
        _message = string.Empty;

        _suppressTreeSync = true;
        FolderTree.ItemsSource = new[] { _root };
        _suppressTreeSync = false;

        TreeSummary.Text = Loc.I.Format("ColumnsItems", Fmt.Count(result.TotalFolders - 1));
        SetCurrent(_root, true);
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        _busy = false;
    }

    private void ShowScanOverlay(string path, string title)
    {
        ScanTitle.Text = title;
        ScanNote.Text = Loc.I["AppTitle"];
        ScanStats.Text = string.Empty;
        ScanCurrent.Text = path;
        ScanOverlay.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
    }

    private void UpdateScanProgress(ScanStats stats)
    {
        ScanStats.Text = $"{Loc.I.Format("StatusFiles", Fmt.Count(stats.Files))} · " +
                         $"{Loc.I.Format("StatusFolders", Fmt.Count(stats.Folders))} · {Fmt.Bytes(stats.Bytes)}";
        ScanCurrent.Text = stats.CurrentDirectory;
    }

    private void HideScanOverlay()
    {
        ScanOverlay.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = _root == null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- 导航

    private void SetCurrent(FileNode? node, bool syncTree)
    {
        _current = node;
        _context.Clear();

        if (node == null)
        {
            PathText.Text = Loc.I["EmptyTitle"];
            UpButton.IsEnabled = false;
            Treemap.Folder = null;
            TreemapFolderName.Text = string.Empty;
            FileGrid.ItemsSource = null;
            FileHeaderInfo.Text = string.Empty;
            UpdateStatusBar();
            return;
        }

        PathText.Text = node.FullPath;
        UpButton.IsEnabled = node.Parent != null;
        Treemap.Folder = node;
        TreemapFolderName.Text = node.Name;

        BuildRows();
        if (syncTree) SelectNodeInTree(node);
        UpdateStatusBar();
    }

    private void BuildRows()
    {
        if (_current == null)
        {
            FileGrid.ItemsSource = null;
            FileHeaderInfo.Text = string.Empty;
            return;
        }

        long parentSize = Math.Max(1, _current.Size);
        string filter = SearchBox.Text.Trim();
        bool filtering = filter.Length > 0;

        var rows = new List<RowItem>(_current.ChildCount);
        foreach (var dir in _current.SubDirectories)
        {
            if (filtering && !dir.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            rows.Add(new RowItem(dir, parentSize));
        }
        foreach (var file in _current.Files)
        {
            if (filtering && !file.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            rows.Add(new RowItem(file, parentSize));
        }

        FileGrid.ItemsSource = rows;
        FileHeaderInfo.Text = Loc.I.Format("ColumnsItems", Fmt.Count(rows.Count));
    }

    private void SelectNodeInTree(FileNode node)
    {
        if (_root == null) return;

        bool previous = _suppressTreeSync;
        _suppressTreeSync = true;
        try
        {
            var chain = new List<FileNode>();
            for (var n = node; n != null; n = n.Parent)
            {
                chain.Add(n);
                if (ReferenceEquals(n, _root)) break;
            }
            chain.Reverse();

            ItemsControl parent = FolderTree;
            TreeViewItem? container = null;

            foreach (var item in chain)
            {
                var next = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (next == null) break;
                container = next;
                if (!ReferenceEquals(item, node))
                {
                    container.IsExpanded = true;
                    container.UpdateLayout();
                }
                parent = container;
            }

            if (container != null)
            {
                if (ReferenceEquals(node, _root)) container.IsExpanded = true;
                container.IsSelected = true;
                container.BringIntoView();
            }
        }
        finally
        {
            _suppressTreeSync = previous;
        }
    }

    private void SelectRowForNode(FileNode node)
    {
        foreach (var item in FileGrid.Items)
        {
            if (item is RowItem row && ReferenceEquals(row.Node, node))
            {
                FileGrid.SelectedItem = row;
                FileGrid.ScrollIntoView(row);
                return;
            }
        }
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressTreeSync) return;
        if (e.NewValue is FileNode node) SetCurrent(node, false);
    }

    private void FolderTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is FileNode node)
        {
            item.IsSelected = true;
            _context.Clear();
            _context.Add(node);
        }
        else
        {
            _context.Clear();
        }
    }

    private void UpButton_Click(object sender, RoutedEventArgs e) => GoUp();

    private void GoUp()
    {
        if (_current?.Parent == null) return;
        SetCurrent(_current.Parent, true);
    }

    private void CopyPathButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        Clipboard.SetText(_current.FullPath);
        Notify(Loc.I["CopyPath"]);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        BuildRows();
    }

    // ---------------------------------------------------------------- 树图

    private void Treemap_NavigateRequested(object sender, TreemapNodeEventArgs e) => SetCurrent(e.Node, true);

    private void Treemap_FileSelected(object sender, TreemapNodeEventArgs e)
    {
        _context.Clear();
        SelectRowForNode(e.Node);
    }

    private void Treemap_FileActivated(object sender, TreemapNodeEventArgs e)
    {
        if (!FileOperations.Open(e.Node.FullPath))
            PromptDialog.Info(this, Loc.I["Error"], Loc.I["CannotOpen"]);
    }

    private void Treemap_NodeRightClicked(object sender, TreemapNodeEventArgs e)
    {
        _context.Clear();
        _context.Add(e.Node);
        if (!e.Node.IsDirectory) SelectRowForNode(e.Node);

        var key = e.Node.IsDirectory ? "FolderMenu" : "FileMenu";
        if (Resources[key] is not ContextMenu menu) return;

        menu.PlacementTarget = Treemap;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    // ---------------------------------------------------------------- 文件列表

    private void FileGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _context.Clear();
        UpdateStatusBar();
    }

    private void FileGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is RowItem item)
        {
            if (!row.IsSelected)
            {
                FileGrid.SelectedItems.Clear();
                row.IsSelected = true;
            }
            _context.Clear();
            _context.AddRange(GetSelectedNodes());
        }
        else
        {
            _context.Clear();
        }
    }

    private void FileGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is not RowItem item) return;

        if (item.IsDirectory)
            SetCurrent(item.Node, true);
        else if (!FileOperations.Open(item.Node.FullPath))
            PromptDialog.Info(this, Loc.I["Error"], Loc.I["CannotOpen"]);

        e.Handled = true;
    }

    private List<FileNode> GetSelectedNodes()
    {
        var nodes = new List<FileNode>();
        foreach (var item in FileGrid.SelectedItems)
            if (item is RowItem row) nodes.Add(row.Node);
        return nodes;
    }

    // ---------------------------------------------------------------- 文件操作

    private void Open_Click(object sender, RoutedEventArgs e) => OpenTarget();

    private void OpenTarget()
    {
        var node = Targets.FirstOrDefault();
        if (node == null) return;

        if (node.IsDirectory) SetCurrent(node, true);
        else if (!FileOperations.Open(node.FullPath))
            PromptDialog.Info(this, Loc.I["Error"], Loc.I["CannotOpen"]);
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        var node = Targets.FirstOrDefault();
        if (node != null) FileOperations.RevealInExplorer(node.FullPath);
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        var node = Targets.FirstOrDefault();
        if (node == null) return;
        Clipboard.SetText(node.FullPath);
        Notify(Loc.I["CopyPath"]);
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => CopySelected(cut: false);

    private void Cut_Click(object sender, RoutedEventArgs e) => CopySelected(cut: true);

    private void CopySelected(bool cut)
    {
        var nodes = Targets;
        if (nodes.Count == 0) return;

        FileClipboard.Set(nodes.Select(n => n.FullPath).ToArray(), cut);
        Notify(Loc.I.Format(cut ? "CutDone" : "CopyDone", nodes.Count));
    }

    private void Paste_Click(object sender, RoutedEventArgs e) => PasteClipboard();

    private async void PasteClipboard()
    {
        var destination = TargetFolder;
        if (destination == null) return;

        var (paths, cut) = FileClipboard.Get();
        if (paths.Length == 0)
        {
            PromptDialog.Info(this, Loc.I["Info"], Loc.I["ClipboardEmpty"]);
            return;
        }

        foreach (string path in paths)
        {
            string? parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar));
            if (string.Equals(parent, destination.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                PromptDialog.Info(this, Loc.I["Info"], Loc.I["PasteSameFolder"]);
                return;
            }

            if (Directory.Exists(path) && IsInside(destination.FullPath, path))
            {
                PromptDialog.Info(this, Loc.I["Info"], Loc.I["PasteIntoChild"]);
                return;
            }
        }

        var result = cut
            ? FileOperations.Move(paths, destination.FullPath, OwnerHandle)
            : FileOperations.Copy(paths, destination.FullPath, OwnerHandle);

        if (result.Cancelled) return;
        if (!result.Success)
        {
            PromptDialog.Info(this, Loc.I["Error"], result.Message ?? Loc.I.Format("OperationFailed", $"0x{result.Code:X}"));
            return;
        }

        if (cut) Clipboard.Clear();
        Notify(Loc.I.Format("PasteDone", paths.Length));
        await RefreshFolderAsync(destination);
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected(permanent: false);

    private void DeletePermanent_Click(object sender, RoutedEventArgs e) => DeleteSelected(permanent: true);

    private async void DeleteSelected(bool permanent)
    {
        var nodes = Targets;
        if (nodes.Count == 0) return;

        string message = nodes.Count == 1
            ? Loc.I.Format(permanent ? "ConfirmDeletePermanentOne" : "ConfirmDeleteToOne", nodes[0].Name)
            : Loc.I.Format(permanent ? "ConfirmDeletePermanentMany" : "ConfirmDeleteToMany", nodes.Count);

        if (!PromptDialog.Confirm(this, Loc.I["ConfirmDeleteTitle"], message, Loc.I["BtnDelete"])) return;

        var result = FileOperations.Delete(nodes.Select(n => n.FullPath).ToArray(), permanent, OwnerHandle);
        if (result.Cancelled) return;
        if (!result.Success)
        {
            PromptDialog.Info(this, Loc.I["Error"], result.Message ?? Loc.I.Format("OperationFailed", $"0x{result.Code:X}"));
            return;
        }

        var refreshTarget = _current;
        if (_current != null && nodes.Contains(_current)) refreshTarget = _current.Parent;

        Notify(Loc.I.Format("DeleteDone", nodes.Count));
        if (refreshTarget != null) await RefreshFolderAsync(refreshTarget);
        else StartScan(DriveCombo.SelectedItem is DriveItem { IsBrowse: false } drive ? drive.Path : "C:\\");
    }

    private void Rename_Click(object sender, RoutedEventArgs e) => RenameTarget();

    private async void RenameTarget()
    {
        var target = Targets.FirstOrDefault();
        if (target?.Parent == null) return;

        string? newName = PromptDialog.RequestInput(this, Loc.I["RenameTitle"], Loc.I["RenamePrompt"], target.Name);
        if (string.IsNullOrEmpty(newName) || string.Equals(newName, target.Name, StringComparison.Ordinal)) return;

        var result = FileOperations.Rename(target.FullPath, newName);
        if (!result.Success)
        {
            PromptDialog.Info(this, Loc.I["Error"], result.Message ?? Loc.I.Format("OperationFailed", result.Code));
            return;
        }

        string newPath = Path.Combine(target.Parent.FullPath, newName);
        string? follow = ReferenceEquals(target, _current) ? newPath : null;

        Notify(Loc.I["RenameDone"]);
        await RefreshFolderAsync(target.Parent, follow);
    }

    private async void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = TargetFolder;
        if (folder == null) return;

        string? name = PromptDialog.RequestInput(this, Loc.I["NewFolderTitle"], Loc.I["NewFolderPrompt"], Loc.I["DefaultNewFolder"]);
        if (name == null) return;

        if (!FileOperations.IsValidName(name, out string? errorKey))
        {
            PromptDialog.Info(this, Loc.I["Error"], Loc.I[errorKey!]);
            return;
        }

        string target = Path.Combine(folder.FullPath, name);
        if (Directory.Exists(target) || File.Exists(target))
        {
            PromptDialog.Info(this, Loc.I["Info"], Loc.I["FolderExists"]);
            return;
        }

        try
        {
            Directory.CreateDirectory(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            PromptDialog.Info(this, Loc.I["Error"], Loc.I.Format("OperationFailed", ex.Message));
            return;
        }

        Notify(Loc.I["FolderCreated"]);
        await RefreshFolderAsync(folder);
    }

    private void Properties_Click(object sender, RoutedEventArgs e)
    {
        var node = Targets.FirstOrDefault();
        if (node != null) FileOperations.ShowProperties(node.FullPath, OwnerHandle);
    }

    private void RefreshFolder_Click(object sender, RoutedEventArgs e) => RefreshCurrent();

    private void RefreshCurrent()
    {
        if (_current != null)
            _ = RefreshFolderAsync(_current);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => FileGrid.SelectAll();

    // ---------------------------------------------------------------- 键盘

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        switch (e.Key)
        {
            case Key.F5:
                RefreshCurrent();
                e.Handled = true;
                break;
            case Key.F2:
                RenameTarget();
                e.Handled = true;
                break;
            case Key.Delete:
                DeleteSelected(shift);
                e.Handled = true;
                break;
            case Key.C when ctrl:
                CopySelected(cut: false);
                e.Handled = true;
                break;
            case Key.X when ctrl:
                CopySelected(cut: true);
                e.Handled = true;
                break;
            case Key.V when ctrl:
                PasteClipboard();
                e.Handled = true;
                break;
            case Key.A when ctrl:
                FileGrid.SelectAll();
                e.Handled = true;
                break;
            case Key.Enter:
                OpenTarget();
                e.Handled = true;
                break;
            case Key.Back:
                GoUp();
                e.Handled = true;
                break;
            case Key.Escape when _busy:
                _scanCts?.Cancel();
                e.Handled = true;
                break;
        }
    }

    // ---------------------------------------------------------------- 状态

    private void Notify(string message)
    {
        _message = message;
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        if (_root == null)
        {
            StatusLeft.Text = Loc.I["Ready"];
            StatusRight.Text = _message;
            return;
        }

        var selected = GetSelectedNodes();
        StatusLeft.Text = selected.Count > 0
            ? Loc.I.Format("StatusSelected", Fmt.Count(selected.Count), Fmt.Bytes(selected.Sum(n => n.Size)))
            : $"{Loc.I.Format("StatusFiles", Fmt.Count(_totalFiles))} · " +
              $"{Loc.I.Format("StatusFolders", Fmt.Count(_totalFolders))} · " +
              Loc.I.Format("StatusTotal", Fmt.Bytes(_root.Size));

        string right = Loc.I.Format("ScanTime", Fmt.Duration(_scanElapsed));
        if (_accessDenied > 0) right += " · " + Loc.I.Format("StatusDenied", Fmt.Count(_accessDenied));
        if (_message.Length > 0) right += " · " + _message;
        StatusRight.Text = right;
    }

    // ---------------------------------------------------------------- 工具方法

    private static FileNode? FindByPath(FileNode root, string fullPath)
    {
        string rootPath = root.FullPath.TrimEnd(Path.DirectorySeparatorChar);
        string path = fullPath.TrimEnd(Path.DirectorySeparatorChar);

        if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase)) return root;
        if (!path.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
        return root.FindDescendant(path[rootPath.Length..]);
    }

    private static bool IsInside(string path, string folder)
    {
        string prefix = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : class
    {
        while (source != null)
        {
            if (source is T match) return match;
            source = source is Visual or Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
}

internal sealed class DriveItem
{
    public string Path { get; init; } = string.Empty;
    public string Display { get; init; } = string.Empty;
    public bool IsBrowse { get; init; }
}

internal static class FileClipboard
{
    private const string DropEffectFormat = "Preferred DropEffect";

    public static void Set(IReadOnlyList<string> paths, bool cut)
    {
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, paths.ToArray());
        if (cut)
        {
            // 2 = DROPEFFECT_MOVE
            data.SetData(DropEffectFormat, new MemoryStream(new byte[] { 2, 0, 0, 0 }));
        }
        Clipboard.SetDataObject(data, true);
    }

    public static (string[] Paths, bool Cut) Get()
    {
        try
        {
            if (!Clipboard.ContainsFileDropList()) return (Array.Empty<string>(), false);
            var paths = Clipboard.GetFileDropList().Cast<string>().ToArray();

            bool cut = false;
            var data = Clipboard.GetDataObject();
            if (data != null && data.GetDataPresent(DropEffectFormat, false) &&
                data.GetData(DropEffectFormat, false) is MemoryStream stream)
            {
                var buffer = new byte[4];
                stream.Position = 0;
                if (stream.Read(buffer, 0, 4) == 4)
                    cut = BitConverter.ToInt32(buffer, 0) == 2;
                stream.Position = 0;
            }

            return (paths, cut);
        }
        catch (Exception)
        {
            return (Array.Empty<string>(), false);
        }
    }
}
