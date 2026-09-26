using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SciToolbox.Core;

namespace SciToolbox.ViewModels;

// ===================== 首页 =====================

public sealed class ToolTileVm
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Glyph { get; init; } = "";
    public System.Windows.Media.Brush Brush { get; init; } = null!;
    public ICommand SelectCommand { get; init; } = null!;
}

public sealed class CategoryGroupVm
{
    public string Name { get; init; } = "";
    public string Abbrev { get; init; } = "";
    public System.Windows.Media.Brush Brush { get; init; } = null!;
    public List<ToolTileVm> Tools { get; init; } = new();
}

public sealed class HomeVm : ObservableObject
{
    private readonly MainVm _shell;

    public HomeVm(MainVm shell)
    {
        _shell = shell;
        IdentifyCommand = new RelayCommand(() => { if (SmartIdentifyText.Trim().Length > 0) _shell.RouteAccession(SmartIdentifyText.Trim()); });
        GlobalSearchCommand = new RelayCommand(() => { if (GlobalQuery.Trim().Length > 0) _shell.OpenGlobalSearch(GlobalQuery.Trim()); });
        OpenFavoritesCommand = new RelayCommand(() => _shell.OpenToolById("favorites"));
        Rebuild();
        SearchHistory.Shared.Changed += () => System.Windows.Application.Current.Dispatcher.Invoke(Rebuild);
        LocalFavorites.Shared.Changed += () => System.Windows.Application.Current.Dispatcher.Invoke(Rebuild);
    }

    private string _smartIdentifyText = "";
    public string SmartIdentifyText { get => _smartIdentifyText; set => Set(ref _smartIdentifyText, value); }
    public string SmartFieldPlaceholder => L10n.T(L10n.Key.SmartFieldPlaceholder);
    public string IdentifyLabel => L10n.T(L10n.Key.IdentifyAndOpen);
    public string GlobalSearchLabel => L10n.T(L10n.Key.GlobalSearch);
    public string GlobalSearchDesc => L10n.T(L10n.Key.GlobalSearchDesc, ToolRegistry.Shared.Providers.Count);
    public string Subtitle => L10n.T(L10n.Key.HomeSubtitle, ToolRegistry.Shared.Providers.Count);
    public string QuickStart => L10n.T(L10n.Key.QuickStart);
    public string RecentLabel => L10n.T(L10n.Key.RecentQueries);
    public string BrowseLabel => L10n.T(L10n.Key.BrowseCategories);
    public string FavoritesLabel => L10n.T(L10n.Key.FavoritesLabel);
    public string SmartIdentifyDesc => L10n.T(L10n.Key.SmartIdentifyDesc);
    public string XlinkDesc => L10n.T(L10n.Key.XlinkDesc);
    public string CmdKDesc => L10n.T(L10n.Key.CmdKDesc);
    public string CollectionsDesc => L10n.T(L10n.Key.CollectionsDesc);

    private string _globalQuery = "";
    public string GlobalQuery { get => _globalQuery; set => Set(ref _globalQuery, value); }

    public ICommand IdentifyCommand { get; }
    public ICommand GlobalSearchCommand { get; }
    public ICommand OpenFavoritesCommand { get; }

    public List<CategoryGroupVm> Categories { get; private set; } = new();
    public List<HistoryEntry> RecentQueries { get; private set; } = new();
    public int FavoritesCount => LocalFavorites.Shared.Favorites.Count;
    public bool HasFavorites => FavoritesCount > 0;
    public bool HasRecent => RecentQueries.Count > 0;

    public ICommand OpenRecentCommand => new RelayCommand<HistoryEntry>(e => { if (e != null) _shell.ReRunHistory(e); });

    /// <summary>语言切换后重建首页：分类名与最近查询都含本地化文本。</summary>
    public void RebuildForLanguage() { Rebuild(); RaiseAll(); }

    private void Rebuild()
    {
        Categories = ToolRegistry.Shared.Grouped().Select(g => new CategoryGroupVm
        {
            Name = g.category.LocalizedName(),
            Abbrev = g.category.Abbreviation(),
            Brush = ThemeManager.CategoryFillBrush(g.category),
            Tools = g.items.Select(p => new ToolTileVm
            {
                Id = p.Id, Name = p.Name, Glyph = ThemeManager.GlyphFor(p.IconName),
                Brush = ThemeManager.CategoryBrush(g.category),
                SelectCommand = new RelayCommand(() => _shell.OpenToolById(p.Id))
            }).ToList()
        }).ToList();
        RecentQueries = SearchHistory.Shared.Entries.Take(8).ToList();
        Raise(nameof(Categories));
        Raise(nameof(RecentQueries));
        Raise(nameof(HasRecent));
        Raise(nameof(FavoritesCount));
        Raise(nameof(HasFavorites));
    }
}

// ===================== 收藏夹 =====================

public sealed class FavoriteRowVm : ObservableObject, SciToolbox.Views.IActivatableRow
{
    public FavoriteItem Item { get; }
    public string Title => Item.Title;
    public string? Subtitle => Item.Subtitle;
    public string ToolName => Item.ToolName;
    public string? Note => Item.Note;
    public bool HasNote => !string.IsNullOrEmpty(Item.Note);
    public bool HasSubtitle => !string.IsNullOrEmpty(Item.Subtitle);
    public string RelativeTime => Item.RelativeTime;
    public System.Windows.Media.Brush Brush { get; }
    public string OpenTip => SciToolbox.Core.DT.T("打开", "Open");
    public string DeleteTip => SciToolbox.Core.DT.T("删除这条收藏", "Delete this favorite");
    public string EditNoteTip => L10n.T(L10n.Key.EditNote);
    public ICommand OpenCommand { get; }
    public ICommand? ActivateCommand => OpenCommand;
    public ICommand DeleteCommand { get; }
    public ICommand EditNoteCommand { get; }

    public FavoriteRowVm(FavoriteItem item, MainVm shell)
    {
        Item = item;
        var provider = ToolRegistry.Shared.Find(item.ToolId);
        Brush = provider != null ? ThemeManager.CategoryFillBrush(provider.Category) : ThemeManager.AccentFillBrush;
        OpenCommand = new RelayCommand(() => shell.OpenFavorite(item));
        DeleteCommand = new RelayCommand(() => shell.DeleteFavorite(item));
        EditNoteCommand = new RelayCommand(() => shell.EditFavoriteNote(item));
    }
}

public sealed class FavoritesVm : ObservableObject
{
    private readonly MainVm _shell;
    public FavoritesVm(MainVm shell)
    {
        _shell = shell;
        ClearFilterCommand = new RelayCommand(() => Filter = "");
        ClearCommand = new RelayCommand(shell.ClearFavorites);
        ExportTextCommand = new RelayCommand(shell.ExportFavoritesText);
        ExportJsonCommand = new RelayCommand(shell.ExportFavoritesJson);
        Rebuild();
        LocalFavorites.Shared.Changed += () => System.Windows.Application.Current.Dispatcher.Invoke(Rebuild);
    }

    public ObservableCollection<FavoriteRowVm> Rows { get; } = new();
    public string Title => L10n.T(L10n.Key.FavoritesTitle);
    public string Empty => L10n.T(L10n.Key.FavoritesEmpty);
    public string FilterPlaceholder => L10n.T(L10n.Key.FavoritesFilterPlaceholder);
    public string NoFilterText => L10n.T(L10n.Key.NoFilterMatchFavorites, Filter);
    public ICommand ClearFilterCommand { get; }
    public bool HasFilter => !string.IsNullOrWhiteSpace(_filter);
    public bool ShowNoFilter => Rows.Count == 0 && HasFilter;
    public bool ShowTrueEmpty => Rows.Count == 0 && !HasFilter;
    public bool HasItems => Rows.Count > 0;

    private string _filter = "";
    public string Filter { get => _filter; set { if (Set(ref _filter, value)) Rebuild(); } }

    public string ExportLabel => L10n.T(L10n.Key.ExportAsText);
    public string ExportJsonLabel => L10n.T(L10n.Key.ExportAsJSON);
    public string ClearLabel => L10n.T(L10n.Key.ClearAll);
    public ICommand ClearCommand { get; }
    public ICommand ExportTextCommand { get; }
    public ICommand ExportJsonCommand { get; }

    public void Rebuild()
    {
        Rows.Clear();
        var q = _filter.Trim();
        foreach (var f in LocalFavorites.Shared.Favorites)
        {
            if (q.Length > 0 &&
                !f.Title.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !f.ToolName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !(f.Note?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                continue;
            Rows.Add(new FavoriteRowVm(f, _shell));
        }
        RaiseAll();
    }
}

// ===================== 历史 =====================

public sealed class HistoryRowVm : SciToolbox.Views.IActivatableRow
{
    public HistoryEntry Item { get; }
    public string Query => Item.Query;
    public string ToolName => Item.ToolName;
    public string RelativeTime => Item.RelativeTime;
    public System.Windows.Media.Brush Brush { get; }
    public ICommand ReRunCommand { get; }
    public string DeleteTip => SciToolbox.Core.DT.T("删除这条记录", "Delete this entry");
    public System.Windows.Input.ICommand? ActivateCommand => ReRunCommand;
    public ICommand DeleteCommand { get; }

    public HistoryRowVm(HistoryEntry item, MainVm shell)
    {
        Item = item;
        var provider = ToolRegistry.Shared.Find(item.ToolId);
        Brush = provider != null ? ThemeManager.CategoryFillBrush(provider.Category) : ThemeManager.AccentFillBrush;
        ReRunCommand = new RelayCommand(() => shell.ReRunHistory(item));
        DeleteCommand = new RelayCommand(() => shell.DeleteHistory(item));
    }
}

public sealed class HistoryVm : ObservableObject
{
    private readonly MainVm _shell;
    public HistoryVm(MainVm shell)
    {
        _shell = shell;
        ClearFilterCommand = new RelayCommand(() => Filter = "");
        ClearCommand = new RelayCommand(shell.ClearHistory);
        Rebuild();
        SearchHistory.Shared.Changed += () => System.Windows.Application.Current.Dispatcher.Invoke(Rebuild);
    }

    public ObservableCollection<HistoryRowVm> Rows { get; } = new();
    public string Title => L10n.T(L10n.Key.HistoryTitle);
    public string Empty => L10n.T(L10n.Key.HistoryEmpty);
    public string ReSearchLabel => L10n.T(L10n.Key.ReSearch);
    public string ClearLabel => L10n.T(L10n.Key.ClearAll);
    public string FilterPlaceholder => L10n.T(L10n.Key.HistoryFilterPlaceholder);
    public string NoFilterText => L10n.T(L10n.Key.NoFilterMatchHistory, Filter);
    public ICommand ClearFilterCommand { get; }
    public bool HasFilter => !string.IsNullOrWhiteSpace(_filter);
    public bool ShowNoFilter => Rows.Count == 0 && HasFilter;
    public bool ShowTrueEmpty => Rows.Count == 0 && !HasFilter;
    public bool HasItems => Rows.Count > 0;

    private string _filter = "";
    public string Filter { get => _filter; set { if (Set(ref _filter, value)) Rebuild(); } }

    public ICommand ClearCommand { get; }

    public void Rebuild()
    {
        Rows.Clear();
        var q = _filter.Trim();
        foreach (var h in SearchHistory.Shared.Entries)
        {
            if (q.Length > 0 &&
                !h.Query.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !h.ToolName.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;
            Rows.Add(new HistoryRowVm(h, _shell));
        }
        RaiseAll();
    }
}
