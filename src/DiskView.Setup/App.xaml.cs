using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DiskView.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
