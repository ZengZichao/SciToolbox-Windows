using System.Collections.Generic;
using System.Windows.Input;
using SciToolbox.Core;
using SciToolbox.ViewModels;

namespace SciToolbox.ViewModels;

/// <summary>KV 行显示封装（携带复制 / 打开链接 / 跨库跳转命令）。</summary>
public sealed class KvRowVm : ObservableObject
{
    public string Key { get; }
    public string Value { get; }
    public bool Copyable { get; }
    public string? Link { get; }
    public XLink? XlinkTarget { get; }

    public bool HasLink => !string.IsNullOrEmpty(Link);
    public bool HasXlink => XlinkTarget != null;
    public bool IsInteractive => HasLink || HasXlink;
    public string OpenLabel => SciToolbox.Core.DT.T("打开", "Open");
    public string JumpLabel => SciToolbox.Core.DT.T("跳转", "Go");
    public string CopyA11y => SciToolbox.Core.DT.T("复制这一项", "Copy this value");

    public ICommand CopyCommand { get; }
    public ICommand OpenLinkCommand { get; }
    public ICommand GotoXlinkCommand { get; }
    public ICommand PreviewXlinkCommand { get; }

    public KvRowVm(KVRow row, Action<string> copy, Action<string> openLink, Action<XLink> gotoXlink, Action<XLink> previewXlink)
    {
        Key = row.Key;
        Value = row.Value;
        Copyable = row.Copyable;
        Link = row.Link;
        XlinkTarget = row.XlinkTarget;

        CopyCommand = new RelayCommand(() => copy(row.Value));
        OpenLinkCommand = new RelayCommand(() => { if (row.Link != null) openLink(row.Link); });
        GotoXlinkCommand = new RelayCommand(() => { if (row.XlinkTarget != null) gotoXlink(row.XlinkTarget); });
        PreviewXlinkCommand = new RelayCommand(() => { if (row.XlinkTarget != null) previewXlink(row.XlinkTarget); });
    }
}

public sealed class SectionVm
{
    public string? Title { get; }
    public List<KvRowVm> Rows { get; }
    public bool HasTitle => !string.IsNullOrEmpty(Title);

    public SectionVm(KVSection s, Action<string> copy, Action<string> openLink, Action<XLink> gotoXlink, Action<XLink> previewXlink)
    {
        Title = s.Title;
        Rows = s.Rows.Select(r => new KvRowVm(r, copy, openLink, gotoXlink, previewXlink)).ToList();
    }
}

public sealed class FreeTextVm
{
    public string? Title { get; }
    public string Text { get; }
    public bool Copyable { get; }
    public ICommand CopyCommand { get; }

    public string CopyA11y => SciToolbox.Core.DT.T("复制这段内容", "Copy this block");

    public FreeTextVm(FreeTextBlock b, Action<string> copy)
    {
        Title = b.Title;
        Text = b.Text;
        Copyable = b.Copyable;
        CopyCommand = new RelayCommand(() => copy(b.Text));
    }
}

public sealed class ActionVm
{
    public string Label { get; }
    public bool IsPrimary { get; }
    public ICommand Invoke { get; }

    public ActionVm(DetailAction a, Action<DetailAction> handler)
    {
        Label = a.Label;
        IsPrimary = a.Style == DetailAction.ActionStyleKind.Primary;
        Invoke = new RelayCommand(() => handler(a));
    }
}

public sealed class XLinkVm
{
    public string Label { get; }
    public ICommand Goto { get; }
    public ICommand Preview { get; }

    public XLinkVm(XLink x, Action<XLink> gotoFn, Action<XLink> previewFn)
    {
        Label = x.Label;
        Goto = new RelayCommand(() => gotoFn(x));
        Preview = new RelayCommand(() => previewFn(x));
    }
}

/// <summary>详情栏视图模型（对应 KeyValueDetail + DetailModel）。</summary>
public sealed class DetailVm : ObservableObject
{
    private readonly DetailModel _model;
    private readonly Action<string> _copy;
    private readonly Action<string> _openLink;
    private readonly Action<XLink> _gotoXlink;
    private readonly Action<XLink> _previewXlink;
    private readonly Action<DetailAction> _onAction;

    public string HeaderTitle => _model.HeaderTitle;
    public DetailModel ModelForCompare() => _model;
    public string? HeaderSubtitle => _model.HeaderSubtitle;
    public List<string> HeaderMeta => _model.HeaderMeta ?? new();
    public bool HasHeaderMeta => HeaderMeta.Count > 0;
    public List<SectionVm> Sections { get; }
    public List<FreeTextVm> FreeTexts { get; }
    public List<ActionVm> Actions { get; }
    public List<XLinkVm> XLinks { get; }
    public bool HasXLinks => XLinks.Count > 0;
    public bool HasActions => Actions.Count > 0;
    public string? ImageUrl => _model.ImageUrl;
    public bool HasImage => !string.IsNullOrEmpty(_model.ImageUrl);
    public string? WebUrl => _model.WebUrl;
    public bool HasWebUrl => !string.IsNullOrEmpty(_model.WebUrl);

    public System.Windows.Media.Brush AccentBrush { get; }
    public string ImageFailedText => L10n.T(L10n.Key.ImageFailed);

    private bool _isFavorited;
    public bool IsFavorited { get => _isFavorited; set { if (Set(ref _isFavorited, value)) Raise(nameof(FavoriteGlyph)); } }
    public string FavoriteGlyph => _isFavorited ? "\uE735" : "\uE734";

    public string ViewWebLabel => L10n.T(L10n.Key.ViewOnWebsite);
    public string CrossLinksLabel => L10n.T(L10n.Key.CrossLinks);
    public string OpenLabel => SciToolbox.Core.DT.T("打开", "Open");
    public string JumpLabel => SciToolbox.Core.DT.T("跳转", "Go");
    public string FavoriteTip => L10n.T(_isFavorited ? L10n.Key.Unfavorite : L10n.Key.Favorite);
    public string CollectionTip => L10n.T(L10n.Key.AddToCollection);
    public string CompareTip => L10n.T(L10n.Key.AddCompare);
    public string LoadingText => L10n.T(L10n.Key.LoadingDetail);
    public string SelectTip => L10n.T(L10n.Key.SelectToView);

    public ICommand ToggleFavoriteCommand { get; }
    public ICommand AddToCollectionCommand { get; }
    public ICommand AddToCompareCommand { get; }
    public ICommand OpenWebCommand { get; }
    public ICommand CopyImageFailedReset { get; }

    public DetailVm(DetailModel model, IToolProvider provider,
                    Action<string> copy, Action<string> openLink, Action<XLink> gotoXlink, Action<XLink> previewXlink,
                    Action<DetailAction> onAction, Action toggleFavorite, Action addToCollection, Action addToCompare,
                    Action openWeb, bool isFavorited)
    {
        _model = model;
        _copy = copy;
        _openLink = openLink;
        _gotoXlink = gotoXlink;
        _previewXlink = previewXlink;
        _onAction = onAction;
        Sections = model.Sections.Select(s => new SectionVm(s, copy, openLink, gotoXlink, previewXlink)).ToList();
        FreeTexts = model.FreeTextBlocks.Select(b => new FreeTextVm(b, copy)).ToList();
        Actions = model.Actions.Select(a => new ActionVm(a, onAction)).ToList();
        XLinks = model.XLinks.Select(x => new XLinkVm(x, gotoXlink, previewXlink)).ToList();
        AccentBrush = ThemeManager.CategoryBrush(provider.Category);
        _isFavorited = isFavorited;
        ToggleFavoriteCommand = new RelayCommand(toggleFavorite);
        AddToCollectionCommand = new RelayCommand(addToCollection);
        AddToCompareCommand = new RelayCommand(addToCompare);
        OpenWebCommand = new RelayCommand(openWeb);
        CopyImageFailedReset = new RelayCommand(() => { });
    }
}
