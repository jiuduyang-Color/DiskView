using System.IO;
using System.Windows;
using System.Windows.Threading;
using DiskView.Core;

namespace DiskView.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogError(args.ExceptionObject as Exception);

        if (e.Args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            Uninstaller.Run();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        MessageBox.Show(e.Exception.Message, Loc.I["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void LogError(Exception? exception)
    {
        if (exception == null) return;
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskSpaceAnalyzer");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}");
        }
        catch
        {
            // 忽略日志写入失败
        }
    }
}
