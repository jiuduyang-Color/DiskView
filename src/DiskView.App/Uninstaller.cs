using System.Diagnostics;
using System.IO;
using System.Windows;
using DiskView.Core;
using Microsoft.Win32;

namespace DiskView.App;

internal static class Uninstaller
{
    public static void Run()
    {
        var confirm = MessageBox.Show(Loc.I["UninstallConfirm"], Loc.I["Uninstall"],
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            Shutdown();
            return;
        }

        DeleteShortcuts();

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ProductInfo.UninstallKey, throwOnMissingSubKey: false);
        }
        catch (Exception)
        {
            // 注册表项可能已被其他清理工具移除
        }

        string? exePath = Environment.ProcessPath;
        string? folder = exePath == null ? null : Path.GetDirectoryName(exePath);

        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            ScheduleFolderRemoval(folder);
        }

        Shutdown();
    }

    private static void DeleteShortcuts()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

        foreach (string name in ShortcutNames())
        {
            TryDeleteFile(Path.Combine(desktop, name + ".lnk"));
            TryDeleteFile(Path.Combine(programs, name + ".lnk"));

            string folder = Path.Combine(programs, name);
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
            catch (Exception)
            {
                // 忽略快捷方式删除失败
            }
        }
    }

    private static IEnumerable<string> ShortcutNames() => new[] { "磁盘空间分析器", "Disk Space Analyzer" };

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
            // 忽略
        }
    }

    private static void ScheduleFolderRemoval(string folder)
    {
        try
        {
            // 程序自身位于待删除目录中，需在其退出后再删除
            var start = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{folder}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(start);
        }
        catch (Exception)
        {
            // 删除失败时保留文件，避免误报
        }
    }

    private static void Shutdown() => Application.Current.Shutdown();
}
