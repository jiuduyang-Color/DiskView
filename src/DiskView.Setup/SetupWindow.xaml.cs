using System.Diagnostics;
using System.IO;
using System.Windows;
using DiskView.Core;

namespace DiskView.Setup;

public partial class SetupWindow : Window
{
    private string? _installedExe;

    public SetupWindow()
    {
        InitializeComponent();
        PathBox.Text = Installer.DefaultInstallDirectory;
        SizeNote.Text = string.Empty;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.I["InstallPath"],
            InitialDirectory = Directory.Exists(PathBox.Text) ? PathBox.Text : Installer.DefaultInstallDirectory
        };

        if (dialog.ShowDialog(this) == true) PathBox.Text = dialog.FolderName;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        string directory;
        try
        {
            directory = Path.GetFullPath(PathBox.Text.Trim());
            if (directory.Length == 0 || !Path.IsPathRooted(directory)) throw new ArgumentException();
        }
        catch (Exception)
        {
            MessageBox.Show(this, Loc.I["PathInvalid"], Loc.I["Error"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        InstallPanel.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = Loc.I["Installing"];

        try
        {
            bool desktopShortcut = DesktopCheck.IsChecked == true;
            _installedExe = await Task.Run(() => Installer.Install(directory, desktopShortcut));
        }
        catch (Exception ex)
        {
            Progress.Visibility = Visibility.Collapsed;
            InstallPanel.IsEnabled = true;
            StatusText.Text = string.Empty;
            MessageBox.Show(this, Loc.I.Format("InstallFailed", ex.Message), Loc.I["Error"],
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Progress.Visibility = Visibility.Collapsed;
        InstallPanel.Visibility = Visibility.Collapsed;
        DonePanel.Visibility = Visibility.Visible;
        StatusText.Text = Loc.I.Format("InstallDone");
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_installedExe == null || !File.Exists(_installedExe)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _installedExe, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Loc.I["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
