using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>MGnify 微生物组数据库 — EBI REST API（对应 MGnifyProvider.swift）。</summary>
public sealed class MGnifyProvider : ToolProviderBase
{
    public override string Id => "mgnify";
    public override string Name => DT.T("MGnify 微生物组", "MGnify Microbiome");
    public override ToolCategory Category => ToolCategory.Taxonomy;
    public override string IconName => "hexagon";
    public override string Placeholder => DT.T("关键词 / Accession", "Keyword / Accession");
    public override string DataSourceNote => DT.T("数据来源：EBI MGnify (www.ebi.ac.uk/metagenomics)", "Data source: EBI MGnify (www.ebi.ac.uk/metagenomics)");

    private const string Base = "https://www.ebi.ac.uk/metagenomics/api/v1";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = Urls.BuildQueryURL($"{Base}/studies", new Dictionary<string, string>
        {
            ["q"] = query, ["page_size"] = "20"
        });
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var items = new List<ResultItem>();
        foreach (var s in data["data"].Array)
        {
            var a = s["attributes"];
            var accession = FirstNonEmpty(a["accession"].StrOr(""), s["id"].StrOr(""));
            var name = a["study-name"].StrOr("");
            var biome = BiomeOf(s);
            var samplesCount = a["samples-count"].IntOr(0);

            items.Add(new ResultItem
            {
                Id = accession,
                Title = name.Length == 0 ? accession : name,
                Subtitle = biome.Length == 0 ? null : biome,
                Badge = accession,
                Meta = $"{samplesCount} samples",
                Extra = new Dictionary<string, string> { ["accession"] = accession }
            });
        }
        var count = data["meta"]["pagination"]["count"].Int ?? items.Count;
        return new SearchResult { Items = items, Total = count };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var accession = context != null && context.TryGetValue("accession", out var a0) ? a0 : id;
        var url = $"{Base}/studies/{Urls.EncodePath(accession)}";
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        var s = data["data"];
        var a = s["attributes"];

        var name = a["study-name"].StrOr("");
        var mgnifyAbstract = a["study-abstract"].StrOr("");
        var secondaryAcc = a["secondary-accession"].StrOr("");
        var bioproject = a["bioproject"].StrOr("");
        var samplesCount = a["samples-count"].IntOr(0);
        var centre = a["centre-name"].StrOr("");
        var lastUpdate = a["last-update"].StrOr("");
        var biome = BiomeOf(s);

        var rows = new List<KVRow>
        {
            new("Accession", accession, copyable: true),
            new(DT.T("名称", "Name"), name),
        };
        if (secondaryAcc.Length > 0) rows.Add(new KVRow(DT.T("次要编号", "Secondary accession"), secondaryAcc));
        if (bioproject.Length > 0) rows.Add(new KVRow("BioProject", bioproject, copyable: true));
        if (biome.Length > 0) rows.Add(new KVRow(DT.T("生物群系", "Biome"), biome));
        rows.Add(new KVRow(DT.T("样本数", "Sample count"), samplesCount.ToString()));
        if (centre.Length > 0) rows.Add(new KVRow(DT.T("中心", "Centre"), centre));
        if (lastUpdate.Length > 0) rows.Add(new KVRow(DT.T("最后更新", "Last updated"), lastUpdate));

        var sections = new List<KVSection> { new(DT.T("研究信息", "Study Info"), rows) };
        if (mgnifyAbstract.Length > 0)
            sections.Add(new KVSection(DT.T("摘要", "Abstract"), new List<KVRow> { new("", mgnifyAbstract) }));

        var headerMeta = new List<string>();
        if (biome.Length > 0) headerMeta.Add(biome);

        return new DetailModel
        {
            HeaderTitle = name.Length == 0 ? accession : name,
            HeaderSubtitle = accession,
            HeaderMeta = headerMeta,
            Sections = sections,
            Actions = new List<DetailAction> { new(DT.T("复制 Accession", "Copy Accession"), accession, DetailAction.ActionStyleKind.Secondary) },
            WebUrl = $"https://www.ebi.ac.uk/metagenomics/studies/{accession}"
        };
    }

    private static string BiomeOf(Json s)
    {
        var bd = s["relationships"]["biomes"]["data"].Array;
        if (bd.Count > 0)
        {
            var firstId = bd[0]["id"].StrOr("");
            var segs = firstId.Split(':');
            if (segs.Length > 1) return string.Join(" › ", segs.Skip(1));
            return firstId;
        }
        return "";
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
