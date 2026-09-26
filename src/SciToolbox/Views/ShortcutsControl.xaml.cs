using System.Windows.Controls;
using SciToolbox.Core;

namespace SciToolbox.Views;

public partial class ShortcutsControl : UserControl
{
    private sealed record ShortcutRow(string Keys, string Action);

    public ShortcutsControl()
    {
        InitializeComponent();
        BuildRows();
        // 语言切换后重建这张表
        AppLanguage.Shared.Changed += BuildRows;
        Unloaded += (_, _) => AppLanguage.Shared.Changed -= BuildRows;
    }

    private void BuildRows()
    {
        Rows.Dispatcher.Invoke(() =>
        {
            Rows.ItemsSource = new List<ShortcutRow>
            {
                new(L10n.T(L10n.Key.ShortcutCtrlK), L10n.T(L10n.Key.ShortcutCtrlKDesc)),
                new(L10n.T(L10n.Key.ShortcutCtrlF), L10n.T(L10n.Key.ShortcutCtrlFDesc)),
                new(L10n.T(L10n.Key.ShortcutCtrlBracket), L10n.T(L10n.Key.ShortcutCtrlBracketDesc)),
                new(L10n.T(L10n.Key.ShortcutCtrlBackslash), L10n.T(L10n.Key.ShortcutCtrlBackslashDesc)),
                new(L10n.T(L10n.Key.ShortcutCtrlSlash), L10n.T(L10n.Key.ShortcutCtrlSlashDesc)),
                new(L10n.T(L10n.Key.ShortcutCtrlComma), L10n.T(L10n.Key.ShortcutCtrlCommaDesc)),
                new("Ctrl + Enter", DT.T("保存备注", "Save the note")),
                new(L10n.T(L10n.Key.ShortcutArrows), L10n.T(L10n.Key.ShortcutArrowsDesc)),
                new(L10n.T(L10n.Key.ShortcutCtrlClick), L10n.T(L10n.Key.ShortcutCtrlClickDesc)),
                new(L10n.T(L10n.Key.ShortcutEsc), L10n.T(L10n.Key.ShortcutEscDesc)),
            };
        });
    }
}
