using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>NCBI 基因/序列检索 + 详情 + 关联序列（对应 NCBIGeneProvider.swift）。</summary>
public sealed class NcbiGeneProvider : ToolProviderBase
{
    public override string Id => "ncbi_gene";
    public override string Name => DT.T("NCBI 基因/序列", "NCBI Gene/Sequence");
    public override ToolCategory Category => ToolCategory.Gene;
    public override string IconName => "flask";
    public override string Placeholder => DT.T("基因符号（如 TP53）", "Gene symbol (e.g. TP53)");
    public override string DataSourceNote => DT.T("数据来源：NCBI Gene (eutils.ncbi.nlm.nih.gov)", "Data source: NCBI Gene (eutils.ncbi.nlm.nih.gov)");

    private const string Base = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        if (query.Length == 0 || !(char.IsLetter(query[0]) || char.IsDigit(query[0])))
            throw ApiException.InvalidInput(DT.T("请输入合法基因符号（以字母或数字开头）", "Enter a valid gene symbol (starting with a letter or digit)"));

        var term = $"{query}[Gene Name] OR {query}[Gene Synonym]";
        var searchUrl = BuildUrl("esearch", new Dictionary<string, string>
        {
            ["db"] = "gene", ["term"] = term, ["retmode"] = "json", ["retmax"] = "20"
        });
        var searchResult = await ApiClient.Shared.GetJsonAsync(searchUrl, ct: ct);
        var ids = searchResult["esearchresult"]["idlist"].Array.Select(x => x.StrOr("")).Where(s => s.Length > 0).ToList();
        if (ids.Count == 0) return new SearchResult { Items = new(), Total = null };

        var sumUrl = BuildUrl("esummary", new Dictionary<string, string>
        {
            ["db"] = "gene", ["id"] = string.Join(",", ids), ["retmode"] = "json"
        });
        var sum = await ApiClient.Shared.GetJsonAsync(sumUrl, ct: ct);
        var map = sum["result"];

        var items = new List<ResultItem>();
        foreach (var id in ids)
        {
            var g = map[id];
            var name = g["name"].StrOr("");
            var desc = g["description"].StrOr("");
            var organism = g["organism"]["scientificname"].StrOr("");
            var giArr = g["genomicinfo"].Array;
            var chromosome = FirstNonEmpty(g["chromosome"].StrOr(""), giArr.Count > 0 ? giArr[0]["chr"].StrOr("") : "");

            items.Add(new ResultItem
            {
                Id = id,
                Title = name.Length == 0 ? id : $"{name} — {desc}",
                Subtitle = organism,
                Badge = id,
                Meta = chromosome.Length == 0 ? null : $"Chr {chromosome}",
                Extra = new Dictionary<string, string> { ["uid"] = id, ["name"] = name }
            });
        }
        return new SearchResult { Items = items, Total = null };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var uid = context != null && context.TryGetValue("uid", out var u) ? u : id;
        var sumUrl = BuildUrl("esummary", new Dictionary<string, string>
        {
            ["db"] = "gene", ["id"] = uid, ["retmode"] = "json"
        });
        var sum = await ApiClient.Shared.GetJsonAsync(sumUrl, ct: ct);
        var g = sum["result"][uid];

        if (g["name"].String == null)
            throw ApiException.NotFound(DT.T("未找到该基因", "Gene not found"));

        var name = g["name"].StrOr("");
        var desc = g["description"].StrOr("");
        var organism = g["organism"]["scientificname"].StrOr("");
        var taxid = g["organism"]["taxid"].IntOr(0).ToString();
        var chromosome = g["chromosome"].StrOr("");
        var mapLocation = g["maplocation"].StrOr("");
        var summary = g["summary"].StrOr("");
        var aliases = (g["otheraliases"].StrOr("")).Split(',')
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        var rows = new List<KVRow>
        {
            new("Gene ID", uid, copyable: true),
            new(DT.T("符号", "Symbol"), name, copyable: true),
            new(DT.T("描述", "Description"), desc),
            new(DT.T("物种", "Organism"), organism),
        };
        if (chromosome.Length > 0) rows.Add(new KVRow(DT.T("染色体", "Chromosome"), chromosome));
        if (mapLocation.Length > 0) rows.Add(new KVRow(DT.T("图谱位置", "Map Location"), mapLocation));
        if (aliases.Count > 0) rows.Add(new KVRow(DT.T("别名", "Aliases"), string.Join(", ", aliases)));

        var genomicInfo = g["genomicinfo"].Array;
        for (int i = 0; i < genomicInfo.Count; i++)
        {
            var gi = genomicInfo[i];
            var chr = gi["chr"].StrOr("");
            var start = gi["chrstart"].IntOr(0);
            var stop = gi["chrstop"].IntOr(0);
            var orient = gi["orientation"].StrOr("");
            rows.Add(new KVRow(DT.T($"基因组{i + 1}", $"Genomic {i + 1}"), $"{chr}: {start}-{stop} ({orient})"));
        }

        var seqRows = new List<KVRow>();
        var nuccore = await SafeSeqList("nuccore", uid, ct);
        var protein = await SafeSeqList("protein", uid, ct);
        foreach (var s in nuccore.Take(5))
            seqRows.Add(new KVRow(s.Accession, $"{s.Title} ({s.Length} bp)", copyable: true));
        foreach (var s in protein.Take(5))
            seqRows.Add(new KVRow(s.Accession, $"{s.Title} ({s.Length} aa)", copyable: true));

        var sections = new List<KVSection> { new(DT.T("基因信息", "Gene Info"), rows) };
        if (seqRows.Count > 0) sections.Add(new KVSection(DT.T("关联序列", "Related Sequences"), seqRows));

        var xlinks = new List<XLink>();
        if (taxid.Length > 0 && taxid != "0")
            xlinks.Add(new XLink("ncbi_taxonomy", taxid, DT.T($"NCBI 分类: {organism}", $"NCBI Taxonomy: {organism}")));
        xlinks.Add(new XLink("ensembl", name, $"Ensembl: {name}"));

        var freeTexts = new List<FreeTextBlock>();
        if (summary.Length > 0)
            freeTexts.Add(new FreeTextBlock(DT.T("基因摘要", "Gene Summary"), summary, copyable: true));

        var headerMeta = new List<string>();
        if (chromosome.Length > 0) headerMeta.Add($"Chr {chromosome}");
        if (mapLocation.Length > 0) headerMeta.Add(mapLocation);

        return new DetailModel
        {
            HeaderTitle = $"{name} — {desc}",
            HeaderSubtitle = organism,
            HeaderMeta = headerMeta,
            Sections = sections,
            FreeTextBlocks = freeTexts,
            Actions = new List<DetailAction> { new(DT.T("复制 Gene ID", "Copy Gene ID"), uid, DetailAction.ActionStyleKind.Secondary) },
            XLinks = xlinks,
            WebUrl = $"https://www.ncbi.nlm.nih.gov/gene/{uid}"
        };
    }

    private sealed class SeqSummary
    {
        public string Accession = ""; public string Title = ""; public int Length;
    }

    private async Task<List<SeqSummary>> SafeSeqList(string db, string uid, CancellationToken ct)
    {
        try { return await FetchSeqList(db, uid, ct); } catch { return new List<SeqSummary>(); }
    }

    private async Task<List<SeqSummary>> FetchSeqList(string db, string uid, CancellationToken ct)
    {
        var term = $"{uid}[GeneID]";
        var searchUrl = BuildUrl("esearch", new Dictionary<string, string>
        {
            ["db"] = db, ["term"] = term, ["retmode"] = "json", ["retmax"] = "15"
        });
        var s = await ApiClient.Shared.GetJsonAsync(searchUrl, ct: ct);
        var ids = s["esearchresult"]["idlist"].Array.Select(x => x.StrOr("")).Where(x => x.Length > 0).ToList();
        if (ids.Count == 0) return new List<SeqSummary>();

        var sumUrl = BuildUrl("esummary", new Dictionary<string, string>
        {
            ["db"] = db, ["id"] = string.Join(",", ids), ["retmode"] = "json"
        });
        var sum = await ApiClient.Shared.GetJsonAsync(sumUrl, ct: ct);
        var map = sum["result"];

        var list = new List<SeqSummary>();
        foreach (var id in ids)
        {
            var x = map[id];
            list.Add(new SeqSummary
            {
                Accession = FirstNonEmpty(x["accession"].StrOr(""), x["caption"].StrOr(""), id),
                Title = FirstNonEmpty(x["title"].StrOr(""), x["caption"].StrOr("")),
                Length = x["slen"].IntOr(0)
            });
        }
        return list;
    }

    private static string BuildUrl(string tool, Dictionary<string, string> parameters) =>
        Urls.BuildQueryURL($"{Base}/{tool}.fcgi", parameters);

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
