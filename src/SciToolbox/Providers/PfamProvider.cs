using System.Text.RegularExpressions;
using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>Pfam / InterPro 检索，经 EBI Search + InterPro API（对应 PfamProvider.swift）。</summary>
public sealed class PfamProvider : ToolProviderBase
{
    public override string Id => "pfam";
    public override string Name => "Pfam / InterPro";
    public override ToolCategory Category => ToolCategory.Protein;
    public override string IconName => "stack";
    public override string Placeholder => DT.T("蛋白家族名 / 关键词", "Protein family name / Keyword");
    public override string DataSourceNote => DT.T("数据来源：EBI InterPro (www.ebi.ac.uk/interpro)", "Data source: EBI InterPro (www.ebi.ac.uk/interpro)");

    private const string EbiSearch = "https://www.ebi.ac.uk/ebisearch/ws/rest/pfam";
    private const string Interpro = "https://www.ebi.ac.uk/interpro/api/entry/pfam";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        if (query.Length < 2)
            throw ApiException.InvalidInput(DT.T("检索词至少 2 个字符", "Search term must be at least 2 characters"));

        var url = Urls.BuildQueryURL(EbiSearch, new Dictionary<string, string>
        {
            ["query"] = query, ["format"] = "json", ["size"] = "20"
        });
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var entries = data["entries"].Array;
        var total = data["hitCount"].Int ?? entries.Count;
        if (entries.Count == 0) return new SearchResult { Items = new(), Total = 0 };

        var items = new List<ResultItem>();
        foreach (var e in entries)
        {
            var acc = FirstNonEmpty(e["acc"].StrOr(""), e["id"].StrOr(""));
            PfamEntry? detail = null;
            try { detail = await FetchEntryAsync(acc, ct); } catch { }
            items.Add(new ResultItem
            {
                Id = acc,
                Title = detail?.Name ?? acc,
                Subtitle = detail?.ShortName,
                Badge = acc,
                Meta = detail?.Type,
                Extra = new Dictionary<string, string> { ["acc"] = acc }
            });
        }
        return new SearchResult { Items = items, Total = total };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var acc = ((context != null && context.TryGetValue("acc", out var a) ? a : id)).ToUpperInvariant();
        var d = await FetchEntryAsync(acc, ct);

        var rows = new List<KVRow>
        {
            new("Accession", d.Accession, copyable: true),
            new(DT.T("名称", "Name"), d.Name),
            new(DT.T("缩写", "Short name"), d.ShortName),
            new(DT.T("类型", "Type"), d.Type),
            new(DT.T("数据库", "Database"), d.SourceDatabase),
        };
        if (d.Integrated.Length > 0) rows.Add(new KVRow(DT.T("整合入", "Integrated into"), d.Integrated, copyable: true));

        var sections = new List<KVSection> { new(DT.T("条目信息", "Entry Info"), rows) };
        if (d.Description.Length > 0)
            sections.Add(new KVSection(DT.T("描述", "Description"), new List<KVRow> { new("", d.Description) }));
        if (d.GoTerms.Count > 0)
            sections.Add(new KVSection(DT.T("GO 注释", "GO annotation"), d.GoTerms.Select(g => new KVRow(g.id, g.name, copyable: true)).ToList()));

        var xlinks = d.GoTerms.Select(g => new XLink("go", g.id, $"GO: {g.name}")).ToList();

        var headerMeta = new List<string>();
        if (d.Type.Length > 0) headerMeta.Add(d.Type);
        if (d.ShortName.Length > 0) headerMeta.Add(d.ShortName);

        return new DetailModel
        {
            HeaderTitle = d.Name.Length == 0 ? d.Accession : d.Name,
            HeaderSubtitle = d.Accession,
            HeaderMeta = headerMeta,
            Sections = sections,
            Actions = new List<DetailAction> { new(DT.T("复制 Accession", "Copy Accession"), d.Accession, DetailAction.ActionStyleKind.Secondary) },
            XLinks = xlinks,
            WebUrl = d.WebUrl
        };
    }

    private sealed class PfamEntry
    {
        public string Accession = ""; public string Name = ""; public string ShortName = "";
        public string Type = ""; public string SourceDatabase = ""; public string Integrated = "";
        public string Description = ""; public List<(string id, string name)> GoTerms = new();
        public int LiteratureCount; public string WebUrl = "";
    }

    private async Task<PfamEntry> FetchEntryAsync(string acc, CancellationToken ct)
    {
        var url = $"{Interpro}/{Urls.EncodePath(acc)}";
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        var m = data["metadata"];
        var nameObj = m["name"];

        var descArr = m["description"].Array;
        var goArr = m["go_terms"].Array;
        var litArr = m["literature"].Array;

        var goTerms = new List<(string id, string name)>();
        foreach (var g in goArr)
        {
            var gid = g["identifier"].StrOr("");
            var gname = g["name"].StrOr("");
            if (gname.Length > 0) goTerms.Add((gid, gname));
        }

        return new PfamEntry
        {
            Accession = FirstNonEmpty(m["accession"].StrOr(""), acc),
            Name = nameObj["name"].StrOr(""),
            ShortName = nameObj["short"].StrOr(""),
            Type = m["type"].StrOr(""),
            SourceDatabase = FirstNonEmpty(m["source_database"].StrOr(""), "pfam"),
            Integrated = m["integrated"].StrOr(""),
            Description = StripHtml(descArr.Count > 0 ? descArr[0]["text"].StrOr("") : ""),
            GoTerms = goTerms,
            LiteratureCount = litArr.Count,
            WebUrl = $"https://www.ebi.ac.uk/interpro/entry/pfam/{acc}"
        };
    }

    private static string StripHtml(string s) =>
        Regex.Replace(Regex.Replace(s, "<[^>]+>", " "), @"\s+", " ").Trim();

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
