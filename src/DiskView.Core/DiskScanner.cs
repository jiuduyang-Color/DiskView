using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace DiskView.Core;

public sealed class ScanStats
{
    public string CurrentDirectory { get; init; } = string.Empty;
    public long Bytes { get; init; }
    public long Files { get; init; }
    public long Folders { get; init; }
    public TimeSpan Elapsed { get; init; }
}

public sealed class ScanResult
{
    public required FileNode Root { get; init; }
    public long TotalBytes { get; init; }
    public long TotalFiles { get; init; }
    public long TotalFolders { get; init; }
    public int AccessDeniedCount { get; init; }
    public TimeSpan Elapsed { get; init; }
}

/// <summary>多线程目录扫描器，使用 Win32 FindFirstFileEx 高速枚举。</summary>
public sealed class DiskScanner
{
    public ScanResult Scan(string rootPath, IProgress<ScanStats>? progress = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Path is required.", nameof(rootPath));

        string fullPath = NormalizePath(rootPath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(fullPath);

        var stopwatch = Stopwatch.StartNew();
        var root = new FileNode(GetDisplayName(fullPath), fullPath, true, 0, 0);

        long totalBytes = 0;
        long totalFiles = 0;
        long totalFolders = 1;
        int accessDenied = 0;

        var allDirectories = new List<FileNode> { root };
        var allDirectoriesLock = new object();

        int remaining = 1;
        var queue = new BlockingCollection<FileNode>();
        queue.Add(root);
        int threadCount = Math.Clamp(Environment.ProcessorCount, 2, 16);

        object reportLock = new object();
        long lastReportTicks = 0;

        void Report(string currentDirectory, bool force)
        {
            if (progress == null) return;
            lock (reportLock)
            {
                long now = stopwatch.ElapsedTicks;
                if (!force && now - lastReportTicks < Stopwatch.Frequency / 8) return;
                lastReportTicks = now;
            }

            progress.Report(new ScanStats
            {
                CurrentDirectory = currentDirectory,
                Bytes = Interlocked.Read(ref totalBytes),
                Files = Interlocked.Read(ref totalFiles),
                Folders = Interlocked.Read(ref totalFolders),
                Elapsed = stopwatch.Elapsed
            });
        }

        void ProcessDirectory(FileNode directory)
        {
            try
            {
                var entries = FastDirectory.Enumerate(directory.FullPath, token);
                var subDirectories = new List<FileNode>(entries.Count);
                var files = new List<FileNode>();
                long ownBytes = 0;

                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();

                    string childPath = FastDirectory.CombineDisplayPath(directory.FullPath, entry.Name);

                    if (entry.IsDirectory)
                    {
                        if (entry.IsReparsePoint) continue;

                        var child = new FileNode(entry.Name, childPath, true, 0, entry.ModifiedTicks)
                        {
                            Parent = directory
                        };
                        subDirectories.Add(child);
                        Interlocked.Increment(ref totalFolders);
                        lock (allDirectoriesLock) allDirectories.Add(child);

                        Interlocked.Increment(ref remaining);
                        queue.Add(child);
                    }
                    else
                    {
                        var file = new FileNode(entry.Name, childPath, false, entry.Size, entry.ModifiedTicks)
                        {
                            Parent = directory
                        };
                        files.Add(file);
                        ownBytes += entry.Size;
                        Interlocked.Increment(ref totalFiles);
                        Interlocked.Add(ref totalBytes, entry.Size);
                    }
                }

                directory.SetChildren(subDirectories, files);
                directory.AddSizeSilent(ownBytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                directory.AccessDenied = true;
                directory.ErrorMessage = ex.Message;
                Interlocked.Increment(ref accessDenied);
            }
            catch (IOException ex)
            {
                directory.ErrorMessage = ex.Message;
            }
            finally
            {
                Report(directory.FullPath, false);
                if (Interlocked.Decrement(ref remaining) == 0)
                {
                    try { queue.CompleteAdding(); } catch (ObjectDisposedException) { }
                }
            }
        }

        var workers = new Task[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            workers[i] = Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        if (queue.TryTake(out var directory, 100, token))
                        {
                            ProcessDirectory(directory);
                            continue;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }

                    if (queue.IsCompleted) return;
                }
            }, CancellationToken.None);
        }

        try
        {
            Task.WaitAll(workers);
        }
        catch (AggregateException)
        {
            // 工作线程已自行吞掉可恢复异常
        }

        token.ThrowIfCancellationRequested();

        // 自底向上汇总目录大小
        for (int i = allDirectories.Count - 1; i > 0; i--)
        {
            var directory = allDirectories[i];
            directory.Parent?.AddSizeSilent(directory.Size);
        }

        foreach (var directory in allDirectories)
            directory.SortChildren();

        stopwatch.Stop();
        Report(fullPath, true);

        return new ScanResult
        {
            Root = root,
            TotalBytes = root.Size,
            TotalFiles = totalFiles,
            TotalFolders = totalFolders,
            AccessDeniedCount = accessDenied,
            Elapsed = stopwatch.Elapsed
        };
    }

    public static string NormalizePath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.Length > 3)
            full = full.TrimEnd(Path.DirectorySeparatorChar);
        return full;
    }

    private static string GetDisplayName(string fullPath)
    {
        string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar);
        if (trimmed.Length <= 1) return fullPath;
        if (trimmed.Length == 2 && trimmed[1] == ':') return trimmed + Path.DirectorySeparatorChar;
        string name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? fullPath : name;
    }
}
