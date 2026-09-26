using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>BacDive 菌株数据库 — DSMZ REST API v2（对应 BacDiveProvider.swift）。</summary>
public sealed class BacDiveProvider : ToolProviderBase
{
    public override string Id => "bacdive";
    public override string Name => DT.T("BacDive 菌株", "BacDive Strains");
    public override ToolCategory Category => ToolCategory.Taxonomy;
    public override string IconName => "bug";
    public override string Placeholder => DT.T("属名 或 属名 种名", "Genus or Genus species");
    public override string DataSourceNote => DT.T("数据来源：BacDive (api.bacdive.dsmz.de)", "Data source: BacDive (api.bacdive.dsmz.de)");

    private const string Base = "https://api.bacdive.dsmz.de";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw ApiException.InvalidInput(DT.T("请输入属名", "Please enter a genus"));
        var genus = parts[0];
        var species = parts.Length > 1 ? parts[1] : "";

        var url = species.Length == 0
            ? $"{Base}/v2/taxon/{Urls.EncodePath(genus)}"
            : $"{Base}/v2/taxon/{Urls.EncodePath(genus)}/{Urls.EncodePath(species)}";

        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        var ids = data["results"].Array.Select(x => x.StrOr("")).Where(s => s.Length > 0).Take(60).ToList();
        var count = data["count"].Int ?? ids.Count;
        if (ids.Count == 0) return new SearchResult { Items = new(), Total = 0 };

        var summaries = new Dictionary<string, (string name, string strain, string typeStrain, string dsm)>();
        var preview = ids.Take(10).ToList();
        if (preview.Count > 0)
        {
            try
            {
                var fetchUrl = $"{Base}/v2/fetch/{string.Join(";", preview)}";
                var fd = await ApiClient.Shared.GetJsonAsync(fetchUrl, ct: ct);
                foreach (var kv in fd["results"].Dict)
                {
                    var s = ExtractStrain(kv.Value);
                    summaries[kv.Key] = (s.FullName, s.StrainDesignation, s.TypeStrain, s.DsmNumber);
                }
            }
            catch { /* 摘要获取失败不阻断主结果 */ }
        }

        var items = new List<ResultItem>();
        foreach (var id in ids)
        {
            summaries.TryGetValue(id, out var summary);
            items.Add(new ResultItem
            {
                Id = id,
                Title = !string.IsNullOrEmpty(summary.name) ? summary.name : $"BacDive ID: {id}",
                Subtitle = summary.strain,
                Badge = id,
                Meta = !string.IsNullOrEmpty(summary.typeStrain) ? "Type strain" : null,
                Extra = new Dictionary<string, string> { ["bacdiveId"] = id }
            });
        }
        return new SearchResult { Items = items, Total = count };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var bacdiveId = context != null && context.TryGetValue("bacdiveId", out var b) ? b : id;
        var url = $"{Base}/v2/fetch/{Urls.EncodePath(bacdiveId)}";
        var fd = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        var res = fd["results"].Dict;
        Json rec = res.TryGetValue(bacdiveId, out var r0) ? r0 : (res.Values.FirstOrDefault() ?? Json.Null);
        var s = ExtractStrain(rec);

        if (s.BacdiveId.Length == 0)
            throw ApiException.NotFound(DT.T("未找到该菌株", "Strain not found"));

        var sections = new List<KVSection>();

        var infoRows = new List<KVRow>
        {
            new("BacDive ID", s.BacdiveId, copyable: true),
            new(DT.T("DSM 编号", "DSM Number"), s.DsmNumber, copyable: true),
            new(DT.T("全名", "Full Name"), s.FullName),
            new(DT.T("菌株编号", "Strain Designation"), s.StrainDesignation),
        };
        if (s.TypeStrain.Length > 0) infoRows.Add(new KVRow("Type strain", s.TypeStrain));
        if (s.NcbiTaxId.Length > 0) infoRows.Add(new KVRow("NCBI TaxID", s.NcbiTaxId, copyable: true));
        if (s.Description.Length > 0) infoRows.Add(new KVRow(DT.T("描述", "Description"), s.Description));
        sections.Add(new KVSection(DT.T("基本信息", "Basic Info"), infoRows));

        var taxRows = new List<(string k, string v)>
        {
            (DT.T("域", "Domain"), s.Domain), (DT.T("门", "Phylum"), s.Phylum),
            (DT.T("纲", "Class"), s.Class), (DT.T("目", "Order"), s.Order),
            (DT.T("科", "Family"), s.Family), (DT.T("属", "Genus"), s.Genus),
            (DT.T("种", "Species"), s.Species)
        }.Where(x => x.v.Length > 0).Select(x => new KVRow(x.k, x.v)).ToList();
        if (taxRows.Count > 0) sections.Add(new KVSection(DT.T("分类", "Taxonomy"), taxRows));

        var morphRows = new List<(string k, string v)>
        {
            (DT.T("革兰氏染色", "Gram Stain"), s.GramStain), (DT.T("细胞形态", "Cell Shape"), s.CellShape),
            (DT.T("细胞长度", "Cell Length"), s.CellLength), (DT.T("细胞宽度", "Cell Width"), s.CellWidth),
            (DT.T("运动性", "Motility"), s.Motility)
        }.Where(x => x.v.Length > 0).Select(x => new KVRow(x.k, x.v)).ToList();
        if (morphRows.Count > 0) sections.Add(new KVSection(DT.T("形态", "Morphology"), morphRows));

        var cultRows = new List<KVRow>();
        if (s.MediumName.Length > 0) cultRows.Add(new KVRow(DT.T("培养基", "Medium"), s.MediumName));
        if (s.TempOptimum.Length > 0) cultRows.Add(new KVRow(DT.T("最适温度", "Optimum Temp"), $"{s.TempOptimum} °C"));
        if (s.TempMinimum.Length > 0) cultRows.Add(new KVRow(DT.T("最低温度", "Min Temp"), $"{s.TempMinimum} °C"));
        if (s.TempMaximum.Length > 0) cultRows.Add(new KVRow(DT.T("最高温度", "Max Temp"), $"{s.TempMaximum} °C"));
        if (s.PhOptimum.Length > 0) cultRows.Add(new KVRow(DT.T("最适 pH", "Optimum pH"), s.PhOptimum));
        if (s.OxygenTolerance.Length > 0) cultRows.Add(new KVRow(DT.T("需氧性", "Oxygen"), s.OxygenTolerance));
        if (cultRows.Count > 0) sections.Add(new KVSection(DT.T("培养条件", "Culture Conditions"), cultRows));

        var seqRows = new List<KVRow>();
        if (s.GcContent.Length > 0) seqRows.Add(new KVRow(DT.T("GC 含量", "GC Content"), s.GcContent));
        if (s.GenomeAccession.Length > 0) seqRows.Add(new KVRow(DT.T("基因组", "Genome"), s.GenomeAccession, copyable: true));
        if (s.Seq16sAccession.Length > 0)
            seqRows.Add(new KVRow("16S rRNA", $"{s.Seq16sAccession} ({s.Seq16sLength} bp)", copyable: true));
        if (seqRows.Count > 0) sections.Add(new KVSection(DT.T("序列信息", "Sequence Info"), seqRows));

        if (s.Enzymes.Count > 0)
            sections.Add(new KVSection(DT.T("酶活性", "Enzymes"),
                s.Enzymes.Take(30).Select(e => new KVRow(e.ec, $"{e.value} — {e.activity}")).ToList()));

        if (s.Literature.Count > 0)
            sections.Add(new KVSection(DT.T("文献", "References"), s.Literature.Select(l =>
            {
                var ps = new List<string>();
                if (l.authors.Length > 0) ps.Add(l.authors);
                if (l.journal.Length > 0) ps.Add(l.journal);
                if (l.year.Length > 0) ps.Add(l.year);
                return new KVRow(l.year, l.title + (ps.Count == 0 ? "" : " — " + string.Join(", ", ps)));
            }).ToList()));

        var xlinks = new List<XLink>();
        if (s.NcbiTaxId.Length > 0)
            xlinks.Add(new XLink("ncbi_taxonomy", s.NcbiTaxId, DT.T($"NCBI 分类: {s.FullName}", $"NCBI Taxonomy: {s.FullName}")));

        var headerMeta = new List<string>();
        if (s.TypeStrain.Length > 0) headerMeta.Add("Type strain");
        if (s.DsmNumber.Length > 0) headerMeta.Add($"DSM {s.DsmNumber}");

        return new DetailModel
        {
            HeaderTitle = s.FullName.Length == 0 ? $"BacDive {s.BacdiveId}" : s.FullName,
            HeaderSubtitle = s.StrainDesignation.Length == 0 ? $"BacDive ID: {s.BacdiveId}" : s.StrainDesignation,
            HeaderMeta = headerMeta,
            Sections = sections,
            Actions = new List<DetailAction> { new(DT.T("复制 BacDive ID", "Copy BacDive ID"), s.BacdiveId, DetailAction.ActionStyleKind.Secondary) },
            XLinks = xlinks,
            WebUrl = $"https://bacdive.dsmz.de/strain/{s.BacdiveId}"
        };
    }

    private sealed class StrainData
    {
        public string BacdiveId = ""; public string DsmNumber = ""; public string FullName = "";
        public string StrainDesignation = ""; public string TypeStrain = ""; public string NcbiTaxId = "";
        public string Description = "";
        public string Domain = "", Phylum = "", Class = "", Order = "", Family = "", Genus = "", Species = "";
        public string GramStain = "", CellShape = "", CellLength = "", CellWidth = "", Motility = "";
        public string MediumName = "";
        public string TempOptimum = "", TempMinimum = "", TempMaximum = "";
        public string PhOptimum = "";
        public string OxygenTolerance = "";
        public List<(string value, string activity, string ec)> Enzymes = new();
        public string GcContent = ""; public string GenomeAccession = "";
        public string Seq16sAccession = "", Seq16sLength = "";
        public List<(string title, string authors, string journal, string year, string pubmed, string doi)> Literature = new();
    }

    private static StrainData ExtractStrain(Json d)
    {
        var s = new StrainData();
        var gen = d["General"];
        var ntc = d["Name and taxonomic classification"];
        var cellM = d["Morphology"]["cell morphology"];
        var cul = d["Culture and growth conditions"];
        var phys = d["Physiology and metabolism"];
        var seq = d["Sequence information"];

        s.BacdiveId = gen["BacDive-ID"].StrOr("");
        s.DsmNumber = gen["DSM-Number"].StrOr("");
        s.FullName = ntc["full scientific name"].StrOr("");
        s.StrainDesignation = ntc["strain designation"].StrOr("");
        s.TypeStrain = ntc["type strain"].StrOr("");
        s.Description = gen["description"].StrOr("");
        s.NcbiTaxId = gen["NCBI tax id"]["NCBI tax id"].StrOr("");

        s.Domain = ntc["domain"].StrOr("");
        s.Phylum = ntc["phylum"].StrOr("");
        s.Class = ntc["class"].StrOr("");
        s.Order = ntc["order"].StrOr("");
        s.Family = ntc["family"].StrOr("");
        s.Genus = ntc["genus"].StrOr("");
        s.Species = ntc["species"].StrOr("");

        s.GramStain = cellM["gram stain"].StrOr("");
        s.CellShape = cellM["cell shape"].StrOr("");
        s.CellLength = cellM["cell length"].StrOr("");
        s.CellWidth = cellM["cell width"].StrOr("");
        s.Motility = cellM["motility"].StrOr("");

        foreach (var t in cul["culture temp"].Array)
        {
            var type = t["type"].StrOr("");
            var temp = t["temperature"].StrOr("");
            if (type == "optimum") s.TempOptimum = temp;
            else if (type == "minimum") s.TempMinimum = temp;
            else if (type == "maximum") s.TempMaximum = temp;
        }
        foreach (var p in cul["culture pH"].Array)
        {
            var type = p["type"].StrOr("");
            var ph = p["pH"].StrOr("");
            if (type == "optimum") s.PhOptimum = ph;
        }
        s.MediumName = cul["culture medium"]["name"].StrOr("");
        s.OxygenTolerance = phys["oxygen tolerance"]["oxygen tolerance"].StrOr("");

        s.Enzymes = phys["enzymes"].Array.Take(60).Select(e =>
            (value: e["value"].StrOr(""), activity: e["activity"].StrOr(""), ec: e["ec"].StrOr(""))).ToList();

        s.GcContent = seq["GC content"]["GC-content"].StrOr("");
        s.GenomeAccession = seq["Genome sequences"]["INSDC accession"].StrOr("");
        s.Seq16sAccession = seq["16S sequences"]["accession"].StrOr("");
        s.Seq16sLength = seq["16S sequences"]["length"].StrOr("");

        s.Literature = d["Literature"].Array.Take(15).Select(l =>
            (title: l["title"].StrOr(""), authors: l["authors"].StrOr(""), journal: l["journal"].StrOr(""),
             year: l["year"].StrOr(""), pubmed: l["Pubmed-ID"].StrOr(""), doi: l["DOI"].StrOr(""))).ToList();

        return s;
    }
}
