using System.Windows;
using System.Windows.Input;
using SciToolbox.Core;
using SciToolbox.Views;

namespace SciToolbox.ViewModels;

public sealed partial class MainVm
{
    // ===== 历史 =====
    public void ReRunHistory(HistoryEntry e)
    {
        var provider = _registry.Find(e.ToolId);
        if (provider == null) return;
        if (SelectedToolId == e.ToolId && SearchVmInstance != null)
            SearchVmInstance.Prefill(e.Query, false);
        else
        {
            PushNav(SelectedToolId);
            NavigateTo(e.ToolId, e.Query, false);
        }
    }

    public void DeleteHistory(HistoryEntry e)
    {
        var removed = SearchHistory.Shared.Remove(e.Id);
        if (removed != null)
            Toast.ShowUndoableToast(L10n.T(L10n.Key.DeletedHistory), () => SearchHistory.Shared.Restore(removed));
    }

    public void ClearHistory()
    {
        int n = SearchHistory.Shared.Entries.Count;
        if (n == 0) return;
        if (Confirm(L10n.T(L10n.Key.ConfirmClearHistoryMsg, n) + "\n" + L10n.T(L10n.Key.ConfirmClearHistoryMsg2)))
        {
            SearchHistory.Shared.ClearAll();
            Toast.Show(L10n.T(L10n.Key.ClearedHistory));
        }
    }

    // ===== 收藏 =====
    public void OpenFavorite(FavoriteItem f)
    {
        var provider = _registry.Find(f.ToolId);
        if (provider == null) { Toast.Show(L10n.T(L10n.Key.ToolNotFound)); return; }
        FetchDetail(f.ItemId, provider, new Dictionary<string, string> { [DetailContextKey(f.ToolId)] = f.ItemId }, true);
    }

    private static string DetailContextKey(string toolId) => toolId switch
    {
        "uniprot" => "accession",
        "pubmed" => "pmid",
        "pdb" => "id",
        "ncbi_taxonomy" => "taxid",
        "ncbi_gene" => "uid",
        "ensembl" => "id",
        "go" => "goId",
        "pfam" => "acc",
        "bacdive" => "bacdiveId",
        "mgnify" => "accession",
        "gbif" => "key",
        "gtdb_official" => "taxon",
        "kegg" => "entry",
        _ => "id"
    };

    public void DeleteFavorite(FavoriteItem f)
    {
        var removed = LocalFavorites.Shared.Remove(f.Id);
        if (removed != null)
            Toast.ShowUndoableToast(L10n.T(L10n.Key.DeletedFavorite), () => LocalFavorites.Shared.Restore(removed));
    }

    public void EditFavoriteNote(FavoriteItem f)
    {
        NoteEditorVmInstance.Text = f.Note ?? "";
        NoteEditorVmInstance.OnSave = note =>
        {
            LocalFavorites.Shared.UpdateNote(f.Id, note);
            Toast.Show(L10n.T(L10n.Key.NoteUpdated));
        };
        ShowNoteEditor = true;
    }

    public void ClearFavorites()
    {
        int n = LocalFavorites.Shared.Favorites.Count;
        if (n == 0) return;
        if (Confirm(L10n.T(L10n.Key.ConfirmClearFavoritesMsg, n) + "\n" + L10n.T(L10n.Key.ConfirmClearFavoritesMsg2)))
        {
            LocalFavorites.Shared.ClearAll();
            Toast.Show(L10n.T(L10n.Key.ClearedFavorites));
        }
    }

    public void ExportFavoritesText()
    {
        if (LocalFavorites.Shared.Favorites.Count == 0) return;
        if (ExportUtil.SaveTextFile(LocalFavorites.Shared.ExportAsText(), L10n.T(L10n.Key.FavoritesFilename), "txt"))
            Toast.Show(L10n.T(L10n.Key.Exported, ".txt"));
    }

    public void ExportFavoritesJson()
    {
        if (LocalFavorites.Shared.Favorites.Count == 0) return;
        if (ExportUtil.SaveTextFile(LocalFavorites.Shared.ExportAsJson(), L10n.T(L10n.Key.FavoritesFilename), "json"))
            Toast.Show(L10n.T(L10n.Key.Exported, ".json"));
    }

    // ===== 集合 =====
    public void CreateCollection()
    {
        var name = InputDialog.Prompt(Application.Current?.MainWindow, L10n.T(L10n.Key.NewCollection), L10n.T(L10n.Key.NamePrompt), "");
        if (name == null) return;
        CollectionStore.Shared.Create(name);
    }

    public void DeleteCollection(string id)
    {
        var c = CollectionStore.Shared.Find(id);
        if (c == null) return;
        if (!Confirm(L10n.T(L10n.Key.ConfirmDeleteCollection, c.Name, c.Entries.Count) + "\n" + L10n.T(L10n.Key.DeleteCollectionMsg))) return;
        var removed = CollectionStore.Shared.Delete(id);
        if (removed != null)
            Toast.ShowUndoableToast(L10n.T(L10n.Key.DeletedCollection), () => CollectionStore.Shared.RestoreCollection(removed));
    }

    public void RenameCollection(string id)
    {
        var c = CollectionStore.Shared.Find(id);
        if (c == null) return;
        var name = InputDialog.Prompt(Application.Current?.MainWindow, L10n.T(L10n.Key.RenameCollection), L10n.T(L10n.Key.NamePrompt), c.Name);
        if (name != null) CollectionStore.Shared.Rename(id, name);
    }

    public void OpenCollectionEntry(CollectionEntry entry)
    {
        var provider = _registry.Find(entry.ToolId);
        if (provider == null) return;
        FetchDetail(entry.ItemId, provider, entry.Context, true);
    }

    public void RemoveCollectionEntry(string collectionId, CollectionEntry entry)
    {
        var removed = CollectionStore.Shared.RemoveEntry(collectionId, entry.Id);
        if (removed != null)
            Toast.ShowUndoableToast(L10n.T(L10n.Key.RemovedFromCollection), () => CollectionStore.Shared.RestoreEntry(removed, collectionId));
    }

    public void EditCollectionEntryNote(string collectionId, CollectionEntry entry)
    {
        NoteEditorVmInstance.Text = entry.Note ?? "";
        NoteEditorVmInstance.OnSave = note =>
        {
            CollectionStore.Shared.UpdateEntryNote(collectionId, entry.Id, note);
            Toast.Show(L10n.T(L10n.Key.NoteUpdated));
        };
        ShowNoteEditor = true;
    }

    public void ExportCollectionCsv(string id)
    {
        var c = CollectionStore.Shared.Find(id);
        if (c == null) return;
        var header = new List<string> { L10n.T(L10n.Key.CsvAccession), L10n.T(L10n.Key.CsvTitle), L10n.T(L10n.Key.CsvSubtitle), L10n.T(L10n.Key.CsvSource) };
        var rows = c.Entries.Select(e => new List<string> { e.ItemId, e.Title, e.Subtitle ?? "", e.ToolName }).ToList();
        var csv = ExportUtil.BuildCsv(header, rows);
        if (ExportUtil.SaveTextFile(csv, c.Name, "csv"))
            Toast.Show(L10n.T(L10n.Key.Exported, c.Name + ".csv"));
    }

    public void ExportCollectionsJson()
    {
        if (CollectionStore.Shared.Collections.Count == 0) return;
        if (ExportUtil.SaveTextFile(CollectionStore.Shared.ExportAsJson(), L10n.T(L10n.Key.Collections), "json"))
            Toast.Show(L10n.T(L10n.Key.Exported, ".json"));
    }

    public async Task BatchVerify(string id, Action<int>? onProgress = null)
    {
        var c = CollectionStore.Shared.Find(id);
        if (c == null || c.Entries.Count == 0) { Toast.Show(L10n.T(L10n.Key.CompareNoEntries)); return; }
        int ok = 0, fail = 0, done = 0;
        foreach (var entry in c.Entries)
        {
            var provider = _registry.Find(entry.ToolId);
            if (provider == null) { fail++; }
            else
            {
                try { await provider.DetailAsync(entry.ItemId, entry.Context); ok++; }
                catch { fail++; }
            }
            onProgress?.Invoke(++done);
        }
        Toast.Show(L10n.T(L10n.Key.BatchVerifyComplete) + $" ({ok}/{c.Entries.Count})"
            + (fail > 0 ? " · " + L10n.T(L10n.Key.Failed) + " " + fail : ""));
    }

    // ===== 演示模式：确定性导航 + 检索 + 自动打开首条详情 =====
    public void StartDemo(string toolId, string query)
    {
        OpenToolById(toolId);
        SearchVmInstance?.Prefill(query, autoOpen: true);
    }

    // ===== 跨库跳转（工具 + 查询） =====
    public void OpenToolWithQuery(string toolId, string query)
    {
        var provider = _registry.Find(toolId);
        if (provider == null) return;
        PushNav(SelectedToolId);
        NavigateTo(toolId, query, false);
    }

    // ===== 命令面板 =====
    private CommandPaletteVm? _palette;
    public CommandPaletteVm PaletteVmInstance => _palette ??= new CommandPaletteVm(this);

    public void DismissPalette() => ShowCommandPalette = false;
    public void NavigateFromPalette(string toolId, string? query)
    {
        var provider = _registry.Find(toolId);
        if (provider == null) return;
        if (SelectedToolId == toolId && SearchVmInstance != null && !string.IsNullOrEmpty(query))
            SearchVmInstance.Prefill(query!, false);
        else
        {
            PushNav(SelectedToolId);
            NavigateTo(toolId, query, false);
        }
    }

    // ===== 加入集合弹窗 =====
    private AddToCollectionVm? _addCollection;
    public AddToCollectionVm AddCollectionVmInstance => _addCollection ??= new AddToCollectionVm(this);
    public void DismissAddCollection() => ShowAddToCollection = false;

    // 批量/单条 加入集合：填充弹窗 VM 后展示
    public void AddResultsToCollection(List<CollectionCandidate> candidates)
    {
        if (candidates.Count == 0) return;
        CollectionCandidates = candidates;
        AddCollectionVmInstance.Candidates = candidates;
        AddCollectionVmInstance.Populate();
        ShowAddToCollection = true;
    }

    // ===== 备注编辑弹窗 =====
    private NoteEditorVm? _noteEditor;
    public NoteEditorVm NoteEditorVmInstance => _noteEditor ??= new NoteEditorVm(this);
    private bool _showNoteEditor;
    public bool ShowNoteEditor { get => _showNoteEditor; set { Set(ref _showNoteEditor, value); Raise(nameof(NoteEditorVisible)); Raise(nameof(AnyOverlayVisible)); } }
    public bool NoteEditorVisible => _showNoteEditor;
    public void DismissNoteEditor() => ShowNoteEditor = false;

    // ===== 智能路由候选选择 =====
    public void ChooseRouteAt(int index)
    {
        if (index >= 0 && index < RouteMatches.Count) ChooseRoute(RouteMatches[index]);
    }

    // ===== 语言/主题刷新 =====
    /// <summary>
    /// 语言 / 主题切换后刷新界面文案。
    /// 替换 Content 对象并不会让已建立的绑定重新求值，
    /// 因此需要逐个 VM 通知全部属性变更。
    /// </summary>
    public void ReloadTexts()
    {
        RebuildSidebar();
        foreach (var it in SidebarItems) it.IsSelected = !it.IsHeader && it.Id == SelectedToolId;

        Raise(nameof(ContentTitle));
        Raise(nameof(ContentSubtitle));
        RaiseAll();

        HomeVmInstance.RaiseAll();
        HomeVmInstance.RebuildForLanguage();
        FavoritesVmInstance.RaiseAll();
        FavoritesVmInstance.Rebuild();
        HistoryVmInstance.RaiseAll();
        HistoryVmInstance.Rebuild();
        CollectionsVmInstance.RaiseAll();
        CollectionsVmInstance.Rebuild();
        SettingsVmInstance.RaiseAll();
        GlobalVmInstance.RaiseAll();
        GlobalVmInstance.RebuildGroupTexts();
        SearchVmInstance?.RaiseAll();
        _currentDetail?.RaiseAll();
        PaletteVmInstance.RaiseAll();
        NoteEditorVmInstance.RaiseAll();
        AddCollectionVmInstance.RaiseAll();
    }


    // ===== 数据管理：导出全部 / 重置 =====
    public void ExportAllData()
    {
        var payload = new
        {
            exportedAt = DateTime.Now,
            favorites = LocalFavorites.Shared.Favorites,
            history = SearchHistory.Shared.Entries,
            collections = CollectionStore.Shared.Collections
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        if (ExportUtil.SaveTextFile(json, L10n.T(L10n.Key.AllDataFilename), "json"))
            Toast.Show(L10n.T(L10n.Key.AllDataExported));
    }

    public void ResetApp()
    {
        if (!Confirm(L10n.T(L10n.Key.ConfirmResetAll) + "\n" + L10n.T(L10n.Key.ResetAllMessage))) return;
        LocalFavorites.Shared.ClearAll();
        SearchHistory.Shared.ClearAll();
        CollectionStore.Shared.ClearAll();
        ResponseCache.Shared.Clear();
        Toast.Show(L10n.T(L10n.Key.DataReset));
    }

    // ===== 通用确认框 =====
    public bool ConfirmClear(string message) => Confirm(message);

    private static bool Confirm(string message)
    {
        var r = MessageBox.Show(Application.Current?.MainWindow, message, L10n.T(L10n.Key.Hint),
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        return r == MessageBoxResult.OK;
    }
}
