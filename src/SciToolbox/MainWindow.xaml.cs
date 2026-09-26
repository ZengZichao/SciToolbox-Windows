using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SciToolbox.ViewModels;
using SciToolbox.Views;

namespace SciToolbox;

public partial class MainWindow : Window
{
    public MainVm Vm { get; }

    public MainWindow()
    {
        InitializeComponent();
        Vm = new MainVm();
        DataContext = Vm;
        Vm.HookNetworkSignals();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += (_, _) => FocusMainSearch();

        // 切换工具 / 打开首页时自动聚焦主输入框；弹窗打开时改为聚焦弹窗
        Vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainVm.Content):
                FocusMainSearch();
                break;
            case nameof(MainVm.ShowCommandPalette):
                if (Vm.ShowCommandPalette) FocusPaletteInput();
                break;
            case nameof(MainVm.ShowShortcuts):
                if (Vm.ShowShortcuts) FocusOverlay();
                break;
            case nameof(MainVm.ShowRouteSheet):
                if (Vm.ShowRouteSheet) FocusOverlay();
                break;
            case nameof(MainVm.ShowAddToCollection):
                if (Vm.ShowAddToCollection) FocusOverlay();
                break;
            case nameof(MainVm.ShowNoteEditor):
                if (Vm.ShowNoteEditor) FocusOverlay();
                break;
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (ctrl)
        {
            // Ctrl+Enter 在备注框里表示保存，不该被后续处理吃掉
            if (e.Key == Key.Enter && TrySaveNoteShortcut()) { e.Handled = true; return; }

            switch (e.Key)
            {
                case Key.K: Vm.ShowCommandPalette = true; e.Handled = true; break;
                case Key.F: FocusMainSearch(); e.Handled = true; break;
                case Key.OemComma: Vm.OpenToolById("settings"); e.Handled = true; break;
                case Key.OemQuestion: Vm.ShowShortcuts = true; e.Handled = true; break;
                case Key.OemBackslash: Vm.SidebarVisible = !Vm.SidebarVisible; e.Handled = true; break;
                case Key.OemOpenBrackets: Vm.DetailPrevCommand.Execute(null); e.Handled = true; break;
                case Key.OemCloseBrackets: Vm.DetailNextCommand.Execute(null); e.Handled = true; break;
            }
            return;
        }

        if (e.Key == Key.Escape)
        {
            // 关掉最上层弹窗，并阻止事件继续冒泡到底下的输入控件
            if (Vm.ShowCommandPalette) Vm.ShowCommandPalette = false;
            else if (Vm.ShowRouteSheet) Vm.ShowRouteSheet = false;
            else if (Vm.ShowAddToCollection) Vm.ShowAddToCollection = false;
            else if (Vm.ShowNoteEditor) Vm.ShowNoteEditor = false;
            else if (Vm.ShowShortcuts) Vm.ShowShortcuts = false;
            else return;
            e.Handled = true;
            FocusMainSearch();
        }
    }

    private bool TrySaveNoteShortcut()
    {
        if (!Vm.ShowNoteEditor) return false;
        Vm.NoteEditorVmInstance.SaveCommand.Execute(null);
        return true;
    }

    // ===== 侧边栏交互 =====
    private void HeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SidebarItemVm vm)
            vm.ToggleCommand.Execute(null);
    }

    // ===== 覆盖层遮罩点击关闭 =====
    private void ScrimClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        switch (fe.Tag as string)
        {
            case "palette": Vm.ShowCommandPalette = false; break;
            case "route": Vm.ShowRouteSheet = false; break;
            case "addcollection": Vm.ShowAddToCollection = false; break;
            case "note": Vm.ShowNoteEditor = false; break;
            case "shortcuts": Vm.ShowShortcuts = false; break;
        }
    }

    private void ToastUndo_Click(object sender, RoutedEventArgs e) => Core.Toast.Shared.PerformUndo();

    // ===== 焦点辅助 =====
    private void FocusMainSearch()
    {
        // 弹窗打开时不能把焦点抢回主搜索框，否则弹窗里根本打不了字
        if (Vm.AnyOverlayVisible) return;
        Postpone(() =>
        {
            var box = FindFirst<TextBox>(this, t => t.Tag as string == "MainSearch" && t.IsVisible);
            if (box != null) { box.Focus(); box.SelectAll(); }
        });
    }

    private void FocusPaletteInput() => Postpone(() => FindFirst<CommandPaletteControl>(this, _ => true)?.FocusInput());

    /// <summary>弹窗出现后把焦点移进去第一个可聚焦控件，键盘用户才能直接操作。</summary>
    private void FocusOverlay() => Postpone(() =>
    {
        var target = FindFirst<Control>(this, c => c.IsVisible && c.Focusable && c.IsTabStop
                                                  && c is not ScrollViewer && c is not ItemsControl);
        if (target is TextBox tb) { tb.Focus(); tb.SelectAll(); }
        else target?.Focus();
    });

    private void Postpone(Action action) =>
        Dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Input);

    private static T? FindFirst<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T t && predicate(t)) return t;
            var deeper = FindFirst(child, predicate);
            if (deeper != null) return deeper;
        }
        return null;
    }
}
