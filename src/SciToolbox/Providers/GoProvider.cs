using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>GO 术语查询，经 EBI QuickGO REST API（对应 GOProvider.swift）。</summary>
public sealed class GoProvider : ToolProviderBase
{
    public override string Id => "go";
    public override string Name => DT.T("GO 术语查询", "GO Term Search");
    public override ToolCategory Category => ToolCategory.Function;
    public override string IconName => "grid";
    public override string Placeholder => DT.T("GO ID / 关键词", "GO ID / Keyword");
    public override string DataSourceNote => DT.T("数据来源：EBI QuickGO (www.ebi.ac.uk/QuickGO)", "Data source: EBI QuickGO (www.ebi.ac.uk/QuickGO)");

    private const string Base = "https://www.ebi.ac.uk/QuickGO/services/ontology/go";

    private static readonly Dictionary<string, (string zh, string en, string abbr)> AspectMap = new()
    {
        ["biological_process"] = ("生物学过程", "Biological Process", "BP"),
        ["cellular_component"] = ("细胞组分", "Cellular Component", "CC"),
        ["molecular_function"] = ("分子功能", "Molecular Function", "MF"),
    };

    private static (string name, string abbr)? AspectInfo(string aspect) =>
        AspectMap.TryGetValue(aspect, out var a) ? (DT.T(a.zh, a.en), a.abbr) : null;

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = Urls.BuildQueryURL(Base + "/search", new Dictionary<string, string>
        {
            ["query"] = query, ["limit"] = "50", ["page"] = "1"
        });
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var items = new List<ResultItem>();
        foreach (var t in data["results"].Array)
        {
            var goId = t["id"].StrOr("");
            var name = t["name"].StrOr("");
            var aspect = t["aspect"].StrOr("");
            var info = AspectInfo(aspect);
            items.Add(new ResultItem
            {
                Id = goId,
                Title = name.Length == 0 ? goId : name,
                Badge = goId,
                Meta = info?.name ?? aspect,
                Extra = new Dictionary<string, string> { ["goId"] = goId }
            });
        }
        return new SearchResult { Items = items, Total = null };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var goId = context != null && context.TryGetValue("goId", out var g0) ? g0 : id;
        var encodedId = Urls.EncodePath(goId);

        var termTask = ApiClient.Shared.GetJsonAsync(Base + "/terms/" + encodedId, ct: ct);
        var ancTask = FetchList(Base + "/terms/" + encodedId + "/ancestors", goId, ct);
        var childTask = FetchList(Base + "/terms/" + encodedId + "/children", goId, ct);
        await Task.WhenAll(termTask, ancTask, childTask);

        var termRaw = termTask.Result;
        var ancestors = ancTask.Result;
        var children = childTask.Result;

        var term = UnwrapResults(termRaw).Select(NormalizeTerm).FirstOrDefault(t => t != null)
            ?? new GoTerm { GoId = goId };

        var rows = new List<KVRow>
        {
            new("GO ID", term.GoId, copyable: true),
            new(DT.T("名称", "Name"), term.Name),
            new(DT.T("类别", "Category"), term.AspectName),
        };
        if (term.IsObsolete) rows.Add(new KVRow(DT.T("状态", "Status"), DT.T("已废弃", "Obsolete")));

        var sections = new List<KVSection> { new(DT.T("术语信息", "Term Info"), rows) };

        if (term.Definition.Length > 0)
            sections.Add(new KVSection(DT.T("定义", "Definition"), new List<KVRow> { new("", term.Definition) }));
        if (term.Synonyms.Count > 0)
            sections.Add(new KVSection(DT.T("同义词", "Synonyms"), term.Synonyms.Select(s => new KVRow("", s)).ToList()));
        if (ancestors.Count > 0)
            sections.Add(new KVSection(DT.T("祖先节点", "Ancestors"),
                ancestors.Take(40).Select(a => new KVRow(a.AspectShort, a.GoId + " — " + a.Name, copyable: true)).ToList()));
        if (children.Count > 0)
            sections.Add(new KVSection(DT.T("子节点", "Children"),
                children.Take(60).Select(c => new KVRow(c.AspectShort, c.GoId + " — " + c.Name, copyable: true)).ToList()));

        var headerMeta = new List<string>();
        if (term.AspectName.Length > 0) headerMeta.Add(term.AspectName);

        return new DetailModel
        {
            HeaderTitle = term.Name.Length == 0 ? goId : term.Name,
            HeaderSubtitle = goId,
            HeaderMeta = headerMeta,
            Sections = sections,
            Actions = new List<DetailAction> { new(DT.T("复制 GO ID", "Copy GO ID"), goId, DetailAction.ActionStyleKind.Secondary) },
            XLinks = new List<XLink> { new("uniprot", goId, DT.T("UniProt: 搜索含此 GO 的蛋白", "UniProt: Search proteins with this GO")) },
            WebUrl = "https://www.ebi.ac.uk/QuickGO/term/" + goId
        };
    }

    private sealed class GoTerm
    {
        public string GoId = ""; public string Name = ""; public string Definition = "";
        public string Aspect = ""; public string AspectName = ""; public string AspectShort = "";
        public bool IsObsolete; public List<string> Synonyms = new();
    }

    private async Task<List<GoTerm>> FetchList(string url, string exclude, CancellationToken ct)
    {
        try
        {
            var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
            return UnwrapResults(data).Select(NormalizeTerm).Where(t => t != null && t.GoId != exclude).Select(t => t!).ToList();
        }
        catch { return new List<GoTerm>(); }
    }

    private static List<Json> UnwrapResults(Json data)
    {
        var wrapped = data["results"].Array;
        return wrapped.Count > 0 ? wrapped.ToList() : data.Array.ToList();
    }

    private static GoTerm? NormalizeTerm(Json t)
    {
        if (t["id"].String == null) return null;
        var def = FirstNonEmpty(t["definition"]["text"].StrOr(""), t["definition"].StrOr(""));
        var aspect = t["aspect"].StrOr("");
        var info = AspectInfo(aspect);
        var syns = t["synonyms"].Array.Select(s => FirstNonEmpty(s.StrOr(""), s["name"].StrOr(""), s["value"].StrOr("")))
            .Where(s => s.Length > 0).ToList();

        return new GoTerm
        {
            GoId = t["id"].StrOr(""),
            Name = t["name"].StrOr(""),
            Definition = def,
            Aspect = aspect,
            AspectName = info?.name ?? aspect,
            AspectShort = info?.abbr ?? "",
            IsObsolete = t["isObsolete"].Bool ?? false,
            Synonyms = syns
        };
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
