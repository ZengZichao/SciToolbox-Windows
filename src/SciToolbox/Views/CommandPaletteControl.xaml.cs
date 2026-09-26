using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SciToolbox.ViewModels;

namespace SciToolbox.Views;

public partial class CommandPaletteControl : UserControl
{
    public CommandPaletteControl()
    {
        InitializeComponent();
    }

    public void FocusInput()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            InputBox.Focus();
            InputBox.CaretIndex = InputBox.Text.Length;
        }));
    }

    private MainVm? Vm => Window.GetWindow(this)?.DataContext as MainVm;

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CommandPaletteVm vm) return;
        switch (e.Key)
        {
            case Key.Enter:
                vm.Accept();
                e.Handled = true;
                break;
            case Key.Up:
                vm.MoveUp();
                e.Handled = true;
                break;
            case Key.Down:
                vm.MoveDown();
                e.Handled = true;
                break;
            case Key.Escape:
                Vm?.DismissPalette();
                e.Handled = true;
                break;
        }
    }

    // 单击即选定并关闭
    private void List_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        if (FindAncestor<ListBoxItem>(src)?.DataContext == null) return;
        if (DataContext is CommandPaletteVm vm) vm.Accept();
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null && current is not T)
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        return current as T;
    }
}
