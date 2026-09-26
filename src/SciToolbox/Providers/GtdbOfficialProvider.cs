using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>GTDB 官方分类树浏览器 — 官方 GTDB API（对应 GTDBOfficialProvider.swift）。</summary>
public sealed class GtdbOfficialProvider : ToolProviderBase
{
    public override string Id => "gtdb_official";
    public override string Name => DT.T("GTDB 官方分类", "GTDB Official Taxonomy");
    public override ToolCategory Category => ToolCategory.Taxonomy;
    public override string IconName => "globe";
    public override string Placeholder => DT.T("分类名（留空浏览域级）", "Taxon name (leave empty to browse domains)");
    public override string DataSourceNote => DT.T("数据来源：GTDB 官方 API (gtdb-api.ecogenomic.org)", "Data source: GTDB official API (gtdb-api.ecogenomic.org)");

    private const string Base = "https://gtdb-api.ecogenomic.org";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var q = query.Trim();
        if (q.Length == 0)
        {
            return new SearchResult
            {
                Items = new List<ResultItem>
                {
                    new() { Id = "d__Archaea", Title = "Archaea", Badge = DT.T("域", "Domain"), Meta = DT.T("古菌域", "Archaea domain"), Extra = new Dictionary<string, string> { ["taxon"] = "d__Archaea" } },
                    new() { Id = "d__Bacteria", Title = "Bacteria", Badge = DT.T("域", "Domain"), Meta = DT.T("细菌域", "Bacteria domain"), Extra = new Dictionary<string, string> { ["taxon"] = "d__Bacteria" } },
                },
                Total = 2
            };
        }

        var url = $"{Base}/taxon/search/{Urls.EncodePath(q)}?limit=30";
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        if (data["detail"].String != null)
            throw ApiException.NotFound(FirstNonEmpty(data["detail"].StrOr(""), DT.T("未找到", "Not found")));

        var items = new List<ResultItem>();
        foreach (var taxon in data["matches"].Array)
        {
            var taxonStr = taxon.StrOr("");
            if (taxonStr.Length == 0) continue;
            var parsed = ParseTaxon(taxonStr);
            items.Add(new ResultItem
            {
                Id = taxonStr,
                Title = parsed.name,
                Badge = parsed.rankName,
                Meta = taxonStr,
                Extra = new Dictionary<string, string> { ["taxon"] = taxonStr }
            });
        }
        return new SearchResult { Items = items, Total = items.Count };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var taxon = context != null && context.TryGetValue("taxon", out var t) ? t : id;
        var url = $"{Base}/taxon/{Urls.EncodePath(taxon)}";
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        if (data["detail"].String != null)
            throw ApiException.NotFound(FirstNonEmpty(data["detail"].StrOr(""), DT.T("未找到该分类", "Taxon not found")));

        var parsed = ParseTaxon(taxon);
        // GTDB API 子节点为顶层数组；基因组叶节点响应为对象（此时无子分类）。
        var children = data.Array;

        var childRows = new List<KVRow>();
        var xlinks = new List<XLink>();
        foreach (var child in children)
        {
            var childTaxon = child["taxon"].StrOr("");
            var total = child["total"].IntOr(0);
            var isGenome = child["isGenome"].Bool ?? false;
            if (childTaxon.Length == 0) continue;
            var cp = ParseTaxon(childTaxon);
            var label = cp.name + (total > 0 ? $" ({total})" : "") + (isGenome ? DT.T(" [基因组]", " [genome]") : "");
            childRows.Add(new KVRow(cp.rankName, label, copyable: true));
            if (!isGenome)
                xlinks.Add(new XLink("gtdb_official", childTaxon, DT.T($"下钻: {cp.name}", $"Drill down: {cp.name}")));
        }

        var sections = new List<KVSection>
        {
            new(DT.T("分类信息", "Taxonomy Info"), new List<KVRow>
            {
                new(DT.T("分类名", "Taxon name"), parsed.name, copyable: true),
                new(DT.T("完整标记", "Full marker"), taxon, copyable: true),
                new(DT.T("等级", "Rank"), parsed.rankName),
            })
        };
        if (childRows.Count > 0)
            sections.Add(new KVSection(DT.T($"下级分类 ({childRows.Count})", $"Child taxa ({childRows.Count})"), childRows));

        return new DetailModel
        {
            HeaderTitle = parsed.name,
            HeaderSubtitle = parsed.rankName,
            HeaderMeta = new List<string> { taxon },
            Sections = sections,
            Actions = new List<DetailAction> { new(DT.T("复制分类名", "Copy Taxon"), taxon, DetailAction.ActionStyleKind.Secondary) },
            XLinks = xlinks,
            WebUrl = "https://gtdb.ecogenomic.org/tree?r=" + Uri.EscapeDataString(taxon)
        };
    }

    private static (string rank, string rankName, string name) ParseTaxon(string taxon)
    {
        var idx = taxon.IndexOf("__", StringComparison.Ordinal);
        if (idx >= 0)
        {
            var rank = taxon.Substring(0, idx);
            var name = taxon.Substring(idx + 2);
            return (rank, GtdbRank.Name(rank), name);
        }
        return ("", "", taxon);
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
