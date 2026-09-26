using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SciToolbox.ViewModels;

namespace SciToolbox.Views;

public partial class NoteEditorControl : UserControl
{
    public NoteEditorControl()
    {
        InitializeComponent();
        // 弹窗每次打开时把焦点交给正文，否则按键仍落在主搜索框里
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Dispatcher.BeginInvoke(new Action(() =>
            {
                Body.Focus();
                Body.CaretIndex = Body.Text.Length;
            }), System.Windows.Threading.DispatcherPriority.Input);
        };
    }

    private void Body_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            (DataContext as NoteEditorVm)?.CancelCommand?.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            (DataContext as NoteEditorVm)?.SaveCommand?.Execute(null);
            e.Handled = true;
        }
    }
}
