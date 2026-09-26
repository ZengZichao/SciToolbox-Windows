using System.Text;
using System.Xml.Linq;
using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>NCBI 分类检索，经 E-utilities（对应 NCBITaxonomyProvider.swift）。</summary>
public sealed class NcbiTaxonomyProvider : ToolProviderBase
{
    public override string Id => "ncbi_taxonomy";
    public override string Name => DT.T("NCBI 分类检索", "NCBI Taxonomy Search");
    public override ToolCategory Category => ToolCategory.Taxonomy;
    public override string IconName => "tree";
    public override string Placeholder => DT.T("物种名 / TaxID", "Species name / TaxID");
    public override string DataSourceNote => DT.T("数据来源：NCBI Taxonomy (eutils.ncbi.nlm.nih.gov)", "Data source: NCBI Taxonomy (eutils.ncbi.nlm.nih.gov)");

    private const string Base = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var searchUrl = BuildUrl("esearch", new Dictionary<string, string>
        {
            ["db"] = "taxonomy", ["term"] = query, ["retmode"] = "json", ["retmax"] = "30"
        });
        var searchResult = await ApiClient.Shared.GetJsonAsync(searchUrl, ct: ct);
        var ids = searchResult["esearchresult"]["idlist"].Array.Select(x => x.StrOr("")).Where(s => s.Length > 0).ToList();
        if (ids.Count == 0) return new SearchResult { Items = new(), Total = null };

        var sumUrl = BuildUrl("esummary", new Dictionary<string, string>
        {
            ["db"] = "taxonomy", ["id"] = string.Join(",", ids), ["retmode"] = "json"
        });
        var sum = await ApiClient.Shared.GetJsonAsync(sumUrl, ct: ct);
        var map = sum["result"];

        var items = new List<ResultItem>();
        foreach (var id in ids)
        {
            var t = map[id];
            var sciName = t["scientificname"].StrOr("");
            var othernames = t["othernames"].Array;
            var commonName = FirstNonEmpty(t["commonname"].StrOr(""), othernames.Count > 0 ? othernames[0].StrOr("") : "");
            var rank = t["rank"].StrOr("");
            var rankLocal = RankName.Localized(rank);

            items.Add(new ResultItem
            {
                Id = id,
                Title = sciName.Length == 0 ? $"TaxID: {id}" : sciName,
                Subtitle = commonName.Length == 0 ? null : commonName,
                Badge = id,
                Meta = rankLocal.Length == 0 ? null : rankLocal,
                Extra = new Dictionary<string, string> { ["taxid"] = id, ["name"] = sciName }
            });
        }
        return new SearchResult { Items = items, Total = null };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var taxid = context != null && context.TryGetValue("taxid", out var t0) ? t0 : id;
        var sumUrl = BuildUrl("esummary", new Dictionary<string, string>
        {
            ["db"] = "taxonomy", ["id"] = taxid, ["retmode"] = "json"
        });
        var sum = await ApiClient.Shared.GetJsonAsync(sumUrl, ct: ct);
        var t = sum["result"][taxid];

        var sciName = t["scientificname"].StrOr("");
        var commonName = t["commonname"].StrOr("");
        var rank = t["rank"].StrOr("");
        var rankLocal = RankName.Localized(rank);

        var lineageRows = await FetchLineageAsync(taxid, ct);
        lineageRows.Add(new KVRow(rankLocal, sciName, copyable: true));

        var infoRows = new List<KVRow>
        {
            new("TaxID", taxid, copyable: true),
            new(DT.T("学名", "Scientific name"), sciName),
        };
        if (commonName.Length > 0) infoRows.Add(new KVRow(DT.T("常用名", "Common name"), commonName));
        infoRows.Add(new KVRow(DT.T("分类等级", "Rank"), rankLocal.Length == 0 ? rank : rankLocal));

        var xlinks = new List<XLink> { new("gbif", sciName, $"GBIF: {sciName}") };
        if (sciName.Length > 0)
            xlinks.Add(new XLink("ncbi_gene", sciName, DT.T($"NCBI 基因: {sciName}", $"NCBI Gene: {sciName}")));

        var headerMeta = new List<string>();
        if (rankLocal.Length > 0) headerMeta.Add(rankLocal);

        return new DetailModel
        {
            HeaderTitle = sciName.Length == 0 ? $"TaxID: {taxid}" : sciName,
            HeaderSubtitle = commonName.Length == 0 ? null : commonName,
            HeaderMeta = headerMeta,
            Sections = new List<KVSection>
            {
                new(DT.T("基本信息", "Basic Info"), infoRows),
                new(DT.T("分类谱系", "Lineage"), lineageRows)
            },
            Actions = new List<DetailAction> { new(DT.T("复制 TaxID", "Copy TaxID"), taxid, DetailAction.ActionStyleKind.Secondary) },
            XLinks = xlinks,
            WebUrl = $"https://www.ncbi.nlm.nih.gov/Taxonomy/Browser/wwwtax.cgi?id={taxid}"
        };
    }

    private async Task<List<KVRow>> FetchLineageAsync(string taxid, CancellationToken ct)
    {
        var url = BuildUrl("efetch", new Dictionary<string, string>
        {
            ["db"] = "taxonomy", ["id"] = taxid, ["retmode"] = "xml"
        });
        try
        {
            var data = await ApiClient.Shared.GetRawAsync(url, "application/xml,*/*", ct);
            var text = Encoding.UTF8.GetString(data);
            var doc = XDocument.Parse(text);
            var lineageEx = doc.Descendants("LineageEx").FirstOrDefault();
            if (lineageEx == null) return new List<KVRow>();
            var rows = new List<KVRow>();
            foreach (var taxon in lineageEx.Elements("Taxon"))
            {
                var name = taxon.Element("ScientificName")?.Value ?? "";
                var rank = taxon.Element("Rank")?.Value ?? "";
                if (name.Length > 0)
                    rows.Add(new KVRow(RankName.Localized(rank), name, copyable: true));
            }
            return rows;
        }
        catch { return new List<KVRow>(); }
    }

    private static string BuildUrl(string tool, Dictionary<string, string> parameters) =>
        Urls.BuildQueryURL($"{Base}/{tool}.fcgi", parameters);

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
