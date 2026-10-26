using System.Windows;
using System.Windows.Input;
using DiskView.Core;

namespace DiskView.App;

public partial class PromptDialog : Window
{
    private PromptDialog(string title, string message, string okText, string cancelText, bool isInput, string initialValue)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        CancelButton.Content = cancelText;
        CancelButton.Visibility = string.IsNullOrEmpty(cancelText) ? Visibility.Collapsed : Visibility.Visible;

        if (isInput)
        {
            InputBox.Visibility = Visibility.Visible;
            InputBox.Text = initialValue;
            Loaded += (_, _) =>
            {
                InputBox.Focus();
                InputBox.SelectAll();
            };
        }
    }

    public string Value => InputBox.Text;

    public static bool Confirm(Window owner, string title, string message, string okText)
    {
        var dialog = new PromptDialog(title, message, okText, Loc.I["BtnCancel"], false, string.Empty) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    public static string? RequestInput(Window owner, string title, string message, string initialValue)
    {
        var dialog = new PromptDialog(title, message, Loc.I["BtnOk"], Loc.I["BtnCancel"], true, initialValue) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Value.Trim() : null;
    }

    public static void Info(Window owner, string title, string message)
    {
        var dialog = new PromptDialog(title, message, Loc.I["BtnOk"], string.Empty, false, string.Empty) { Owner = owner };
        dialog.ShowDialog();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
