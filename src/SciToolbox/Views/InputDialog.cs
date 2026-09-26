using System.Windows;
using System.Windows.Controls;
using SciToolbox.Core;

namespace SciToolbox.Views;

/// <summary>轻量输入对话框（纯代码构建，用于新建 / 重命名集合）。</summary>
public static class InputDialog
{
    public static string? Prompt(Window? owner, string title, string message, string defaultText)
    {
        string? result = null;

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeManager.Brush("Brush.Ink2")
        });
        var box = new TextBox
        {
            Text = defaultText,
            Style = (Style)Application.Current.Resources["SearchBox"],
            Margin = new Thickness(0, 0, 0, 16)
        };
        panel.Children.Add(box);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = L10n.T(L10n.Key.Save), Style = (Style)Application.Current.Resources["PrimaryButton"], Margin = new Thickness(0, 0, 8, 0), MinWidth = 80 };
        var cancel = new Button { Content = L10n.T(L10n.Key.Cancel), Style = (Style)Application.Current.Resources["SecondaryButton"], MinWidth = 80 };

        var dlg = new Window
        {
            Title = title,
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Background = ThemeManager.Brush("Brush.Window"),
            Owner = owner,
            MinWidth = 360
        };

        ok.Click += (_, _) => { result = box.Text; dlg.DialogResult = true; };
        cancel.Click += (_, _) => { dlg.DialogResult = false; };
        box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { result = box.Text; dlg.DialogResult = true; } };

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        box.Focus();
        box.SelectAll();
        dlg.ShowDialog();
        return result;
    }
}
