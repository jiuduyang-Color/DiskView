using System.Windows;
using DiskView.Core;

namespace DiskView.App;

/// <summary>首次启动的免责声明确认窗口：必须手动勾选后才能继续，不同意则直接退出程序。</summary>
public partial class DisclaimerWindow : Window
{
    public DisclaimerWindow()
    {
        InitializeComponent();
        DisclaimerBody.Text = Loc.I["DisclaimerText"];
    }

    private void Agree_Click(object sender, RoutedEventArgs e)
    {
        if (AgreeCheck.IsChecked != true) return;
        DialogResult = true;
    }

    private void Decline_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
