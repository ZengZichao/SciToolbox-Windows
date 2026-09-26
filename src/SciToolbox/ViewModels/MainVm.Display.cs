using System.Windows.Input;
using SciToolbox.Core;

namespace SciToolbox.ViewModels;

public sealed partial class MainVm
{
    /// <summary>当前详情视图模型（供右栏绑定）。</summary>
    public DetailVm? CurrentDetail => _currentDetail;

    public bool HasDetailState => DetailLoading || HasDetailError || _currentDetail != null;

    // ===== 智能路由候选展示 =====
    public List<RouteChoiceVm> RouteOptions { get; private set; } = new();

    private void BuildRouteOptions()
    {
        RouteOptions = RouteMatches.Select(m => new RouteChoiceVm(m, this)).ToList();
        Raise(nameof(RouteOptions));
    }

    // 在 Actions/RouteAccession 的 choose 分支之外，提供一个可被弹窗列表调用的入口
    public void RouteChooseAt(RouteChoiceVm choice) => ChooseRoute(choice.Match);

    // 供命令面板输入变化 / 详情更新时刷新绑定
    public void NotifyDetailChanged()
    {
        Raise(nameof(CurrentDetail));
        Raise(nameof(HasDetailState));
    }

    // ===== 文本 / Toast 展示属性 =====
    public string OfflineText => L10n.T(L10n.Key.OfflineBanner);
    public string CompareLabel => L10n.T(L10n.Key.Compare);
    public string ClearCompareLabel => L10n.T(L10n.Key.ClearCompare);
    public string UndoLabel => L10n.T(L10n.Key.Undo);

    public string CloseTip => L10n.T(L10n.Key.Close);
    public string RouteTitle => L10n.T(L10n.Key.SelectDatabase);
    public string RouteHint => L10n.T(L10n.Key.SmartFieldHint);
    public string ShortcutsTitle => L10n.T(L10n.Key.KeyboardShortcuts);
    public string ShortcutsFootnote => SciToolbox.Core.DT.T("弹窗打开时按 Esc 关闭", "Press Esc to dismiss a dialog");
    public string CompareHint => SciToolbox.Core.DT.T("并排比较已加入对比的条目", "Compare the entries you added side by side");
    public string CompareCount => ComparisonStore.Shared.Items.Count.ToString();

    public string PaletteTip => L10n.T(L10n.Key.CmdPalette);
    public string ShortcutsTip => L10n.T(L10n.Key.CmdShortcuts);
    public string SidebarTip => L10n.T(L10n.Key.CmdToggleSidebar);
    public string BackTip => L10n.T(L10n.Key.BackToPrevTool);
    public string PrevDetailTip => L10n.T(L10n.Key.PrevDetail);
    public string NextDetailTip => L10n.T(L10n.Key.NextDetail);
    public string RetryLabel => L10n.T(L10n.Key.Retry);
    public string EmptyGuidanceText => L10n.T(L10n.Key.EmptyGuidance);
    public string LoadingDetailText => L10n.T(L10n.Key.LoadingDetail);
    public string CacheHintText => L10n.T(L10n.Key.CacheHint);
    public string ThrottleHintText => L10n.T(L10n.Key.ThrottleHint);

    /// <summary>任一弹窗打开时，覆盖层整体拦住鼠标，防止点到弹窗底下的内容。</summary>
    public bool AnyOverlayVisible =>
        _showCommandPalette || _showRouteSheet || _showAddToCollection || _showNoteEditor || _showShortcuts;

    public bool ToastVisible => Toast.Shared.IsVisible;
    public string ToastMessage => Toast.Shared.Message ?? "";
    public bool ToastCanUndo => Toast.Shared.UndoAction != null;

    private void HookToast()
    {
        Toast.Shared.Changed += () =>
        {
            Raise(nameof(ToastVisible));
            Raise(nameof(ToastMessage));
            Raise(nameof(ToastCanUndo));
        };
    }
}

/// <summary>智能路由候选（带目标库名与置信度展示）。</summary>
public sealed class RouteChoiceVm
{
    public AccessionRouter.Match Match { get; }
    public string ToolName { get; }
    public string Query { get; }
    public string Reason { get; }
    public string ConfidenceLabel { get; }
    public System.Windows.Media.Brush Brush { get; }
    public ICommand ChooseCommand { get; }

    public RouteChoiceVm(AccessionRouter.Match match, MainVm shell)
    {
        Match = match;
        var provider = ToolRegistry.Shared.Find(match.ToolId);
        ToolName = provider?.Name ?? match.ToolId;
        Query = match.Query;
        Reason = match.Reason;
        ConfidenceLabel = L10n.T(L10n.Key.MatchConfidence, (int)(match.Confidence * 100));
        Brush = provider != null ? ThemeManager.CategoryFillBrush(provider.Category) : ThemeManager.AccentFillBrush;
        ChooseCommand = new RelayCommand(() => shell.RouteChooseAt(this));
    }
}
