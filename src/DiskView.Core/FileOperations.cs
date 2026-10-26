using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace DiskView.Core;

public sealed class FileOpResult
{
    public bool Success { get; init; }
    public bool Cancelled { get; init; }
    public int Code { get; init; }
    public string? Message { get; init; }

    public static FileOpResult Ok() => new() { Success = true };
    public static FileOpResult Cancel() => new() { Cancelled = true, Code = NativeMethods.ErrorCancelled };
    public static FileOpResult Fail(int code, string? message = null) => new() { Code = code, Message = message };
}

/// <summary>基于 Windows Shell 的文件操作（支持回收站、冲突自动重命名）。</summary>
public static class FileOperations
{
    public static FileOpResult Copy(IReadOnlyList<string> sources, string destinationFolder, IntPtr owner)
        => Execute(NativeMethods.FO_COPY, sources, destinationFolder, owner,
            NativeMethods.FOF_ALLOWUNDO | NativeMethods.FOF_NOCONFIRMATION |
            NativeMethods.FOF_NOCONFIRMMKDIR | NativeMethods.FOF_RENAMEONCOLLISION);

    public static FileOpResult Move(IReadOnlyList<string> sources, string destinationFolder, IntPtr owner)
        => Execute(NativeMethods.FO_MOVE, sources, destinationFolder, owner,
            NativeMethods.FOF_ALLOWUNDO | NativeMethods.FOF_NOCONFIRMATION |
            NativeMethods.FOF_NOCONFIRMMKDIR | NativeMethods.FOF_RENAMEONCOLLISION);

    public static FileOpResult Delete(IReadOnlyList<string> paths, bool permanent, IntPtr owner)
    {
        ushort flags = NativeMethods.FOF_NOCONFIRMATION;
        if (!permanent) flags |= NativeMethods.FOF_ALLOWUNDO;
        return Execute(NativeMethods.FO_DELETE, paths, null, owner, flags);
    }

    public static FileOpResult Rename(string path, string newName)
    {
        newName = newName.Trim();
        if (!IsValidName(newName, out string? errorKey))
            return FileOpResult.Fail(-1, Loc.I[errorKey!]);

        string? parent = Path.GetDirectoryName(path);
        if (parent == null) return FileOpResult.Fail(-1, Loc.I["OperationFailed"]);

        string target = Path.Combine(parent, newName);
        if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
            return FileOpResult.Ok();

        if (File.Exists(target) || Directory.Exists(target))
            return FileOpResult.Fail(-1, Loc.I["NameExists"]);

        try
        {
            if (Directory.Exists(path))
                Directory.Move(path, target);
            else
                File.Move(path, target);
            return FileOpResult.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return FileOpResult.Fail(-1, ex.Message);
        }
    }

    public static string CreateFolder(string parentFolder, string name)
    {
        string target = Path.Combine(parentFolder, name);
        Directory.CreateDirectory(target);
        return target;
    }

    public static bool IsValidName(string name, out string? errorKey)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            errorKey = "InvalidName";
            return false;
        }

        errorKey = null;
        return true;
    }

    public static bool Open(string path)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    public static bool RevealInExplorer(string path)
    {
        try
        {
            string argument = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
            using var process = System.Diagnostics.Process.Start("explorer.exe", argument);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public static bool ShowProperties(string path, IntPtr owner)
    {
        var info = new NativeMethods.ShellExecuteInfo
        {
            cbSize = Marshal.SizeOf<NativeMethods.ShellExecuteInfo>(),
            fMask = NativeMethods.SEE_MASK_INVOKEIDLIST,
            hwnd = owner,
            lpVerb = "properties",
            lpFile = path,
            nShow = 1
        };

        return NativeMethods.ShellExecuteExW(ref info);
    }

    private static FileOpResult Execute(uint operation, IReadOnlyList<string> sources, string? destination, IntPtr owner, ushort flags)
    {
        var valid = sources.Where(static s => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (valid.Length == 0) return FileOpResult.Ok();

        var op = new NativeMethods.ShFileOpStruct
        {
            hwnd = owner,
            wFunc = operation,
            pFrom = string.Join('\0', valid) + "\0\0",
            pTo = destination is null ? null : destination + "\0",
            fFlags = flags
        };

        int result = NativeMethods.SHFileOperationW(ref op);
        if (result == 0)
            return op.fAnyOperationsAborted ? FileOpResult.Cancel() : FileOpResult.Ok();

        if (result == NativeMethods.ErrorCancelled)
            return FileOpResult.Cancel();

        return FileOpResult.Fail(result, Loc.I.Format("OperationFailed", $"0x{result:X}"));
    }
}
