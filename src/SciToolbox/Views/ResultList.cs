using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SciToolbox.Views;

/// <summary>列表行可被 Enter 键激活。</summary>
public interface IActivatableRow
{
    ICommand? ActivateCommand { get; }
}

/// <summary>支持 Ctrl+点击多选的列表行。</summary>
public interface IMultiSelectRow
{
    bool IsSelected { get; set; }
}

/// <summary>支持上下键移动高亮、Enter 打开、Ctrl+点击多选的结果列表。</summary>
public class ResultList : ListBox
{
    static ResultList()
    {
        // 隐式样式没定义 DefaultStyleKey 会退回 ListBox 主题样式，这里显式对齐
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ResultList),
            new FrameworkPropertyMetadata(typeof(ListBox)));
    }

    public ResultList()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ListBoxItem lbi)
        {
            lbi.PreviewKeyDown -= OnItemKeyDown;
            lbi.PreviewKeyDown += OnItemKeyDown;
        }
    }

    private void OnItemKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Move(1); e.Handled = true; break;
            case Key.Up:
                Move(-1); e.Handled = true; break;
            case Key.Enter:
                Activate(SelectedItem); e.Handled = true; break;
            case Key.Home:
                SelectAt(0); e.Handled = true; break;
            case Key.End:
                SelectAt(Math.Max(0, Items.Count - 1)); e.Handled = true; break;
        }
    }

    private void Move(int delta)
    {
        if (Items.Count == 0) return;
        int i = SelectedIndex < 0 ? (delta > 0 ? -1 : 0) : SelectedIndex;
        SelectAt(Math.Clamp(i + delta, 0, Items.Count - 1));
    }

    private void SelectAt(int index)
    {
        if (index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
        if (ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
        {
            item.Focus();
            item.BringIntoView();
        }
        else
        {
            ScrollIntoView(Items[index]);
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (!ctrl) return;
        if (e.OriginalSource is DependencyObject src && FindContainer(src) is { } lbi
            && lbi.DataContext is IMultiSelectRow row)
        {
            row.IsSelected = !row.IsSelected;
            lbi.IsSelected = true;
            e.Handled = true;
        }
    }

    private ListBoxItem? FindContainer(DependencyObject src)
    {
        while (src != null && src is not ListBoxItem)
            src = VisualTreeHelper.GetParent(src);
        return src as ListBoxItem;
    }

    private static void Activate(object item)
    {
        if (item is IActivatableRow row) row.ActivateCommand?.Execute(null);
    }
}
