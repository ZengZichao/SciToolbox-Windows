using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>GBIF 物种数据库 — 全球生物多样性（对应 GBIFProvider.swift）。</summary>
public sealed class GbifProvider : ToolProviderBase
{
    public override string Id => "gbif";
    public override string Name => DT.T("GBIF 物种", "GBIF Species");
    public override ToolCategory Category => ToolCategory.Taxonomy;
    public override string IconName => "leaf";
    public override string Placeholder => DT.T("物种名 / 关键词", "Species name / Keyword");
    public override string DataSourceNote => DT.T("数据来源：GBIF (api.gbif.org)", "Data source: GBIF (api.gbif.org)");

    private const string Base = "https://api.gbif.org/v1";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = Urls.BuildQueryURL($"{Base}/species/search", new Dictionary<string, string>
        {
            ["q"] = query, ["limit"] = "20"
        });
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var items = new List<ResultItem>();
        foreach (var s in data["results"].Array)
        {
            var key = s["key"].Int?.ToString() ?? "";
            var sciName = s["scientificName"].StrOr("");
            var canonical = FirstNonEmpty(s["canonicalName"].StrOr(""), sciName);
            var rank = s["rank"].StrOr("");
            var kingdom = s["kingdom"].StrOr("");
            var family = s["family"].StrOr("");

            items.Add(new ResultItem
            {
                Id = key,
                Title = sciName.Length == 0 ? $"Key: {key}" : sciName,
                Subtitle = canonical == sciName ? null : canonical,
                Badge = key,
                Meta = string.Join(" · ", new[] { kingdom, family, rank }.Where(x => x.Length > 0)),
                Extra = new Dictionary<string, string> { ["key"] = key }
            });
        }
        var count = data["count"].Int ?? items.Count;
        return new SearchResult { Items = items, Total = count };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var key = context != null && context.TryGetValue("key", out var k) ? k : id;
        var url = $"{Base}/species/{Urls.EncodePath(key)}";
        var s = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var sciName = s["scientificName"].StrOr("");
        var canonical = FirstNonEmpty(s["canonicalName"].StrOr(""), sciName);
        var authorship = s["authorship"].StrOr("");
        var rank = s["rank"].StrOr("");
        var taxStatus = s["taxonomicStatus"].StrOr("");
        var acceptedKey = s["acceptedKey"].Int?.ToString() ?? "";

        var rows = new List<KVRow>
        {
            new("Key", key, copyable: true),
            new(DT.T("学名", "Scientific name"), sciName),
        };
        if (canonical.Length > 0 && canonical != sciName) rows.Add(new KVRow(DT.T("规范名", "Canonical name"), canonical));
        if (authorship.Length > 0) rows.Add(new KVRow(DT.T("命名作者", "Author"), authorship));
        if (rank.Length > 0) rows.Add(new KVRow(DT.T("等级", "Rank"), rank));
        if (taxStatus.Length > 0) rows.Add(new KVRow(DT.T("分类状态", "Taxonomic status"), taxStatus));
        if (acceptedKey.Length > 0) rows.Add(new KVRow(DT.T("接受名 Key", "Accepted key"), acceptedKey, copyable: true));

        var classRanks = new[] { "kingdom", "phylum", "class", "order", "family", "genus", "species" };
        var classLabels = new[] { DT.T("界", "Kingdom"), DT.T("门", "Phylum"), DT.T("纲", "Class"), DT.T("目", "Order"), DT.T("科", "Family"), DT.T("属", "Genus"), DT.T("种", "Species") };
        var classRows = new List<KVRow>();
        for (int i = 0; i < classRanks.Length; i++)
        {
            var val = s[classRanks[i]].StrOr("");
            if (val.Length > 0) classRows.Add(new KVRow(classLabels[i], val));
        }

        var sections = new List<KVSection> { new(DT.T("基本信息", "Basic Info"), rows) };
        if (classRows.Count > 0) sections.Add(new KVSection(DT.T("分类", "Classification"), classRows));

        var childrenTask = SafeList($"{Base}/species/{key}/children?limit=50", ct);
        var synonymsTask = SafeList($"{Base}/species/{key}/synonyms?limit=50", ct);
        await Task.WhenAll(childrenTask, synonymsTask);
        var children = childrenTask.Result;
        var synonyms = synonymsTask.Result;

        if (children.Count > 0)
            sections.Add(new KVSection(DT.T($"下级分类 ({children.Count})", $"Child taxa ({children.Count})"),
                children.Select(c => new KVRow(c.Rank, c.ScientificName + (c.CanonicalName != c.ScientificName ? $" ({c.CanonicalName})" : ""), copyable: true)).ToList()));
        if (synonyms.Count > 0)
            sections.Add(new KVSection(DT.T($"同物异名 ({synonyms.Count})", $"Synonyms ({synonyms.Count})"),
                synonyms.Select(syn => new KVRow(syn.Rank, syn.ScientificName)).ToList()));

        var xlinks = new List<XLink> { new("ncbi_taxonomy", canonical, DT.T($"NCBI 分类: {canonical}", $"NCBI Taxonomy: {canonical}")) };

        var headerMeta = new List<string>();
        if (rank.Length > 0) headerMeta.Add(rank);
        if (taxStatus.Length > 0) headerMeta.Add(taxStatus);

        return new DetailModel
        {
            HeaderTitle = sciName.Length == 0 ? $"Key: {key}" : sciName,
            HeaderSubtitle = canonical == sciName ? null : canonical,
            HeaderMeta = headerMeta,
            Sections = sections,
            Actions = new List<DetailAction> { new(DT.T("复制 Key", "Copy Key"), key, DetailAction.ActionStyleKind.Secondary) },
            XLinks = xlinks,
            WebUrl = $"https://www.gbif.org/species/{key}"
        };
    }

    private sealed class SpeciesSummary
    {
        public string Key = ""; public string ScientificName = ""; public string CanonicalName = ""; public string Rank = "";
    }

    private async Task<List<SpeciesSummary>> SafeList(string url, CancellationToken ct)
    {
        try
        {
            var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
            var list = new List<SpeciesSummary>();
            foreach (var s in data["results"].Array)
            {
                var key = s["key"].Int?.ToString() ?? "";
                if (key.Length == 0) continue;
                list.Add(new SpeciesSummary
                {
                    Key = key,
                    ScientificName = s["scientificName"].StrOr(""),
                    CanonicalName = FirstNonEmpty(s["canonicalName"].StrOr(""), s["scientificName"].StrOr("")),
                    Rank = s["rank"].StrOr("")
                });
            }
            return list;
        }
        catch { return new List<SpeciesSummary>(); }
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
