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

        // 免责声明未确认前不创建主窗口；显式控制关闭时机，避免关闭对话框时程序被提前退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!AgreementStore.IsAccepted())
        {
            var disclaimer = new DisclaimerWindow();
            if (disclaimer.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            AgreementStore.Accept();
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
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
