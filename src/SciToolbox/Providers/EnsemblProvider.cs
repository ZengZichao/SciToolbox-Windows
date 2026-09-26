using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>Ensembl 基因数据库 — 直连 REST API（对应 EnsemblProvider.swift）。</summary>
public sealed class EnsemblProvider : ToolProviderBase
{
    public override string Id => "ensembl";
    public override string Name => DT.T("Ensembl 基因", "Ensembl Gene");
    public override ToolCategory Category => ToolCategory.Gene;
    public override string IconName => "scope";
    public override string Placeholder => DT.T("基因符号（如 BRCA1）", "Gene symbol (e.g. BRCA1)");
    public override string DataSourceNote => DT.T("数据来源：Ensembl REST API (rest.ensembl.org)", "Data source: Ensembl REST API (rest.ensembl.org)");

    private const string Base = "https://rest.ensembl.org";

    public override Task<SearchResult> SearchAsync(string query, CancellationToken ct = default) =>
        SearchAsync(query, 0, null, ct);

    public override async Task<SearchResult> SearchAsync(string query, int offset, string? pickerId, CancellationToken ct = default)
    {
        const string species = "homo_sapiens";
        if (offset > 0) return new SearchResult { Items = new(), Total = 0 };
        var symbol = query.ToUpperInvariant();
        var url = $"{Base}/lookup/symbol/{species}/{Urls.EncodePath(symbol)}?content-type=application/json";

        Json g;
        try
        {
            g = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        }
        catch (ApiException ex) when (ex.Kind == ApiException.KindType.Http && (ex.StatusCode == 400 || ex.StatusCode == 404))
        {
            throw ApiException.NotFound(DT.T("未找到该基因（请确认物种与符号）", "Gene not found (check species and symbol)"));
        }

        if (g["id"].String == null)
            throw ApiException.NotFound(DT.T("未找到该基因（请确认物种与符号）", "Gene not found (check species and symbol)"));

        var geneId = g["id"].StrOr("");
        var displayName = FirstNonEmpty(g["display_name"].StrOr(""), symbol);
        var desc = g["description"].StrOr("");
        var organism = FirstNonEmpty(g["species"].StrOr(""), species);

        var item = new ResultItem
        {
            Id = geneId,
            Title = $"{displayName} — {desc}",
            Subtitle = organism,
            Badge = geneId,
            Meta = g["biotype"].StrOr(""),
            Extra = new Dictionary<string, string> { ["id"] = geneId, ["symbol"] = displayName }
        };
        return new SearchResult { Items = new List<ResultItem> { item }, Total = 1 };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var geneId = context != null && context.TryGetValue("id", out var i) ? i : id;

        var infoTask = FetchGeneInfoAsync(geneId, ct);
        var seqTask = SafeAsync(FetchSequenceAsync(geneId, ct));
        var xrefTask = SafeAsync(FetchXrefsAsync(geneId, ct));
        await Task.WhenAll(infoTask, seqTask, xrefTask);

        var info = infoTask.Result;
        var seq = seqTask.Result;
        var xrefs = xrefTask.Result;

        var rows = new List<KVRow>
        {
            new("Ensembl ID", info.Id, copyable: true),
            new(DT.T("符号", "Symbol"), info.DisplayName),
            new(DT.T("类型", "Type"), info.ObjectType),
            new("Biotype", info.Biotype),
        };
        if (info.Description.Length > 0) rows.Add(new KVRow(DT.T("描述", "Description"), info.Description));
        if (info.Source.Length > 0) rows.Add(new KVRow(DT.T("来源", "Source"), info.Source));
        if (info.AssemblyName.Length > 0) rows.Add(new KVRow(DT.T("组装", "Assembly"), info.AssemblyName));
        if (info.Version.Length > 0) rows.Add(new KVRow(DT.T("版本", "Version"), info.Version));
        if (info.SeqRegion.Length > 0) rows.Add(new KVRow(DT.T("染色体", "Chromosome"), info.SeqRegion));
        if (info.Start > 0) rows.Add(new KVRow(DT.T("位置", "Location"), $"{info.Start}-{info.End}"));
        if (info.Strand.Length > 0) rows.Add(new KVRow(DT.T("链", "Strand"), info.Strand));
        if (info.Length > 0) rows.Add(new KVRow(DT.T("长度", "Length"), $"{info.Length} bp"));

        var sections = new List<KVSection> { new(DT.T("基因信息", "Gene Info"), rows) };

        if (seq != null && seq.Seq.Length > 0)
        {
            var seqRows = new List<KVRow>();
            if (seq.Desc.Length > 0) seqRows.Add(new KVRow(DT.T("描述", "Description"), seq.Desc));
            seqRows.Add(new KVRow(DT.T("分子类型", "Molecule Type"), seq.Moltype));
            seqRows.Add(new KVRow(DT.T("长度", "Length"), $"{seq.Length} bp{(seq.Truncated ? DT.T(" (已截断)", " (truncated)") : "")}"));
            sections.Add(new KVSection(DT.T("序列", "Sequence"), seqRows));
        }

        if (xrefs != null && xrefs.Count > 0)
        {
            var xrefRows = xrefs.Take(20).Select(x =>
                new KVRow(x.DbName, $"{x.Id}{(x.Description.Length == 0 ? "" : " — " + x.Description)}", copyable: true)).ToList();
            sections.Add(new KVSection(DT.T("交叉引用", "Cross References"), xrefRows));
        }

        var xlinkList = new List<XLink>();
        if (xrefs != null)
            foreach (var x in xrefs)
            {
                var db = x.DbName.ToLowerInvariant();
                if (db.Contains("uniprot")) xlinkList.Add(new XLink("uniprot", x.Id, $"UniProt: {x.Id}"));
                else if (db.Contains("pubmed")) xlinkList.Add(new XLink("pubmed", x.Id, $"PubMed: {x.Id}"));
            }

        var actions = new List<DetailAction>();
        if (seq != null && seq.Seq.Length > 0)
        {
            var header = $">{info.DisplayName} {seq.Desc}";
            actions.Add(new DetailAction(DT.T("复制序列", "Copy Sequence"), seq.Seq, DetailAction.ActionStyleKind.Primary));
            actions.Add(new DetailAction(DT.T("复制 FASTA", "Copy FASTA"), ExportUtil.Fasta(header, seq.Seq), DetailAction.ActionStyleKind.Secondary));
        }

        var headerMeta = new List<string>();
        if (info.Biotype.Length > 0) headerMeta.Add(info.Biotype);
        if (info.Organism.Length > 0) headerMeta.Add(info.Organism);

        return new DetailModel
        {
            HeaderTitle = $"{info.DisplayName} — {info.Description}",
            HeaderSubtitle = info.Id,
            HeaderMeta = headerMeta,
            Sections = sections,
            Actions = actions,
            XLinks = xlinkList,
            WebUrl = $"https://www.ensembl.org/{info.Organism}/Gene/Summary?g={info.Id}"
        };
    }

    private sealed class GeneInfo
    {
        public string Id = ""; public string DisplayName = ""; public string Organism = "";
        public string ObjectType = ""; public string Biotype = ""; public string Description = "";
        public string Source = ""; public string AssemblyName = ""; public string Version = "";
        public string SeqRegion = ""; public int Start; public int End; public string Strand = ""; public int Length;
    }
    private sealed class SeqInfo { public string Seq = ""; public string Desc = ""; public string Moltype = ""; public int Length; public bool Truncated; }
    private sealed class XrefInfo { public string DbName = ""; public string Id = ""; public string Description = ""; }

    private async Task<GeneInfo> FetchGeneInfoAsync(string id, CancellationToken ct)
    {
        var url = $"{Base}/lookup/id/{Urls.EncodePath(id)}?content-type=application/json";
        var g = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        var loc = g["location"].Dict.Count == 0 ? g : g["location"];
        var start = loc["start"].IntOr(0);
        var end = loc["end"].IntOr(0);
        var strandInt = loc["strand"].IntOr(0);
        return new GeneInfo
        {
            Id = FirstNonEmpty(g["id"].StrOr(""), id),
            DisplayName = g["display_name"].StrOr(""),
            Organism = g["species"].StrOr(""),
            ObjectType = g["object_type"].StrOr(""),
            Biotype = g["biotype"].StrOr(""),
            Description = g["description"].StrOr(""),
            Source = g["source"].StrOr(""),
            AssemblyName = g["assembly_name"].StrOr(""),
            Version = g["version"].StrOr(""),
            SeqRegion = loc["seq_region_name"].StrOr(""),
            Start = start,
            End = end,
            Strand = strandInt == 0 ? "" : (strandInt > 0 ? DT.T("正链 (+)", "Plus strand (+)") : DT.T("反链 (−)", "Minus strand (−)")),
            Length = (start > 0 && end > 0) ? Math.Abs(end - start) + 1 : 0
        };
    }

    private async Task<SeqInfo> FetchSequenceAsync(string id, CancellationToken ct)
    {
        var url = $"{Base}/sequence/id/{Urls.EncodePath(id)}?content-type=application/json";
        var j = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        var seq = j["seq"].StrOr("");
        const int maxLen = 60000;
        bool truncated = false;
        if (seq.Length > maxLen) { seq = seq.Substring(0, maxLen); truncated = true; }
        return new SeqInfo
        {
            Seq = seq,
            Desc = j["desc"].StrOr(""),
            Moltype = j["moltype"].StrOr(""),
            Length = seq.Length,
            Truncated = truncated
        };
    }

    private async Task<List<XrefInfo>> FetchXrefsAsync(string id, CancellationToken ct)
    {
        var url = $"{Base}/xrefs/id/{Urls.EncodePath(id)}?content-type=application/json";
        var arr = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        var list = new List<XrefInfo>();
        foreach (var x in arr.Array)
        {
            var dbName = x["db_display_name"].StrOr("");
            var displayId = x["display_id"].StrOr("");
            if (dbName.Length == 0 || displayId.Length == 0) continue;
            list.Add(new XrefInfo { DbName = dbName, Id = displayId, Description = x["description"].StrOr("") });
        }
        return list;
    }

    private static async Task<T?> SafeAsync<T>(Task<T> task)
    {
        try { return await task; } catch { return default; }
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
