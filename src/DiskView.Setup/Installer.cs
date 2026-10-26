using System.IO;
using System.IO.Compression;
using System.Reflection;
using DiskView.Core;
using Microsoft.Win32;

namespace DiskView.Setup;

internal static class Installer
{
    private const string PayloadResource = "DiskView.Setup.payload.app.zip";

    public static string DefaultInstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        ProductInfo.FolderName);

    public static string Install(string targetDirectory, bool desktopShortcut)
    {
        targetDirectory = Path.GetFullPath(targetDirectory);
        Directory.CreateDirectory(targetDirectory);

        ExtractPayload(targetDirectory);

        string exePath = Path.Combine(targetDirectory, ProductInfo.ExeName);
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Application files are missing.", exePath);

        CreateStartMenuShortcut(exePath);

        if (desktopShortcut)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateShortcut(Path.Combine(desktop, Loc.I["AppTitle"] + ".lnk"), exePath);
        }

        WriteUninstallRegistry(targetDirectory, exePath);
        return exePath;
    }

    private static void ExtractPayload(string targetDirectory)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource)
                           ?? throw new InvalidOperationException("Installer payload is missing.");

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        string root = targetDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;

            string destination = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid archive entry.");

            string? parent = Path.GetDirectoryName(destination);
            if (parent != null) Directory.CreateDirectory(parent);

            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private static void CreateStartMenuShortcut(string exePath)
    {
        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        string folder = Path.Combine(programs, Loc.I["SetupHeading"]);
        Directory.CreateDirectory(folder);
        CreateShortcut(Path.Combine(folder, Loc.I["AppTitle"] + ".lnk"), exePath);
    }

    private static void CreateShortcut(string linkPath, string targetPath)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type == null) return;

        dynamic shell = Activator.CreateInstance(type)!;
        dynamic shortcut = shell.CreateShortcut(linkPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.IconLocation = targetPath + ",0";
        shortcut.Description = Loc.I["AppTitle"];
        shortcut.Save();
    }

    private static void WriteUninstallRegistry(string targetDirectory, string exePath)
    {
        long size = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories))
                size += new FileInfo(file).Length;
        }
        catch (Exception)
        {
            // 大小仅用于展示，忽略统计失败
        }

        using var key = Registry.CurrentUser.CreateSubKey(ProductInfo.UninstallKey, true);
        key.SetValue("DisplayName", Loc.I["AppTitle"]);
        key.SetValue("DisplayVersion", ProductInfo.Version);
        key.SetValue("Publisher", ProductInfo.Publisher);
        key.SetValue("DisplayIcon", exePath + ",0");
        key.SetValue("InstallLocation", targetDirectory);
        key.SetValue("UninstallString", $"\"{exePath}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, size / 1024), RegistryValueKind.DWord);
    }
}
