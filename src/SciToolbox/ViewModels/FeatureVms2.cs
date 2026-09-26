using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SciToolbox.Core;

namespace SciToolbox.ViewModels;

// ===================== 项目集合 =====================

public sealed class CollectionEntryRowVm : SciToolbox.Views.IActivatableRow
{
    public CollectionEntry Entry { get; }
    public string Title => Entry.Title;
    public string? Subtitle => Entry.Subtitle;
    public bool HasSubtitle => !string.IsNullOrEmpty(Entry.Subtitle);
    public string ToolName => Entry.ToolName;
    public string? Note => Entry.Note;
    public bool HasNote => !string.IsNullOrEmpty(Entry.Note);
    public System.Windows.Media.Brush Brush { get; }
    public ICommand OpenCommand { get; }
    public System.Windows.Input.ICommand? ActivateCommand => OpenCommand;
    public ICommand RemoveCommand { get; }
    public ICommand EditNoteCommand { get; }

    public CollectionEntryRowVm(CollectionEntry entry, string collectionId, CollectionsVm vm)
    {
        Entry = entry;
        var provider = ToolRegistry.Shared.Find(entry.ToolId);
        Brush = provider != null ? ThemeManager.CategoryFillBrush(provider.Category) : ThemeManager.AccentFillBrush;
        OpenCommand = new RelayCommand(() => vm.OpenEntry(collectionId, entry));
        RemoveCommand = new RelayCommand(() => vm.RemoveEntry(collectionId, entry));
        EditNoteCommand = new RelayCommand(() => vm.EditEntryNote(collectionId, entry));
    }
}

public sealed class CollectionRowVm
{
    public SciCollection Model { get; }
    public string Id => Model.Id;
    public string Name => Model.Name;
    public string? Note => Model.Note;
    public bool HasNote => !string.IsNullOrEmpty(Model.Note);
    public int Count => Model.Entries.Count;
    public string CountLabel => L10n.T(L10n.Key.EntityCount, Model.Entries.Count);
    public string RelativeUpdated => Model.RelativeUpdated;
    public string RenameTip => L10n.T(L10n.Key.Rename);
    public string DeleteTip => L10n.T(L10n.Key.DeleteCollection);
    public System.Windows.Input.ICommand? ActivateCommand => OpenCommand;
    public ICommand OpenCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RenameCommand { get; }

    public CollectionRowVm(SciCollection model, CollectionsVm vm)
    {
        Model = model;
        OpenCommand = new RelayCommand(() => vm.SelectCollection(model.Id));
        DeleteCommand = new RelayCommand(() => vm.DeleteCollection(model.Id));
        RenameCommand = new RelayCommand(() => vm.RenameCollection(model.Id));
    }
}

public sealed class CollectionsVm : ObservableObject
{
    private readonly MainVm _shell;

    public CollectionsVm(MainVm shell)
    {
        _shell = shell;
        NewCommand = new RelayCommand(() => shell.CreateCollection());
        ExportAllJsonCommand = new RelayCommand(() => shell.ExportCollectionsJson());
        BackToListCommand = new RelayCommand(() => { Selected = null; SelectedEntries.Clear(); RaiseAll(); });
        ExportCsvCommand = new RelayCommand(() => { if (_selected != null) _shell.ExportCollectionCsv(_selected.Id); }, () => !Verifying);
        BatchVerifyCommand = new AsyncRelayCommand(async () =>
        {
            if (_selected == null || Verifying) return;
            Verifying = true; _verified = 0;
            try { await _shell.BatchVerify(_selected.Id, done => { _verified = done; Raise(nameof(VerifyProgressLabel)); }); }
            finally { Verifying = false; }
        }, () => HasSelected && !Verifying);
        Rebuild();
        CollectionStore.Shared.Changed += () => System.Windows.Application.Current.Dispatcher.Invoke(Rebuild);
    }

    public ObservableCollection<CollectionRowVm> Collections { get; } = new();
    public ObservableCollection<CollectionEntryRowVm> SelectedEntries { get; } = new();
    public string Title => L10n.T(L10n.Key.CollectionsTitle);
    public string Empty => L10n.T(L10n.Key.CollectionsEmpty);
    public string SelectedEmpty => L10n.T(L10n.Key.CollectionEmpty);
    public string NewLabel => L10n.T(L10n.Key.NewCollection);
    public string ExportAllLabel => L10n.T(L10n.Key.ExportJSON);
    public string BackLabel => L10n.T(L10n.Key.BackToList);
    public string ExportCsvLabel => L10n.T(L10n.Key.ExportCSV);
    public string VerifyHintLabel => L10n.T(L10n.Key.BatchVerifyHint, _selected?.Entries.Count ?? 0);
    public string EntryNoteLabel => L10n.T(L10n.Key.EditNote);
    public string RemoveLabel => L10n.T(L10n.Key.RemoveFromCollectionA11y);
    public string RenameLabel => L10n.T(L10n.Key.Rename);
    public string DeleteLabel => L10n.T(L10n.Key.DeleteCollection);
    public string OpenLabel => SciToolbox.Core.DT.T("打开", "Open");
    public bool ShowListEmpty => !HasCollections;
    public bool ShowSelectionEmpty => HasSelected && NoEntities;
    public bool HasCollections => Collections.Count > 0;

    private SciCollection? _selected;
    public SciCollection? Selected { get => _selected; set { Set(ref _selected, value); Raise(nameof(HasSelected)); Raise(nameof(SelectedTitle)); } }
    public bool HasSelected => _selected != null;
    public string SelectedTitle => _selected?.Name ?? "";
    public bool NoEntities => _selected != null && _selected.Entries.Count == 0;

    public ICommand NewCommand { get; }
    public ICommand ExportAllJsonCommand { get; }
    public ICommand BackToListCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand BatchVerifyCommand { get; }

    private bool _verifying;
    /// <summary>批量核对会逐条请求，耗时较长，必须有可见进度，否则界面像卡死。</summary>
    public bool Verifying
    {
        get => _verifying;
        set { if (Set(ref _verifying, value)) { Raise(nameof(CanAct)); Raise(nameof(VerifyProgressLabel)); } }
    }
    public bool CanAct => !_verifying;
    public string VerifyLabel => L10n.T(L10n.Key.BatchVerify);
    public string VerifyProgressLabel => L10n.T(L10n.Key.Progress, _verified, _selected?.Entries.Count ?? 0);

    private int _verified;

    public void Rebuild()
    {
        Collections.Clear();
        foreach (var c in CollectionStore.Shared.Collections)
            Collections.Add(new CollectionRowVm(c, this));
        RaiseAll();
        // 刷新选中
        if (_selected != null)
        {
            var cur = CollectionStore.Shared.Find(_selected.Id);
            ShowEntries(cur);
        }
    }

    public void SelectCollection(string id)
    {
        _selected = CollectionStore.Shared.Find(id);
        ShowEntries(_selected);
        Raise(nameof(Selected));
        Raise(nameof(HasSelected));
        Raise(nameof(SelectedTitle));
    }

    private void ShowEntries(SciCollection? c)
    {
        SelectedEntries.Clear();
        if (c == null) return;
        foreach (var e in c.Entries)
            SelectedEntries.Add(new CollectionEntryRowVm(e, c.Id, this));
        Raise(nameof(NoEntities));
    }

    public void DeleteCollection(string id) => _shell.DeleteCollection(id);
    public void RenameCollection(string id) => _shell.RenameCollection(id);
    public void OpenEntry(string collectionId, CollectionEntry entry) => _shell.OpenCollectionEntry(entry);
    public void RemoveEntry(string collectionId, CollectionEntry entry) => _shell.RemoveCollectionEntry(collectionId, entry);
    public void EditEntryNote(string collectionId, CollectionEntry entry) => _shell.EditCollectionEntryNote(collectionId, entry);
}

// ===================== 设置 =====================

public sealed class SettingsVm : ObservableObject
{
    private readonly MainVm _shell;
    public SettingsVm(MainVm shell)
    {
        _shell = shell;
        ClearCacheCommand = new RelayCommand(() =>
        {
            if (!_shell.ConfirmClear(L10n.T(L10n.Key.ConfirmClearCache))) return;
            ResponseCache.Shared.Clear();
            Toast.Show(L10n.T(L10n.Key.CacheCleared));
        });
        ShowShortcutsCommand = new RelayCommand(() => shell.ShowShortcuts = true);
    }

    public string Title => L10n.T(L10n.Key.Settings);
    public string AppearanceSection => L10n.T(L10n.Key.Appearance);
    public string ThemeLabel => L10n.T(L10n.Key.Theme);
    public string DensityLabel => L10n.T(L10n.Key.SidebarDensity);
    public string LanguageLabel => L10n.T(L10n.Key.Language);
    public string CacheSection => L10n.T(L10n.Key.Cache);
    public string EnableCacheLabel => L10n.T(L10n.Key.EnableCache);
    public string DataSection => L10n.T(L10n.Key.DataManagement);
    public string ShortcutsSection => L10n.T(L10n.Key.ShortcutsSection);
    public string AboutSection => L10n.T(L10n.Key.About);
    public string AboutText => L10n.T(L10n.Key.AboutText);
    public string VersionValue => "1.0.0";
    public string VersionLabel => L10n.T(L10n.Key.Version);
    public string OsValue => "Windows 10 / 11";
    public string OsLabel => L10n.T(L10n.Key.OsVersion);

    public string ThemeSystem => L10n.T(L10n.Key.ThemeSystem);
    public string ThemeLight => L10n.T(L10n.Key.ThemeLight);
    public string ThemeDark => L10n.T(L10n.Key.ThemeDark);
    public string DensityCompact => L10n.T(L10n.Key.DensityCompact);
    public string DensityNormal => L10n.T(L10n.Key.DensityNormal);
    public string DensitySpacious => L10n.T(L10n.Key.DensitySpacious);

    public bool CacheEnabled
    {
        get => ResponseCache.Shared.Enabled;
        set
        {
            ResponseCache.Shared.Enabled = value;
            RaiseAll();
        }
    }
    public string CacheStateLabel => L10n.T(ResponseCache.Shared.Enabled ? L10n.Key.CacheEnabled : L10n.Key.CacheDisabled);
    public string ClearCacheLabel => L10n.T(L10n.Key.ClearCache);
    public string ExportAllLabel => L10n.T(L10n.Key.ExportAllData);
    public string ResetLabel => L10n.T(L10n.Key.ResetApp);
    public string ShortcutsLabel => L10n.T(L10n.Key.KeyboardShortcuts);
    public string ClearHistoryLabel => L10n.T(L10n.Key.ClearHistory);
    public string ClearFavoritesLabel => L10n.T(L10n.Key.ClearFavorites);
    public string RevisitOnboardingLabel => L10n.T(L10n.Key.RevisitOnboarding);

    public string SelectedTheme
    {
        get => ThemeManager.Mode.ToString();
        set { if (Enum.TryParse<AppThemeMode>(value, out var m)) ThemeManager.Mode = m; }
    }
    public string SelectedDensity
    {
        get => ThemeManager.Density.ToString();
        set { if (Enum.TryParse<SidebarDensity>(value, out var d)) ThemeManager.Density = d; }
    }
    public string SelectedLanguage
    {
        get => AppLanguage.Cur == AppLanguage.Language.En ? "en" : "zh";
        set
        {
            AppLanguage.Shared.Current = value == "en" ? AppLanguage.Language.En : AppLanguage.Language.Zh;
        }
    }

    public ICommand ClearCacheCommand { get; }
    public ICommand ShowShortcutsCommand { get; }
    public ICommand ClearHistoryCommand => new RelayCommand(() => _shell.ClearHistory());
    public ICommand ClearFavoritesCommand => new RelayCommand(() => _shell.ClearFavorites());
    public ICommand ExportAllCommand => new RelayCommand(() => _shell.ExportAllData());
    public ICommand ResetAppCommand => new RelayCommand(() => _shell.ResetApp());
}

// ===================== 全库搜索 =====================

public sealed class GlobalProviderResultVm : ObservableObject
{
    public string ToolId { get; }
    public string Name { get; }
    public string Glyph { get; }
    public System.Windows.Media.Brush Brush { get; }
    public string Query { get; }
    public List<ResultItem> Items { get; }
    public string? Error { get; }
    public MainVm Shell { get; }

    public bool HasItems => Items.Count > 0;
    public bool ShowFail => !HasItems && Error != null;
    public bool ShowEmpty => !HasItems && Error == null;
    public bool HasError => string.IsNullOrEmpty(Error) == false && Items.Count == 0;
    public string CountLabel => L10n.T(L10n.Key.ResultCountShort, Items.Count);
    public List<ResultItem> TopItems => Items.Take(5).ToList();
    public bool HasMoreItems => Items.Count > 5;
    public string MoreLabel => HasMoreItems ? L10n.T(L10n.Key.ResultCount, Items.Count - TopItems.Count) : "";
    public string FailLabel => Error != null ? L10n.T(L10n.Key.FetchFailed, Error) : "";
    public string EmptyLabel => HasItems || Error != null ? "" : L10n.T(L10n.Key.NoResultMatch, Query);
    public string OpenLabel => L10n.T(L10n.Key.RouteTo, Name);
    public ICommand OpenInToolCommand => new RelayCommand(() => Shell.OpenToolWithQuery(ToolId, Query));

    public GlobalProviderResultVm(IToolProvider provider, string query, List<ResultItem> items, string? error, MainVm shell)
    {
        ToolId = provider.Id; Name = provider.Name; Glyph = ThemeManager.GlyphFor(provider.IconName);
        Brush = ThemeManager.CategoryBrush(provider.Category); Query = query; Items = items; Error = error; Shell = shell;
    }
}

public sealed class GlobalCategoryVm
{
    public string Name { get; init; } = "";
    public string Abbrev { get; init; } = "";
    public void RaiseTexts() { foreach (var p in Providers) p.RaiseAll(); }
    public System.Windows.Media.Brush Brush { get; init; } = null!;
    public List<GlobalProviderResultVm> Providers { get; init; } = new();
}

public sealed class GlobalSearchVm : ObservableObject
{
    private readonly MainVm _shell;
    private CancellationTokenSource? _cts;
    public GlobalSearchVm(MainVm shell)
    {
        _shell = shell;
        ReRunCommand = new RelayCommand(() => Run(_query), () => _query.Length > 0);
        ClearCommand = new RelayCommand(() => { Groups.Clear(); _query = ""; RaiseAll(); });
    }

    private string _query = "";
    private bool _loading;
    public bool Loading { get => _loading; set { Set(ref _loading, value); Raise(nameof(Loading)); Raise(nameof(NotLoading)); } }
    public bool NotLoading => !_loading;
    public string Query => _query;
    public ICommand ReRunCommand { get; }

    public ObservableCollection<GlobalCategoryVm> Groups { get; } = new();
    public bool HasGroups => Groups.Count > 0;
    public bool ShowEmpty => !_loading && Groups.Count == 0;
    public string Header => L10n.T(L10n.Key.GlobalSearch);
    public string EmptyLabel => L10n.T(L10n.Key.NoResultMatch, _query);
    public string QueryLabel => L10n.T(L10n.Key.QueryLabel, _query);
    public string RetryLabel => L10n.T(L10n.Key.Retry);
    public string SearchLabel => L10n.T(L10n.Key.SearchInAll, _query);
    public ICommand ClearCommand { get; }

    /// <summary>语言切换后刷新各分组 / 各库的文案。</summary>
    public void RebuildGroupTexts()
    {
        RaiseAll();
        foreach (var g in Groups) g.RaiseTexts();
    }

    public void Run(string query)
    {
        _query = query;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        Loading = true;
        Groups.Clear();
        Raise(nameof(Query));
        _ = RunAsync(query, ct);
    }

    private async Task RunAsync(string query, CancellationToken ct)
    {
        try { await RunCoreAsync(query, ct); }
        finally { if (!ct.IsCancellationRequested) Loading = false; }
    }

    private async Task RunCoreAsync(string query, CancellationToken ct)
    {
        var providers = ToolRegistry.Shared.Providers;
        var tasks = providers.Select(async p =>
        {
            try
            {
                var r = await p.SearchAsync(query, 0, null, ct);
                return (p, items: r.Items, error: (string?)null);
            }
            catch (Exception ex) { return (p, items: new List<ResultItem>(), error: (string?)ex.Message); }
        });
        var results = await Task.WhenAll(tasks);
        if (ct.IsCancellationRequested) return;

        var dict = results.ToDictionary(x => x.p.Id);
        var groups = new List<GlobalCategoryVm>();
        foreach (var cat in ToolCategoryExtensions.Ordered)
        {
            var ps = providers.Where(p => p.Category == cat).ToList();
            if (ps.Count == 0) continue;
            var provResults = ps.Select(p =>
            {
                var res = dict[p.Id];
                return new GlobalProviderResultVm(p, query, res.items, res.error, _shell);
            }).ToList();
            groups.Add(new GlobalCategoryVm
            {
                Name = cat.LocalizedName(), Abbrev = cat.Abbreviation(),
                Brush = ThemeManager.CategoryFillBrush(cat), Providers = provResults
            });
        }
        foreach (var g in groups) Groups.Add(g);
        Loading = false;
        RaiseAll();
        foreach (var g in Groups) foreach (var pr in g.Providers) pr.RaiseAll();
    }
}

// ===================== 命令面板 =====================

public sealed class PaletteToolVm
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Note { get; init; } = "";
    public string Glyph { get; init; } = "";
}

public sealed class CommandPaletteVm : ObservableObject
{
    private readonly MainVm _shell;
    public CommandPaletteVm(MainVm shell) { _shell = shell; }

    private string _search = "";
    public string Search { get => _search; set { if (Set(ref _search, value)) { SelectedIndex = 0; Raise(nameof(Filtered)); Raise(nameof(HasResults)); } } }
    public string Placeholder => L10n.T(L10n.Key.CmdPalettePlaceholder);
    public string NoMatchLabel => L10n.T(L10n.Key.NoMatchTool);
    public string FormatHint => SciToolbox.Core.DT.T("输入 工具:编号 可直接跳转，例如 uniprot:P12345", "Type tool:accession to jump straight in, e.g. uniprot:P12345");

    private int _selectedIndex;
    public int SelectedIndex { get => _selectedIndex; set => Set(ref _selectedIndex, value); }

    private string? QueryPart
    {
        get
        {
            var t = _search.Trim();
            int colon = t.IndexOf(':');
            if (colon >= 0) { var q = t.Substring(colon + 1).Trim(); return q.Length == 0 ? null : q; }
            int at = t.LastIndexOf('@');
            if (at >= 0) { var q = t.Substring(0, at).Trim(); return q.Length == 0 ? null : q; }
            return null;
        }
    }

    private string FilterText
    {
        get
        {
            var t = _search.Trim();
            int colon = t.IndexOf(':');
            if (colon >= 0) return t.Substring(0, colon).Trim();
            int at = t.LastIndexOf('@');
            if (at >= 0) return t.Substring(at + 1).Trim();
            return t;
        }
    }

    public List<PaletteToolVm> Filtered
    {
        get
        {
            var key = FilterText;
            var all = ToolRegistry.Shared.Providers.Where(p =>
                key.Length == 0 ||
                p.Name.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                p.DataSourceNote.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Contains(key, StringComparison.OrdinalIgnoreCase));
            return all.Select(p => new PaletteToolVm { Id = p.Id, Name = p.Name, Note = p.DataSourceNote, Glyph = ThemeManager.GlyphFor(p.IconName) }).ToList();
        }
    }

    public bool HasResults => Filtered.Count > 0;

    public void Accept()
    {
        var list = Filtered;
        if (list.Count == 0) return;
        var pick = list[Math.Clamp(SelectedIndex, 0, list.Count - 1)];
        _shell.DismissPalette();
        _shell.NavigateFromPalette(pick.Id, QueryPart);
    }

    public void MoveUp() { if (SelectedIndex > 0) SelectedIndex--; }
    public void MoveDown() { if (SelectedIndex < Filtered.Count - 1) SelectedIndex++; }
}

// ===================== 加入集合弹窗 =====================

public sealed class CollectionPickVm
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsNew { get; init; }
}

public sealed class AddToCollectionVm : ObservableObject
{
    private readonly MainVm _shell;
    public AddToCollectionVm(MainVm shell) { _shell = shell; ConfirmCommand = new RelayCommand(Confirm); }

    public ObservableCollection<CollectionPickVm> Options { get; } = new();
    public string Title => L10n.T(L10n.Key.AddToCollectionTitle);
    public string Hint { get; private set; } = "";
    public string NewLabel => L10n.T(L10n.Key.CreateAndAdd);
    public string NoCollectionsHint => L10n.T(L10n.Key.NoCollectionsHint);

    private int _selectedIndex = -1;
    public int SelectedIndex { get => _selectedIndex; set { if (Set(ref _selectedIndex, value)) { Raise(nameof(IsNewSelected)); Raise(nameof(NewNameEnabled)); } } }

    private string _newName = "";
    public string NewName { get => _newName; set => Set(ref _newName, value); }

    /// <summary>只有选中"新建集合"时姓名输入框才可用，否则会被误以为能改已有集合名。</summary>
    public bool IsNewSelected => _selectedIndex >= 0 && _selectedIndex < Options.Count && Options[_selectedIndex].IsNew;
    public bool NewNameEnabled => IsNewSelected;
    public string NamePlaceholder => L10n.T(L10n.Key.NamePlaceholder);
    public string CancelLabel => L10n.T(L10n.Key.CancelBtn);
    public bool ShowNoCollections => Options.Count <= 1;

    public ICommand ConfirmCommand { get; }

    public List<CollectionCandidate> Candidates { get; set; } = new();

    public void Populate()
    {
        Options.Clear();
        foreach (var c in CollectionStore.Shared.Collections)
            Options.Add(new CollectionPickVm { Id = c.Id, Name = c.Name });
        Options.Add(new CollectionPickVm { Id = "__new__", Name = L10n.T(L10n.Key.NewCollectionName), IsNew = true });
        Hint = L10n.T(L10n.Key.AddToCollectionHint, Candidates.Count);
        SelectedIndex = Options.Count > 1 ? 0 : Options.Count - 1;
        RaiseAll();
    }

    private void Confirm()
    {
        var chosen = SelectedIndex >= 0 && SelectedIndex < Options.Count ? Options[SelectedIndex] : null;
        if (chosen == null) return;
        string collectionId;
        if (chosen.IsNew)
        {
            var created = CollectionStore.Shared.Create(string.IsNullOrWhiteSpace(NewName) ? L10n.T(L10n.Key.UnnamedCollection) : NewName);
            collectionId = created.Id;
        }
        else collectionId = chosen.Id;

        int added = CollectionStore.Shared.AddEntries(Candidates.Select(c => c.ToEntry()).ToList(), collectionId);
        _shell.DismissAddCollection();
        if (added > 0) Toast.Show(L10n.T(L10n.Key.AddedEntities, added));
    }
}

// ===================== 备注编辑弹窗 =====================

public sealed class NoteEditorVm : ObservableObject
{
    private readonly MainVm _shell;
    public NoteEditorVm(MainVm shell) { _shell = shell; SaveCommand = new RelayCommand(Save); CancelCommand = new RelayCommand(() => shell.DismissNoteEditor()); }

    private string _text = "";
    public string Text { get => _text; set => Set(ref _text, value); }
    public string Title => L10n.T(L10n.Key.EditNote);
    public string ShortcutHint => SciToolbox.Core.DT.T("Ctrl+Enter 保存，Esc 取消", "Ctrl+Enter saves, Esc cancels");
    public string Placeholder => L10n.T(L10n.Key.NotePlaceholder);
    public string SaveLabel => L10n.T(L10n.Key.Save);
    public string CancelLabel => L10n.T(L10n.Key.CancelBtn);

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public Action<string?>? OnSave;

    /// <summary>备注框支持 Ctrl+Enter 保存，多行输入时不必再把手移回按钮。</summary>
    public bool SaveShortcut()
    {
        if (Text.Trim().Length == 0 && OnSave == null) return false;
        Save();
        return true;
    }

    private void Save()
    {
        var trimmed = Text.Trim();
        OnSave?.Invoke(trimmed.Length == 0 ? null : trimmed);
        _shell.DismissNoteEditor();
    }
}
