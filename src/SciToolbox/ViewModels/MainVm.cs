using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using SciToolbox.Core;

namespace SciToolbox.ViewModels;

public enum ShellMode { Home, Tool, Favorites, History, Collections, Settings, Global }

/// <summary>侧边栏条目显示模型（分组标题 / 工具项 / 顶级页签）。</summary>
public sealed class SidebarItemVm : ObservableObject
{
    public bool IsHeader { get; init; }
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public string Glyph { get; init; } = "";
    public System.Windows.Media.Brush Color { get; init; } = null!;
    public bool IsIndented { get; init; }

    /// <summary>缩进量随 Density 变化，界面直接绑定这个厚度而不是硬编码。</summary>
    public System.Windows.Thickness IndentThickness =>
        new(IsIndented ? Core.ThemeManager.Density.Indent() : 0, 0, 0, 0);
    public string? Badge { get; init; }
    public string? SectionKey { get; init; }
    public bool IsCollapsibleHeader { get; init; }

    public ICommand SelectCommand { get; set; } = null!;
    public ICommand ToggleCommand { get; set; } = null!;

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private bool _collapsed;
    public bool Collapsed { get => _collapsed; set { if (Set(ref _collapsed, value)) Raise(nameof(IsExpanded)); } }

    /// <summary>供折叠箭头旋转绑定使用（展开 0°，收起 -90°）。</summary>
    public double ChevronAngle => _collapsed ? -90 : 0;

    public bool IsExpanded => !_collapsed;

    private bool _childrenVisible = true;
    public bool ChildrenVisible { get => _childrenVisible; set => Set(ref _childrenVisible, value); }

    public SidebarItemVm() { }
}

public sealed class BreadcrumbVm
{
    public string Label { get; init; } = "";
}

/// <summary>整体应用外壳视图模型（对应 ContentView）。</summary>
public sealed partial class MainVm : ObservableObject
{
    private readonly ToolRegistry _registry = ToolRegistry.Shared;

    public MainVm()
    {
        _selectedMode = ShellMode.Home;
        _selectedToolId = "home";

        ToggleSidebarCommand = new RelayCommand(() => SidebarVisible = !SidebarVisible);
        ShowCommandPaletteCommand = new RelayCommand(() => ShowCommandPalette = true);
        ShowShortcutsCommand = new RelayCommand(() => ShowShortcuts = true);
        CloseCommandPaletteCommand = new RelayCommand(() => ShowCommandPalette = false);
        CloseShortcutsCommand = new RelayCommand(() => ShowShortcuts = false);
        CloseRouteCommand = new RelayCommand(() => ShowRouteSheet = false);
        CloseAddCollectionCommand = new RelayCommand(() => ShowAddToCollection = false);
        DetailPrevCommand = new RelayCommand(GoDetailPrevious, () => CanGoDetailPrevious);
        DetailNextCommand = new RelayCommand(GoDetailNext, () => CanGoDetailNext);
        GoBackCommand = new RelayCommand(GoBack, () => NavHistory.Count > 0);
        RetryDetailCommand = new RelayCommand(RetryDetail);
        ToggleCompareModeCommand = new RelayCommand(() => { CompareMode = !CompareMode; });
        ClearCompareCommand = new RelayCommand(() => { ComparisonStore.Shared.Clear(); CompareMode = false; });
        ToggleDetailFavoriteCommand = new RelayCommand(ToggleCurrentFavorite, () => DetailItemId != null);
        DetailAddToCollectionCommand = new RelayCommand(AddCurrentToCollection, () => DetailItemId != null);
        DetailAddToCompareCommand = new RelayCommand(AddCurrentToCompare, () => DetailItemId != null);

        LoadCollapsedSections();
        RebuildSidebar();
        AppLanguage.Shared.Changed += () =>
            System.Windows.Application.Current?.Dispatcher.Invoke(ReloadTexts);
        RefreshDetailFavorited();
        HookToast();

        // 初始内容 = 首页
        Content = HomeVmInstance;
        ContentTitle = L10n.T(L10n.Key.HomeTitle);
        ContentSubtitle = "";
    }

    // ===== 侧边栏 =====
    public ObservableCollection<SidebarItemVm> SidebarItems { get; } = new();
    private bool _sidebarVisible = Prefs.GetBool("sidebarVisible", true);
    public bool SidebarVisible { get => _sidebarVisible; set { if (Set(ref _sidebarVisible, value)) Prefs.SetBool("sidebarVisible", value); } }

    private readonly HashSet<string> _collapsedSections = new();

    public void RebuildSidebar()
    {
        SidebarItems.Clear();
        SidebarItems.Add(new SidebarItemVm
        {
            Id = "home", Text = L10n.T(L10n.Key.Home), Glyph = NavGlyph("home"),
            Color = ThemeManager.AccentBrush, SelectCommand = new RelayCommand(() => SelectTool("home")),
            IsSelected = _selectedToolId == "home"
        });

        foreach (var (category, providers) in _registry.Grouped())
        {
            var key = "cat-" + category;
            var brush = ThemeManager.CategoryBrush(category);
            var header = NewHeader(category.LocalizedName().ToUpperInvariant(), brush, key, category.Abbreviation());
            SidebarItems.Add(header);
            foreach (var p in providers)
                SidebarItems.Add(NewTool(p, brush, key, header.Collapsed));
        }

        var favKey = "fav";
        var favHeader = NewHeader(L10n.T(L10n.Key.FavoritesAndHistory).ToUpperInvariant(), ThemeManager.AccentBrush, favKey, null);
        SidebarItems.Add(favHeader);
        SidebarItems.Add(NewSpecial("favorites", L10n.T(L10n.Key.Favorites), "", favKey, favHeader.Collapsed));
        SidebarItems.Add(NewSpecial("history", L10n.T(L10n.Key.History), "", favKey, favHeader.Collapsed));
        SidebarItems.Add(NewSpecial("collections", L10n.T(L10n.Key.Collections), "", favKey, favHeader.Collapsed));

        var miscKey = "misc";
        var miscHeader = NewHeader(L10n.T(L10n.Key.Others).ToUpperInvariant(), ThemeManager.Brush("Brush.Ink3"), miscKey, null);
        SidebarItems.Add(miscHeader);
        SidebarItems.Add(NewSpecial("settings", L10n.T(L10n.Key.Settings), "", miscKey, miscHeader.Collapsed));
    }

    /// <summary>顶级页签字形，供侧边栏与命令面板复用。</summary>
    public static string NavGlyph(string id) => id switch
    {
        "home" => Icon(0xE80F),
        "favorites" => Icon(0xE806),
        "history" => Icon(0xE823),
        "collections" => Icon(0xE8A5),
        "settings" => Icon(0xE713),
        "global" => Icon(0xE721),
        _ => Icon(0xE721),
    };

    private static string Icon(int code) => char.ConvertFromUtf32(code);

    private const string CollapsedPrefsKey = "collapsedSections";

    /// <summary>持久化侧边栏各分组的折叠状态。</summary>
    private void LoadCollapsedSections()
    {
        _collapsedSections.Clear();
        var raw = Prefs.GetRaw(CollapsedPrefsKey, "");
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var key = token.Trim().Trim('[', ']', '"');
            if (key.Length > 0) _collapsedSections.Add(key);
        }
    }

    private void SaveCollapsedSections() =>
        Prefs.SetRaw(CollapsedPrefsKey, "[" + string.Join(",", _collapsedSections.Select(k => "\"" + k + "\"")) + "]");

    /// <summary>跳转到某个工具时自动展开它所属的分组。</summary>
    private void ExpandSectionOf(string toolId)
    {
        var key = SidebarItems.FirstOrDefault(x => !x.IsHeader && x.Id == toolId)?.SectionKey;
        if (key == null || !_collapsedSections.Contains(key)) return;
        _collapsedSections.Remove(key);
        SaveCollapsedSections();
        foreach (var it in SidebarItems.Where(x => x.SectionKey == key))
        {
            it.Collapsed = false;
            it.ChildrenVisible = true;
        }
    }

    private SidebarItemVm NewHeader(string title, System.Windows.Media.Brush brush, string key, string? badge)
    {
        var vm = new SidebarItemVm
        {
            IsHeader = true, Text = title, Color = brush, SectionKey = key, Badge = badge,
            IsCollapsibleHeader = true,
            Collapsed = _collapsedSections.Contains(key),
            ChildrenVisible = !_collapsedSections.Contains(key)
        };
        vm.ToggleCommand = new RelayCommand(() =>
        {
            if (_collapsedSections.Contains(key)) _collapsedSections.Remove(key);
            else _collapsedSections.Add(key);
            vm.Collapsed = !vm.Collapsed;
            vm.ChildrenVisible = !vm.Collapsed;
            SaveCollapsedSections();
            foreach (var it in SidebarItems.Where(x => x.SectionKey == key && !x.IsHeader))
                it.ChildrenVisible = vm.ChildrenVisible;
        });
        vm.SelectCommand = vm.ToggleCommand;
        return vm;
    }

    private SidebarItemVm NewTool(IToolProvider p, System.Windows.Media.Brush brush, string key, bool collapsed)
    {
        var vm = new SidebarItemVm
        {
            Id = p.Id, Text = p.Name, Glyph = ThemeManager.GlyphFor(p.IconName), Color = brush,
            IsIndented = true, SectionKey = key, ChildrenVisible = !collapsed,
            IsSelected = _selectedToolId == p.Id
        };
        vm.SelectCommand = new RelayCommand(() => SelectTool(p.Id));
        return vm;
    }

    private SidebarItemVm NewSpecial(string id, string text, string glyph, string key, bool collapsed)
    {
        var vm = new SidebarItemVm
        {
            Id = id, Text = text, Glyph = glyph, Color = ThemeManager.AccentBrush,
            IsIndented = true, SectionKey = key, ChildrenVisible = !collapsed,
            IsSelected = _selectedToolId == id
        };
        vm.SelectCommand = new RelayCommand(() => SelectTool(id));
        return vm;
    }

    // ===== 内容区模式 =====
    private ShellMode _selectedMode;
    public ShellMode Mode { get => _selectedMode; private set { if (Set(ref _selectedMode, value)) { Raise(nameof(IsHomeMode)); Raise(nameof(IsToolMode)); Raise(nameof(IsFavoritesMode)); Raise(nameof(IsHistoryMode)); Raise(nameof(IsCollectionsMode)); Raise(nameof(IsSettingsMode)); Raise(nameof(IsGlobalMode)); } } }
    public bool IsHomeMode => Mode == ShellMode.Home;
    public bool IsToolMode => Mode == ShellMode.Tool;
    public bool IsFavoritesMode => Mode == ShellMode.Favorites;
    public bool IsHistoryMode => Mode == ShellMode.History;
    public bool IsCollectionsMode => Mode == ShellMode.Collections;
    public bool IsSettingsMode => Mode == ShellMode.Settings;
    public bool IsGlobalMode => Mode == ShellMode.Global;

    private string _selectedToolId = "home";
    public string SelectedToolId { get => _selectedToolId; private set => Set(ref _selectedToolId, value); }

    public object? Content { get; private set; }
    public string ContentTitle { get; private set; } = "";
    public string ContentSubtitle { get; private set; } = "";

    private readonly Dictionary<string, string> _toolQueries = new();

    public void SelectTool(string id)
    {
        if (id == SelectedToolId) return;

        // 折返：撤销上一次跳转
        if (NavHistory.Count > 0 && NavHistory[^1].ToolId == id)
        {
            var last = NavHistory[^1];
            NavHistory.RemoveAt(NavHistory.Count - 1);
            NavigateTo(id, last.Query, false);
            RebuildBreadcrumb();
            return;
        }

        // 记录当前到历史
        PushNav(SelectedToolId);
        NavigateTo(id, null, false);
    }

    private void NavigateTo(string toolId, string? query, bool autoDetail)
    {
        // 清除详情状态
        ClearDetail();

        switch (toolId)
        {
            case "home":
                Mode = ShellMode.Home; Content = HomeVmInstance; ContentTitle = L10n.T(L10n.Key.HomeTitle); ContentSubtitle = ""; break;
            case "favorites":
                Mode = ShellMode.Favorites; Content = FavoritesVmInstance; ContentTitle = L10n.T(L10n.Key.Favorites); ContentSubtitle = ""; break;
            case "history":
                Mode = ShellMode.History; Content = HistoryVmInstance; ContentTitle = L10n.T(L10n.Key.History); ContentSubtitle = ""; break;
            case "collections":
                Mode = ShellMode.Collections; Content = CollectionsVmInstance; ContentTitle = L10n.T(L10n.Key.Collections); ContentSubtitle = ""; break;
            case "settings":
                Mode = ShellMode.Settings; Content = SettingsVmInstance; ContentTitle = L10n.T(L10n.Key.Settings); ContentSubtitle = ""; break;
            case "global":
                Mode = ShellMode.Global; Content = GlobalVmInstance; ContentTitle = L10n.T(L10n.Key.GlobalSearch); ContentSubtitle = ""; break;
            default:
                var provider = _registry.Find(toolId);
                if (provider == null) { Mode = ShellMode.Home; Content = HomeVmInstance; break; }
                Mode = ShellMode.Tool;
                var svm = new SearchVm(provider, this);
                SearchVmInstance = svm;
                Content = svm;
                ContentTitle = provider.Name;
                if (!string.IsNullOrEmpty(query))
                    svm.Prefill(query, autoDetail);
                else
                    _ = svm.SeedGtdbAsync();
                break;
        }

        ExpandSectionOf(toolId);
        SelectedToolId = toolId;
        foreach (var it in SidebarItems)
            it.IsSelected = !it.IsHeader && it.Id == toolId;
        Raise(nameof(Content));
        Raise(nameof(ContentTitle));
        Raise(nameof(ContentSubtitle));
        Raise(nameof(ShowDetailEmptyGuidance));
    }

    private void PushNav(string fromToolId)
    {
        if (string.IsNullOrEmpty(fromToolId)) return;
        NavHistory.Add(new NavEntry(fromToolId, _toolQueries.TryGetValue(fromToolId, out var q) ? q : null, ToolDisplayName(fromToolId)));
        if (NavHistory.Count > 10) NavHistory.RemoveRange(0, NavHistory.Count - 10);
        RebuildBreadcrumb();
    }

    public void OnToolQueryChanged(string toolId, string query) => _toolQueries[toolId] = query;

    public void OpenGlobalSearch(string query)
    {
        GlobalVmInstance.Run(query);
        PushNav(SelectedToolId);
        NavigateTo("global", null, false);
    }

    // ===== 导航历史 / 面包屑 =====
    public sealed class NavEntry { public string ToolId; public string? Query; public string ToolName; public NavEntry(string t, string? q, string n) { ToolId = t; Query = q; ToolName = n; } }
    public List<NavEntry> NavHistory { get; } = new();
    public ObservableCollection<BreadcrumbVm> Breadcrumb { get; } = new();
    public void RebuildBreadcrumb()
    {
        Breadcrumb.Clear();
        foreach (var e in NavHistory.TakeLast(3))
            Breadcrumb.Add(new BreadcrumbVm { Label = e.ToolName });
        Raise(nameof(HasBreadcrumb));
    }
    public bool HasBreadcrumb => NavHistory.Count > 0 || DetailHistory.Count > 0;
    private void GoBack()
    {
        if (NavHistory.Count == 0) return;
        var last = NavHistory[^1];
        NavHistory.RemoveAt(NavHistory.Count - 1);
        NavigateTo(last.ToolId, last.Query, false);
        RebuildBreadcrumb();
    }

    public string ToolDisplayName(string id)
    {
        var p = _registry.Find(id);
        if (p != null) return p.Name;
        return id switch
        {
            "home" => L10n.T(L10n.Key.Home),
            "favorites" => L10n.T(L10n.Key.Favorites),
            "history" => L10n.T(L10n.Key.History),
            "collections" => L10n.T(L10n.Key.Collections),
            "settings" => L10n.T(L10n.Key.Settings),
            "global" => L10n.T(L10n.Key.GlobalSearch),
            _ => id
        };
    }

    // ===== 详情栏 =====
    public object? DetailContent { get; private set; }   // DetailVm | DetailEmptyVm | error | compareVm
    private DetailVm? _currentDetail;
    public string? DetailItemId { get; private set; }
    public string? DetailToolId { get; private set; }
    public Dictionary<string, string>? DetailContext { get; private set; }

    private CancellationTokenSource? _detailCts;

    public bool ShowDetailEmptyGuidance => _currentDetail == null && !DetailLoading;

    private void ClearDetail()
    {
        _detailCts?.Cancel();
        _currentDetail = null;
        DetailContent = null;
        DetailItemId = null;
        DetailToolId = null;
        DetailContext = null;
        DetailLoading = false;
        DetailError = null;
        CompareMode = false;
        Raise(nameof(DetailContent));
        Raise(nameof(ShowDetailEmptyGuidance));
    }

    private bool _detailLoading;
    public bool DetailLoading { get => _detailLoading; private set { Set(ref _detailLoading, value); Raise(nameof(ShowLoadingOverlay)); } }
    public bool ShowLoadingOverlay => _detailLoading && _currentDetail != null;

    private string? _detailError;
    public string? DetailError { get => _detailError; private set { Set(ref _detailError, value); Raise(nameof(HasDetailError)); Raise(nameof(DetailErrorText)); } }
    public bool HasDetailError => !string.IsNullOrEmpty(_detailError);
    public string DetailErrorText => _detailError ?? "";

    public string DetailHeaderTitle => _currentDetail?.HeaderTitle ?? "";

    public void OpenDetail(ResultItem item, IToolProvider provider) =>
        FetchDetail(item.Id, provider, item.Extra, true);

    public void FetchDetail(string itemId, IToolProvider provider, Dictionary<string, string>? extra, bool recordHistory)
    {
        _detailCts?.Cancel();
        _detailCts = new CancellationTokenSource();
        var ct = _detailCts.Token;

        DetailLoading = true;
        DetailError = null;
        DetailItemId = itemId;
        DetailToolId = provider.Id;
        DetailContext = extra;
        CompareMode = false;
        Raise(nameof(DetailItemId));
        Raise(nameof(DetailToolId));

        if (recordHistory)
        {
            if (DetailHistory.Count > 0)
            {
                var top = DetailHistory[^1];
                if (top.ToolId == provider.Id && top.ItemId == itemId) { DetailCursor = DetailHistory.Count - 1; }
                else AddVisit(provider.Id, itemId, extra);
            }
            else AddVisit(provider.Id, itemId, extra);
        }

        _ = FetchDetailAsync(itemId, provider, extra, ct);
    }

    private void AddVisit(string toolId, string itemId, Dictionary<string, string>? extra)
    {
        DetailHistory.Add(new DetailVisit(toolId, itemId, extra));
        if (DetailHistory.Count > 50) DetailHistory.RemoveRange(0, DetailHistory.Count - 50);
        DetailCursor = DetailHistory.Count - 1;
    }

    private async Task FetchDetailAsync(string itemId, IToolProvider provider, Dictionary<string, string>? extra, CancellationToken ct)
    {
        try
        {
            var model = await provider.DetailAsync(itemId, extra, ct);
            if (ct.IsCancellationRequested) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _currentDetail = BuildDetailVm(model, provider);
                DetailContent = _currentDetail;
                DetailLoading = false;
                Raise(nameof(DetailContent));
                Raise(nameof(CurrentDetail));
                Raise(nameof(HasDetailState));
                Raise(nameof(ShowDetailEmptyGuidance));
                Raise(nameof(ShowLoadingOverlay));
                Raise(nameof(DetailHeaderTitle));
            });
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex)
        {
            if (ct.IsCancellationRequested) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                DetailError = ex.FriendlyMessage;
                DetailLoading = false;
                _currentDetail = null;
                DetailContent = null;
                Raise(nameof(DetailContent));
                Raise(nameof(CurrentDetail));
                Raise(nameof(ShowDetailEmptyGuidance));
            });
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                DetailError = ex.Message;
                DetailLoading = false;
                _currentDetail = null;
                DetailContent = null;
            });
        }
    }

    private DetailVm BuildDetailVm(DetailModel model, IToolProvider provider)
    {
        bool fav = DetailItemId != null && LocalFavorites.Shared.IsFavorited(provider.Id, DetailItemId);
        return new DetailVm(model, provider, CopyText, OpenLink, GotoXlink, PreviewXlink, HandleDetailAction,
            ToggleCurrentFavorite, AddCurrentToCollection, AddCurrentToCompare, () => { if (model.WebUrl != null) OpenLink(model.WebUrl); }, fav);
    }

    private void RetryDetail()
    {
        if (DetailToolId == null || DetailItemId == null) return;
        var p = _registry.Find(DetailToolId);
        if (p != null) FetchDetail(DetailItemId, p, DetailContext, false);
    }

    // ===== 详情浏览历史 =====
    public sealed class DetailVisit { public string ToolId; public string ItemId; public Dictionary<string, string>? Context; public DetailVisit(string t, string i, Dictionary<string, string>? c) { ToolId = t; ItemId = i; Context = c; } }
    public List<DetailVisit> DetailHistory { get; } = new();
    public int DetailCursor { get; private set; } = -1;
    public bool CanGoDetailPrevious => DetailCursor > 0;
    public bool CanGoDetailNext => DetailCursor >= 0 && DetailCursor + 1 < DetailHistory.Count;
    private void GoDetailPrevious()
    {
        if (!CanGoDetailPrevious) return;
        DetailCursor--;
        var v = DetailHistory[DetailCursor];
        var p = _registry.Find(v.ToolId);
        if (p != null) FetchDetail(v.ItemId, p, v.Context, false);
    }
    private void GoDetailNext()
    {
        if (!CanGoDetailNext) return;
        DetailCursor++;
        var v = DetailHistory[DetailCursor];
        var p = _registry.Find(v.ToolId);
        if (p != null) FetchDetail(v.ItemId, p, v.Context, false);
    }

    // ===== 跨库互链 =====
    private void GotoXlink(XLink link)
    {
        var provider = _registry.Find(link.ToolId);
        if (provider == null) return;
        PushNav(SelectedToolId);
        NavigateTo(link.ToolId, link.Query, true);
    }
    private void PreviewXlink(XLink link)
    {
        var provider = _registry.Find(link.ToolId);
        if (provider == null) return;
        FetchDetail(link.Query, provider, null, true);
    }

    // ===== 复制 / 打开链接 / 动作 =====
    private void CopyText(string s) => ClipboardUtil.Copy(s, L10n.T(L10n.Key.Copied));
    private void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
    private void HandleDetailAction(DetailAction a)
    {
        if (a.Kind == DetailAction.ActionKind.Export)
        {
            var baseName = a.ExportFileName ?? "citation";
            var ext = a.ExportExt ?? "txt";
            if (ExportUtil.SaveTextFile(a.Payload, baseName, ext))
                Toast.Show(L10n.T(L10n.Key.Exported, $"{baseName}.{ext}"));
        }
        else
        {
            ClipboardUtil.Copy(a.Payload, a.Label);
        }
    }

    // ===== 收藏 =====
    private void ToggleCurrentFavorite()
    {
        if (DetailItemId == null || DetailToolId == null || _currentDetail == null) return;
        var tool = _registry.Find(DetailToolId);
        if (tool == null) return;
        LocalFavorites.Shared.Toggle(DetailToolId, tool.Name, DetailItemId,
            _currentDetail.HeaderTitle, _currentDetail.HeaderSubtitle, FavoriteSnapshot(_currentDetail));
        RefreshDetailFavorited();
    }

    private void RefreshDetailFavorited()
    {
        if (_currentDetail != null && DetailItemId != null && DetailToolId != null)
            _currentDetail.IsFavorited = LocalFavorites.Shared.IsFavorited(DetailToolId, DetailItemId);
    }

    private static Dictionary<string, string> FavoriteSnapshot(DetailVm vm)
    {
        var dict = new Dictionary<string, string>();
        foreach (var s in vm.Sections.Take(4))
            foreach (var r in s.Rows.Take(4))
            {
                if (dict.Count >= 10) return dict;
                dict[r.Key] = r.Value;
            }
        return dict;
    }

    // ===== 加入集合 =====
    public List<CollectionCandidate> CollectionCandidates { get; private set; } = new();
    private bool _showAddToCollection;
    public bool ShowAddToCollection { get => _showAddToCollection; set { Set(ref _showAddToCollection, value); Raise(nameof(AddCollectionVisible)); Raise(nameof(AnyOverlayVisible)); } }
    public bool AddCollectionVisible => _showAddToCollection;
    public ICommand CloseAddCollectionCommand { get; }

    private void AddCurrentToCollection()
    {
        if (DetailItemId == null || DetailToolId == null || _currentDetail == null) return;
        var tool = _registry.Find(DetailToolId);
        if (tool == null) return;
        CollectionCandidates = new List<CollectionCandidate>
        {
            new() { ToolId = DetailToolId, ToolName = tool.Name, ItemId = DetailItemId,
                    Title = _currentDetail.HeaderTitle, Subtitle = _currentDetail.HeaderSubtitle, Context = DetailContext }
        };
        AddCollectionVmInstance.Candidates = CollectionCandidates;
        AddCollectionVmInstance.Populate();
        ShowAddToCollection = true;
    }

    // ===== 对比 =====
    private bool _compareMode;
    public bool CompareMode { get => _compareMode; private set { Set(ref _compareMode, value); Raise(nameof(ShowCompareView)); Raise(nameof(ShowSingleDetail)); Raise(nameof(CompareModeLabel)); } }
    public string CompareModeLabel => _compareMode ? L10n.T(L10n.Key.ExitCompare) : L10n.T(L10n.Key.ViewCompare);
    public bool ShowCompareView => _compareMode && ComparisonStore.Shared.Items.Count > 0;
    public bool ShowSingleDetail => !ShowCompareView;
    public bool HasCompareItems => ComparisonStore.Shared.Items.Count > 0;
    public ICommand ToggleCompareModeCommand { get; }
    public ICommand ClearCompareCommand { get; }

    private void AddCurrentToCompare()
    {
        if (DetailItemId == null || DetailToolId == null || _currentDetail == null) return;
        var tool = _registry.Find(DetailToolId);
        if (tool == null) return;
        var subtitle = DetailContext != null && DetailContext.TryGetValue("query", out var qq) ? qq : _currentDetail.HeaderTitle;
        ComparisonStore.Shared.Add(DetailToolId, tool.Name, tool.Category, DetailItemId, subtitle, DetailContext, _currentDetail.ModelForCompare());
        Raise(nameof(HasCompareItems));
    }

    // ===== 命令面板 =====
    private bool _showCommandPalette;
    public bool ShowCommandPalette { get => _showCommandPalette; set { Set(ref _showCommandPalette, value); Raise(nameof(CommandPaletteVisible)); Raise(nameof(AnyOverlayVisible)); } }
    public bool CommandPaletteVisible => _showCommandPalette;
    public ICommand ShowCommandPaletteCommand { get; }
    public ICommand CloseCommandPaletteCommand { get; }

    // ===== 快捷键速查 =====
    private bool _showShortcuts;
    public bool ShowShortcuts { get => _showShortcuts; set { Set(ref _showShortcuts, value); Raise(nameof(ShortcutsVisible)); Raise(nameof(AnyOverlayVisible)); } }
    public bool ShortcutsVisible => _showShortcuts;
    public ICommand ShowShortcutsCommand { get; }
    public ICommand CloseShortcutsCommand { get; }

    // ===== 智能路由 =====
    private bool _showRouteSheet;
    public bool ShowRouteSheet { get => _showRouteSheet; set { Set(ref _showRouteSheet, value); Raise(nameof(RouteVisible)); Raise(nameof(AnyOverlayVisible)); } }
    public bool RouteVisible => _showRouteSheet;
    public List<AccessionRouter.Match> RouteMatches { get; private set; } = new();
    public ICommand CloseRouteCommand { get; }

    public void RouteAccession(string input)
    {
        var matches = AccessionRouter.Classify(input);
        if (matches.Count == 0) { Toast.Show(L10n.T(L10n.Key.SmartRouteFailed)); return; }
        var decision = AccessionRouter.Decide(matches);
        if (decision.Kind == AccessionRouter.DecisionKind.Route && decision.Match != null)
        {
            var m = decision.Match;
            var provider = _registry.Find(m.ToolId);
            if (provider != null) { PushNav(SelectedToolId); NavigateTo(m.ToolId, m.Query, false); }
        }
        else
        {
            RouteMatches = matches;
            BuildRouteOptions();
            ShowRouteSheet = true;
        }
    }

    public void ChooseRoute(AccessionRouter.Match match)
    {
        ShowRouteSheet = false;
        var provider = _registry.Find(match.ToolId);
        if (provider == null) return;
        PushNav(SelectedToolId);
        NavigateTo(match.ToolId, match.Query, false);
    }

    // ===== 离线 / 节流 / 缓存 提示 =====
    public bool IsOffline => !NetworkMonitor.Shared.IsOnline;
    private bool _throttleHint;
    public bool ThrottleHint { get => _throttleHint; set { Set(ref _throttleHint, value); Raise(nameof(ThrottleHint)); } }
    private bool _cacheHint;
    public bool CacheHint { get => _cacheHint; set { Set(ref _cacheHint, value); Raise(nameof(CacheHint)); } }
    private System.Timers.Timer? _cacheHintTimer;

    public void HookNetworkSignals()
    {
        ApiClient.CacheHit += _ => ShowCacheHint();
        ApiClient.ThrottleWaiting += _ => System.Windows.Application.Current.Dispatcher.Invoke(() => ThrottleHint = true);
        ApiClient.ThrottleResumed += _ => System.Windows.Application.Current.Dispatcher.Invoke(() => ThrottleHint = false);
        NetworkMonitor.Shared.Changed += () => System.Windows.Application.Current.Dispatcher.Invoke(() => Raise(nameof(IsOffline)));
        ComparisonStore.Shared.Changed += () => System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Raise(nameof(HasCompareItems));
            Raise(nameof(ShowCompareView));
            Raise(nameof(CompareCount));
        });
    }

    private void ShowCacheHint()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => CacheHint = true);
        _cacheHintTimer?.Dispose();
        _cacheHintTimer = new System.Timers.Timer(3000) { AutoReset = false };
        _cacheHintTimer.Elapsed += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(() => CacheHint = false);
        _cacheHintTimer.Start();
    }

    // ===== 子视图模型 =====
    private HomeVm? _home;
    public HomeVm HomeVmInstance => _home ??= new HomeVm(this);
    private FavoritesVm? _favorites;
    public FavoritesVm FavoritesVmInstance => _favorites ??= new FavoritesVm(this);
    private HistoryVm? _history;
    public HistoryVm HistoryVmInstance => _history ??= new HistoryVm(this);
    private CollectionsVm? _collections;
    public CollectionsVm CollectionsVmInstance => _collections ??= new CollectionsVm(this);
    private SettingsVm? _settings;
    public SettingsVm SettingsVmInstance => _settings ??= new SettingsVm(this);
    private GlobalSearchVm? _global;
    public GlobalSearchVm GlobalVmInstance => _global ??= new GlobalSearchVm(this);
    public SearchVm? SearchVmInstance { get; private set; }

    public ICommand ToggleSidebarCommand { get; }
    public ICommand DetailPrevCommand { get; }
    public ICommand DetailNextCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand ToggleDetailFavoriteCommand { get; }
    public ICommand DetailAddToCollectionCommand { get; }
    public ICommand DetailAddToCompareCommand { get; }
    public ICommand RetryDetailCommand { get; }

    public void OpenToolById(string id) => SelectTool(id);
    public void OpenDetailFromResult(ResultItem item, SearchVm svm) => FetchDetail(item.Id, svm.Provider, item.Extra, true);
}
