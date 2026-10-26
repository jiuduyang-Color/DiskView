using System.Diagnostics;
using DiskView.Core;

if (args.Length > 0 && args[0] == "--ops")
{
    int exitCode = 1;
    var thread = new Thread(() => exitCode = RunOperations());
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    return exitCode;
}

string path = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
Console.WriteLine($"Scanning: {path}");

var scanner = new DiskScanner();
var clock = Stopwatch.StartNew();
int reportCount = 0;

var progress = new Progress<ScanStats>(stats =>
{
    reportCount++;
    if (reportCount % 5 == 1)
        Console.WriteLine($"  [{clock.Elapsed.TotalSeconds,5:0.0}s] {stats.Files,10:N0} files  {stats.Folders,8:N0} dirs  {Fmt.Bytes(stats.Bytes),10}  {stats.CurrentDirectory}");
});

var result = scanner.Scan(path, progress);
clock.Stop();

Console.WriteLine($"Scan complete in {clock.Elapsed.TotalSeconds:0.00}s");
Console.WriteLine($"  Bytes   : {result.TotalBytes:N0} ({Fmt.Bytes(result.TotalBytes)})");
Console.WriteLine($"  Files   : {result.TotalFiles:N0}");
Console.WriteLine($"  Folders : {result.TotalFolders:N0}");
Console.WriteLine($"  Denied  : {result.AccessDeniedCount:N0}");

var reference = Walk(path);
Console.WriteLine("Reference (DirectoryInfo recursion)");
Console.WriteLine($"  Bytes   : {reference.Bytes:N0}");
Console.WriteLine($"  Files   : {reference.Files:N0}");
Console.WriteLine($"  Folders : {reference.Folders:N0}");

bool bytesMatch = Math.Abs(result.TotalBytes - reference.Bytes) < Math.Max(1024L, reference.Bytes / 10000);
Console.WriteLine(bytesMatch ? "RESULT: OK" : "RESULT: MISMATCH");
return bytesMatch ? 0 : 1;

static int RunOperations()
{
    string root = Path.Combine(Path.GetTempPath(), "DiskViewOpsTest");
    try
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "dst"));

        string source = Path.Combine(root, "src", "a.txt");
        File.WriteAllText(source, "hello");

        var copy = FileOperations.Copy(new[] { source }, Path.Combine(root, "dst"), IntPtr.Zero);
        bool copyOk = copy.Success && File.Exists(Path.Combine(root, "dst", "a.txt"));

        string moved = Path.Combine(root, "moved");
        Directory.CreateDirectory(moved);
        var move = FileOperations.Move(new[] { Path.Combine(root, "dst", "a.txt") }, moved, IntPtr.Zero);
        bool moveOk = move.Success && File.Exists(Path.Combine(moved, "a.txt"));

        var rename = FileOperations.Rename(Path.Combine(moved, "a.txt"), "b.txt");
        bool renameOk = rename.Success && File.Exists(Path.Combine(moved, "b.txt"));

        var delete = FileOperations.Delete(new[] { Path.Combine(moved, "b.txt") }, permanent: true, IntPtr.Zero);
        bool deleteOk = delete.Success && !File.Exists(Path.Combine(moved, "b.txt"));

        string folder = Path.Combine(root, "dir1");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "x.txt"), "x");
        var deleteFolder = FileOperations.Delete(new[] { folder }, permanent: true, IntPtr.Zero);
        bool deleteFolderOk = deleteFolder.Success && !Directory.Exists(folder);

        Console.WriteLine($"copy={copyOk}({copy.Code}) move={moveOk}({move.Code}) rename={renameOk}({rename.Code}) " +
                          $"delete={deleteOk}({delete.Code}) deleteFolder={deleteFolderOk}({deleteFolder.Code})");

        return copyOk && moveOk && renameOk && deleteOk && deleteFolderOk ? 0 : 1;
    }
    finally
    {
        try { Directory.Delete(root, true); } catch { }
    }
}

static (long Bytes, long Files, long Folders) Walk(string root)
{
    long bytes = 0, files = 0, folders = 0;
    var stack = new Stack<DirectoryInfo>();
    stack.Push(new DirectoryInfo(root));

    while (stack.Count > 0)
    {
        var dir = stack.Pop();
        folders++;
        try
        {
            foreach (var entry in dir.EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo sub)
                {
                    if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    stack.Push(sub);
                }
                else if (entry is FileInfo file)
                {
                    bytes += file.Length;
                    files++;
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    return (bytes, files, folders);
}
