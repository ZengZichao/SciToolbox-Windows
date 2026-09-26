using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SciToolbox.Core;

namespace SciToolbox.ViewModels;

/// <summary>搜索结果行显示封装。</summary>
public sealed class ResultRowVm : ObservableObject, SciToolbox.Views.IActivatableRow, SciToolbox.Views.IMultiSelectRow
{
    public ResultItem Item { get; }
    public string Title => Item.Title;
    public string? Subtitle => Item.Subtitle;
    public string? Badge => Item.Badge;
    public string? Meta => Item.Meta;
    public bool HasSubtitle => !string.IsNullOrEmpty(Item.Subtitle);
    public bool HasMeta => !string.IsNullOrEmpty(Item.Meta);
    public bool HasBadge => !string.IsNullOrEmpty(Item.Badge);

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { if (Set(ref _isSelected, value)) _onSelectChanged(); } }

    private readonly Action _onSelectChanged;

    private bool _isFavorited;
    public bool IsFavorited { get => _isFavorited; set { if (Set(ref _isFavorited, value)) Raise(nameof(FavoriteGlyph)); } }

    public string FavoriteGlyph => UiIcon.StarGlyph(_isFavorited);
    public string FavoriteTip => _isFavorited ? L10n.T(L10n.Key.Unfavorite) : L10n.T(L10n.Key.Favorite);
    public ICommand OpenCommand { get; }
    public ICommand? ActivateCommand => OpenCommand;
    public ICommand FavoriteCommand { get; }

    public ResultRowVm(ResultItem item, bool favorited, Action open, Action favorite, Action selectionChanged)
    {
        Item = item; _isFavorited = favorited; _onSelectChanged = selectionChanged;
        OpenCommand = new RelayCommand(open);
        FavoriteCommand = new RelayCommand(favorite);
    }
}

public enum SearchState { Home, Loading, Results, Error }

/// <summary>搜索中栏视图模型（对应 SearchScreen）。</summary>
public sealed class SearchVm : ObservableObject
{
    private readonly IToolProvider _provider;
    private readonly MainVm _shell;
    private CancellationTokenSource? _searchCts;

    public SearchVm(IToolProvider provider, MainVm shell)
    {
        _provider = provider;
        _shell = shell;

        PickerOptions = provider.PickerOptions ?? new List<PickerOption>();
        _selectedPickerId = provider.DefaultPickerId ?? PickerOptions.FirstOrDefault()?.Id ?? "";

        SearchCommand = new AsyncRelayCommand(() => DoSearchAsync(Query));
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync, () => HasMore && !IsLoadingMore && !FilterActive);
        RetryCommand = new AsyncRelayCommand(() => DoSearchAsync(_lastQuery));
        OpenGlobalSearchCommand = new RelayCommand(
            () => { if (_lastQuery.Length > 0) _shell.OpenGlobalSearch(_lastQuery); },
            () => _lastQuery.Length > 0);
        ClearFilterCommand = new RelayCommand(() => { InResultFilter = ""; });
        EnterSelectCommand = new RelayCommand(() => { SelectionMode = true; });
        DoneSelectCommand = new RelayCommand(() => { SelectionMode = false; foreach (var r in Items) r.IsSelected = false; });
        ToggleSelectAllCommand = new RelayCommand(ToggleSelectAll);
        BatchCopyCommand = new RelayCommand(BatchCopy);
        BatchFavoriteCommand = new RelayCommand(BatchFavorite);
        BatchAddCollectionCommand = new RelayCommand(() => _shell.AddResultsToCollection(CurrentCandidates()));
        BatchExportCommand = new RelayCommand(BatchExport);
        IdentifyCommand = new RelayCommand(() =>
        {
            var t = Query.Trim();
            if (t.Length == 0) { Toast.Show(L10n.T(L10n.Key.EmptyQuery)); return; }
            _shell.RouteAccession(t);
        });
        SetPickerCommand = new RelayCommand<string?>(id => { if (id != null) SelectedPickerId = id; });
        ClearFilterCommand = new RelayCommand(() => InResultFilter = "");

        Items.CollectionChanged += (_, _) =>
        {
            Raise(nameof(HasAnyItems));
            Raise(nameof(DisplayItems));
            Raise(nameof(HasDisplayItems));
            Raise(nameof(ShowEmptyFilter));
            Raise(nameof(ShowEmptyNoResult));
            Raise(nameof(ShowSelectionBar));
            Raise(nameof(IsAllSelected));
            Raise(nameof(SelectAllLabel));
            Raise(nameof(SelectedCountLabel));
            Raise(nameof(ShowSelectionBar));
        };
    }

    public string IdentifyLabel => L10n.T(L10n.Key.Identify);
    public string SelectLabel => L10n.T(L10n.Key.Select);
    public string NoFilterMatchLabel => L10n.T(L10n.Key.NoFilterMatch, InResultFilter);
    public string RetryLabel => L10n.T(L10n.Key.Retry);
    public string DoneLabel => L10n.T(L10n.Key.Done);
    public string CopyIdsLabel => L10n.T(L10n.Key.CopyAccessions);
    public string BatchFavoriteLabel => L10n.T(L10n.Key.BatchFavorite);
    public string AddCollectionLabel => L10n.T(L10n.Key.AddToCollection);
    public string ExportLabel => L10n.T(L10n.Key.ExportTable);
    public string FilterPlaceholder => L10n.T(L10n.Key.LocalFilter, Items.Count);
    public string ScopeLabel => L10n.T(L10n.Key.SearchScope);
    public string EnterMultiSelectLabel => L10n.T(L10n.Key.EnterMultiSelect);
    public string GlobalSearchLabel => L10n.T(L10n.Key.SearchInAll, _lastQuery);
    public string SourceNoteLabel => _provider.DataSourceNote;
    public string HintLabel => L10n.T(L10n.Key.KeyboardHint);
    public string EmptyResultsLabel => FilterActive
        ? L10n.T(L10n.Key.NoFilterMatch, InResultFilter)
        : L10n.T(L10n.Key.NoResultMatch, _lastQuery);

    private ResultRowVm MakeRow(ResultItem item) =>
        new(item, LocalFavorites.Shared.IsFavorited(_provider.Id, item.Id),
            () => _shell.FetchDetail(item.Id, _provider, item.Extra, true),
            () =>
            {
                LocalFavorites.Shared.Toggle(_provider.Id, _provider.Name, item.Id, item.Title, item.Subtitle);
                IsFavoritedNow(item);
            },
            RaiseChange);

    private void IsFavoritedNow(ResultItem item)
    {
        var row = Items.FirstOrDefault(r => r.Item.Id == item.Id);
        if (row != null) row.IsFavorited = LocalFavorites.Shared.IsFavorited(_provider.Id, item.Id);
    }

    public IToolProvider Provider => _provider;
    public IReadOnlyList<PickerOption> PickerOptions { get; }
    public bool HasPicker => PickerOptions.Count > 0;
    public string Placeholder => _provider.Placeholder;
    public string DataSourceNote => _provider.DataSourceNote;
    public string Name => _provider.Name;
    public System.Windows.Media.Brush AccentBrush => ThemeManager.CategoryBrush(_provider.Category);

    private string _query = "";
    public string Query { get => _query; set { if (Set(ref _query, value)) _shell.OnToolQueryChanged(_provider.Id, value); } }

    private string _inResultFilter = "";
    public string InResultFilter { get => _inResultFilter; set { if (Set(ref _inResultFilter, value)) { Raise(nameof(DisplayItems)); Raise(nameof(FilterActive)); } } }

    private SearchState _state = SearchState.Home;
    public SearchState State { get => _state; set { Set(ref _state, value); Raise(nameof(IsHome)); Raise(nameof(IsLoading)); Raise(nameof(IsResults)); Raise(nameof(IsError)); UpdateSubtitle(); } }
    public bool IsHome => _state == SearchState.Home;
    public bool IsLoading => _state == SearchState.Loading;
    public bool IsResults => _state == SearchState.Results;
    public bool IsError => _state == SearchState.Error;

    private string? _error;
    public string? Error { get => _error; set { Set(ref _error, value); Raise(nameof(ErrorText)); } }
    public string ErrorText => _error ?? "";

    public ObservableCollection<ResultRowVm> Items { get; } = new();
    public SearchResult? LastResult { get; private set; }
    private string _lastQuery = "";

    public IEnumerable<ResultRowVm> DisplayItems =>
        FilterActive
            ? Items.Where(i =>
                i.Title.Contains(InResultFilter, StringComparison.OrdinalIgnoreCase) ||
                (i.Subtitle?.Contains(InResultFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Meta?.Contains(InResultFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Badge?.Contains(InResultFilter, StringComparison.OrdinalIgnoreCase) ?? false))
            : Items;

    public bool FilterActive => !string.IsNullOrWhiteSpace(InResultFilter);
    public bool HasAnyItems => Items.Count > 0;
    public bool HasDisplayItems => DisplayItems.Any();
    public bool ShowEmptyFilter => FilterActive && !HasDisplayItems;
    public bool ShowEmptyNoResult => !FilterActive && !HasAnyItems;

    private bool _isLoadingMore;
    public bool IsLoadingMore { get => _isLoadingMore; set { Set(ref _isLoadingMore, value); Raise(nameof(LoadMoreLabel)); } }
    public string LoadMoreLabel => _isLoadingMore ? L10n.T(L10n.Key.LoadingMore) : L10n.T(L10n.Key.LoadMore);
    private string? _loadMoreError;
    public string? LoadMoreError { get => _loadMoreError; set { Set(ref _loadMoreError, value); Raise(nameof(HasLoadMoreError)); } }
    public bool HasLoadMoreError => !string.IsNullOrEmpty(_loadMoreError);
    public bool HasMore => LastResult?.HasMore ?? false;
    public bool ShowLoadMore => HasMore && !FilterActive;

    private string _selectedPickerId;
    public string SelectedPickerId
    {
        get => _selectedPickerId;
        set
        {
            if (Set(ref _selectedPickerId, value) && !string.IsNullOrEmpty(_lastQuery))
                _ = DoSearchAsync(_lastQuery);
        }
    }

    public string Subtitle { get; private set; } = "";
    private void UpdateSubtitle()
    {
        if (_state == SearchState.Results && LastResult != null)
        {
            var r = LastResult;
            if (r.HasMore)
                Subtitle = r.Total.HasValue ? L10n.T(L10n.Key.ResultCountTotal, r.Items.Count, r.Total.Value)
                                            : L10n.T(L10n.Key.ResultLoaded, r.Items.Count);
            else
                Subtitle = L10n.T(L10n.Key.ResultCount, r.Total ?? r.Items.Count);
        }
        else Subtitle = "";
        Raise(nameof(Subtitle));
    }

    // 选择模式
    private bool _selectionMode;
    public bool SelectionMode { get => _selectionMode; set { Set(ref _selectionMode, value); Raise(nameof(ShowSelectionBar)); Raise(nameof(NotSelectionMode)); } }
    public bool ShowSelectionBar => _selectionMode && Items.Any(r => r.IsSelected);
    public bool NotSelectionMode => !_selectionMode;

    public string SelectedCountLabel => L10n.T(L10n.Key.Selected, Items.Count(r => r.IsSelected));
    public bool IsAllSelected => Items.Count > 0 && Items.All(r => r.IsSelected);
    public string SelectAllLabel => IsAllSelected ? L10n.T(L10n.Key.DeselectAll) : L10n.T(L10n.Key.SelectAll);

    public ICommand SearchCommand { get; }
    public ICommand IdentifyCommand { get; }
    public ICommand SetPickerCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand OpenGlobalSearchCommand { get; }
    public string NoResultGuidance => L10n.T(L10n.Key.NoResultGuidance);
    public ICommand ClearFilterCommand { get; }
    public string ClearFilterLabel => L10n.T(L10n.Key.ClearAndRestore);
    public ICommand EnterSelectCommand { get; }
    public ICommand DoneSelectCommand { get; }
    public ICommand ToggleSelectAllCommand { get; }
    public ICommand BatchCopyCommand { get; }
    public ICommand BatchFavoriteCommand { get; }
    public ICommand BatchAddCollectionCommand { get; }
    public ICommand BatchExportCommand { get; }

    public void Prefill(string q, bool autoOpen)
    {
        Query = q;
        _autoOpenDetail = autoOpen;
        _ = DoSearchAsync(q);
    }

    private bool _autoOpenDetail;

    public void FocusSearch() { /* handled by view via event */ }

    private async Task DoSearchAsync(string rawQuery)
    {
        var q = rawQuery.Trim();
        if (q.Length == 0)
        {
            Toast.Show(L10n.T(L10n.Key.EmptyQuery));
            return;
        }
        if (!NetworkMonitor.Shared.IsOnline)
        {
            Error = L10n.T(L10n.Key.Offline);
            State = SearchState.Error;
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        State = SearchState.Loading;
        Error = null;
        LoadMoreError = null;
        _lastQuery = q;
        InResultFilter = "";
        SelectionMode = false;

        try
        {
            var result = await _provider.SearchAsync(q, 0, string.IsNullOrEmpty(_selectedPickerId) ? null : _selectedPickerId, ct);
            if (ct.IsCancellationRequested) return;
            LastResult = result;
            Items.Clear();
            foreach (var item in result.Items)
                Items.Add(MakeRow(item));
            State = SearchState.Results;
            SearchHistory.Shared.Add(_provider.Id, _provider.Name, q);
            if (_autoOpenDetail)
            {
                _autoOpenDetail = false;
                if (result.Items.Count > 0) _shell.OpenDetail(result.Items[0], _provider);
            }
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex)
        {
            if (ct.IsCancellationRequested) return;
            Error = ex.FriendlyMessage;
            State = SearchState.Error;
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            Error = ex.Message;
            State = SearchState.Error;
        }
    }

    public async Task SeedGtdbAsync()
    {
        if (_provider.Id != "gtdb_official") return;
        State = SearchState.Loading;
        try
        {
            var result = await _provider.SearchAsync("", 0, null);
            LastResult = result;
            Items.Clear();
            foreach (var item in result.Items)
                Items.Add(MakeRow(item));
            State = SearchState.Results;
        }
        catch (ApiException ex) { Error = ex.FriendlyMessage; State = SearchState.Error; }
    }

    private async Task LoadMoreAsync()
    {
        if (IsLoadingMore || !(LastResult?.HasMore ?? false)) return;
        IsLoadingMore = true;
        LoadMoreError = null;
        int nextOffset = LastResult!.Items.Count;
        try
        {
            var more = await _provider.SearchAsync(_lastQuery, nextOffset, string.IsNullOrEmpty(_selectedPickerId) ? null : _selectedPickerId);
            LastResult.Items.AddRange(more.Items);
            LastResult.HasMore = more.HasMore;
            LastResult.Total = more.Total ?? LastResult.Total;
            foreach (var item in more.Items)
                Items.Add(MakeRow(item));
        }
        catch (ApiException ex) { LoadMoreError = ex.FriendlyMessage; }
        catch (Exception ex) { LoadMoreError = ex.Message; }
        finally { IsLoadingMore = false; Raise(nameof(HasMore)); Raise(nameof(ShowLoadMore)); }
    }

    public ResultRowVm? RowOf(ResultItem item) => Items.FirstOrDefault(r => r.Item.Id == item.Id);

    public List<CollectionCandidate> CurrentCandidates() =>
        Items.Where(r => r.IsSelected).Select(r => new CollectionCandidate
        {
            ToolId = _provider.Id, ToolName = _provider.Name, ItemId = r.Item.Id,
            Title = r.Item.Title, Subtitle = r.Item.Subtitle, Context = r.Item.Extra
        }).ToList();

    public void SingleCandidate(ResultItem item) => _shell.AddResultsToCollection(new List<CollectionCandidate>
    {
        new() { ToolId = _provider.Id, ToolName = _provider.Name, ItemId = item.Id, Title = item.Title, Subtitle = item.Subtitle, Context = item.Extra }
    });

    public void AddToCompare(ResultItem item) => ComparisonStore.Shared.Add(
        _provider.Id, _provider.Name, _provider.Category, item.Id, item.Title, item.Extra);

    private void ToggleSelectAll()
    {
        bool target = !IsAllSelected;
        foreach (var r in Items) r.IsSelected = target;
        RaiseChange();
    }

    private void BatchCopy()
    {
        var lines = Items.Where(r => r.IsSelected).Select(r => r.Item.Badge ?? r.Item.Id).ToList();
        if (lines.Count == 0) return;
        ClipboardUtil.Copy(string.Join("\n", lines), L10n.T(L10n.Key.Copied));
    }

    private void BatchFavorite()
    {
        var selected = Items.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0) return;
        foreach (var r in selected)
        {
            LocalFavorites.Shared.Add(_provider.Id, _provider.Name, r.Item.Id, r.Item.Title, r.Item.Subtitle);
            r.IsFavorited = true;
        }
        Toast.Show(L10n.T(L10n.Key.BatchFavorited, selected.Count));
    }

    private void BatchExport()
    {
        var selected = Items.Where(r => r.IsSelected).Select(r => r.Item).ToList();
        if (selected.Count == 0) return;
        var rows = selected.Select(i => new List<string> { i.Badge ?? i.Id, i.Title, i.Subtitle ?? "", i.Meta ?? "" }).ToList();
        var csv = ExportUtil.BuildCsv(new List<string>
        {
            L10n.T(L10n.Key.CsvAccession), L10n.T(L10n.Key.CsvTitle), L10n.T(L10n.Key.CsvSubtitle), L10n.T(L10n.Key.CsvMeta)
        }, rows);
        if (ExportUtil.SaveTextFile(csv, "SciToolbox_" + _provider.Name, "csv"))
            Toast.Show(L10n.T(L10n.Key.Exported, selected.Count.ToString()));
    }

    public void RaiseChange()
    {
        Raise(nameof(SelectedCountLabel));
        Raise(nameof(ShowSelectionBar));
        Raise(nameof(IsAllSelected));
        Raise(nameof(SelectAllLabel));
        Raise(nameof(ShowSelectionBar));
    }

    public void UpdateFavorited(ResultItem item, bool fav)
    {
        var row = RowOf(item);
        if (row != null) row.IsFavorited = fav;
    }
}
