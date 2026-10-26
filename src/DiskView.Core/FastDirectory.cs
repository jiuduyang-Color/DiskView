using System.IO;
using System.Runtime.InteropServices;

namespace DiskView.Core;

internal readonly struct DirectoryEntry
{
    public DirectoryEntry(string name, bool isDirectory, bool isReparsePoint, long size, long modifiedTicks)
    {
        Name = name;
        IsDirectory = isDirectory;
        IsReparsePoint = isReparsePoint;
        Size = size;
        ModifiedTicks = modifiedTicks;
    }

    public string Name { get; }
    public bool IsDirectory { get; }
    public bool IsReparsePoint { get; }
    public long Size { get; }
    public long ModifiedTicks { get; }
}

internal static class FastDirectory
{
    public static List<DirectoryEntry> Enumerate(string directoryPath, CancellationToken token)
    {
        var entries = new List<DirectoryEntry>(64);
        string searchPattern = ToExtendedPath(directoryPath.TrimEnd(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar + "*";

        IntPtr buffer = Marshal.AllocHGlobal(NativeMethods.FindDataSize);
        try
        {
            IntPtr handle = NativeMethods.FindFirstFileExW(
                searchPattern,
                NativeMethods.FindExInfoBasic,
                buffer,
                NativeMethods.FindExSearchNameMatch,
                IntPtr.Zero,
                NativeMethods.FindFirstExLargeFetch);

            if (handle == NativeMethods.InvalidHandleValue)
            {
                int error = Marshal.GetLastWin32Error();
                if (error is NativeMethods.ErrorFileNotFound or NativeMethods.ErrorPathNotFound or NativeMethods.ErrorNoMoreFiles)
                    return entries;
                if (error == NativeMethods.ErrorAccessDenied)
                    throw new UnauthorizedAccessException($"Access denied: {directoryPath}");
                throw new IOException($"Cannot enumerate '{directoryPath}' (Win32 error {error}).");
            }

            try
            {
                do
                {
                    token.ThrowIfCancellationRequested();

                    // WIN32_FIND_DATAW 布局：属性 0，写入时间 20，大小 28/32，文件名 44
                    uint attributes = (uint)Marshal.ReadInt32(buffer, 0);
                    long lastWrite = Marshal.ReadInt64(buffer, 20);
                    uint sizeHigh = (uint)Marshal.ReadInt32(buffer, 28);
                    uint sizeLow = (uint)Marshal.ReadInt32(buffer, 32);
                    string name = Marshal.PtrToStringUni(buffer + 44) ?? string.Empty;

                    if (name.Length == 0 || name == "." || name == "..") continue;

                    bool isDirectory = (attributes & NativeMethods.FileAttributeDirectory) != 0;
                    bool isReparsePoint = (attributes & NativeMethods.FileAttributeReparsePoint) != 0;
                    long size = isDirectory ? 0 : ((long)sizeHigh << 32) | sizeLow;

                    entries.Add(new DirectoryEntry(name, isDirectory, isReparsePoint, size, lastWrite));
                }
                while (NativeMethods.FindNextFileW(handle, buffer));
            }
            finally
            {
                NativeMethods.FindClose(handle);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return entries;
    }

    /// <summary>为超过 260 字符的路径添加扩展前缀。</summary>
    private static string ToExtendedPath(string path)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return @"\\?\UNC\" + path[2..];
        return @"\\?\" + path;
    }

    public static string CombineDisplayPath(string directoryPath, string name)
    {
        string trimmed = directoryPath.TrimEnd(Path.DirectorySeparatorChar);
        return trimmed + Path.DirectorySeparatorChar + name;
    }
}
