using System.Runtime.InteropServices;

namespace DiskView.Core;

internal static class NativeMethods
{
    public const int FindExInfoBasic = 1;
    public const int FindExSearchNameMatch = 0;
    public const int FindFirstExLargeFetch = 2;

    public const uint FileAttributeDirectory = 0x10;
    public const uint FileAttributeReparsePoint = 0x400;

    public static readonly IntPtr InvalidHandleValue = new(-1);

    public const int ErrorFileNotFound = 2;
    public const int ErrorPathNotFound = 3;
    public const int ErrorAccessDenied = 5;
    public const int ErrorNoMoreFiles = 18;

    /// <summary>WIN32_FIND_DATAW 的字节长度，用于非托管缓冲区。</summary>
    public const int FindDataSize = 592;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindFirstFileExW(
        string lpFileName,
        int fInfoLevelId,
        IntPtr lpFindFileData,
        int fSearchOp,
        IntPtr lpSearchFilter,
        int dwAdditionalFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool FindNextFileW(IntPtr hFindFile, IntPtr lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FindClose(IntPtr hFindFile);

    // Shell 文件操作（复制 / 移动 / 删除，支持回收站）
    public const uint FO_DELETE = 0x0003;
    public const uint FO_COPY = 0x0002;
    public const uint FO_MOVE = 0x0001;

    public const ushort FOF_RENAMEONCOLLISION = 0x0008;
    public const ushort FOF_NOCONFIRMATION = 0x0010;
    public const ushort FOF_ALLOWUNDO = 0x0040;
    public const ushort FOF_NOCONFIRMMKDIR = 0x0200;

    public const int ErrorCancelled = 1223;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int SHFileOperationW(ref ShFileOpStruct lpFileOp);

    public const int SEE_MASK_INVOKEIDLIST = 0x0000000C;
    public const int SEE_MASK_NOCLOSEPROCESS = 0x00000040;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ShellExecuteInfo
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ShellExecuteExW(ref ShellExecuteInfo lpExecInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr ShellExecuteW(IntPtr hwnd, string? lpOperation, string lpFile, string? lpParameters, string? lpDirectory, int nShowCmd);
}
