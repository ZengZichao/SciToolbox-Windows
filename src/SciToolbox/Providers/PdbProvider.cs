using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>RCSB PDB 结构数据库 — 直连 API（对应 PDBProvider.swift）。</summary>
public sealed class PdbProvider : ToolProviderBase
{
    public override string Id => "pdb";
    public override string Name => DT.T("RCSB PDB 结构", "RCSB PDB Structure");
    public override ToolCategory Category => ToolCategory.Protein;
    public override string IconName => "cube";
    public override string Placeholder => DT.T("关键词或 PDB ID", "Keyword or PDB ID");
    public override string DataSourceNote => DT.T("数据来源：RCSB PDB (data.rcsb.org)", "Data source: RCSB PDB (data.rcsb.org)");

    private const string SearchUrl = "https://search.rcsb.org/rcsbsearch/v2/query";
    private const string EntryUrl = "https://data.rcsb.org/rest/v1/core/entry";

    public override Task<SearchResult> SearchAsync(string query, CancellationToken ct = default) =>
        SearchAsync(query, 0, null, ct);

    public override async Task<SearchResult> SearchAsync(string query, int offset, string? pickerId, CancellationToken ct = default)
    {
        if (query.Length < 2)
            throw ApiException.InvalidInput(DT.T("检索词至少 2 个字符", "Search term must be at least 2 characters"));

        const int pageSize = 20;
        var body = new Dictionary<string, object>
        {
            ["query"] = new Dictionary<string, object>
            {
                ["type"] = "terminal",
                ["service"] = "full_text",
                ["parameters"] = new Dictionary<string, object> { ["value"] = query }
            },
            ["return_type"] = "entry",
            ["request_options"] = new Dictionary<string, object>
            {
                ["paginate"] = new Dictionary<string, object> { ["start"] = offset, ["rows"] = pageSize }
            }
        };

        var sres = await ApiClient.Shared.PostJsonAsync(SearchUrl, body, ct: ct);
        var ids = sres["result_set"].Array.Select(x => x["identifier"].StrOr("")).Where(s => s.Length > 0).ToList();
        var total = sres["total_count"].Int ?? ids.Count;

        // full_text 搜不到但查询本身像 4 位 PDB ID 时，直接按 ID 取 entry（兑现「关键词或 PDB ID」）
        if (ids.Count == 0 && offset == 0 && System.Text.RegularExpressions.Regex.IsMatch(query.Trim(), @"^[0-9A-Za-z]{4}$"))
            ids = new List<string> { query.Trim().ToUpperInvariant() };

        if (ids.Count == 0) return new SearchResult { Items = new(), Total = 0 };

        // 并发获取每个 entry 的摘要（保持原顺序）
        var tasks = ids.Select(async id =>
        {
            try
            {
                var entry = await FetchEntryAsync(id, ct);
                return new ResultItem
                {
                    Id = id,
                    Title = entry.Title,
                    Subtitle = entry.Descriptor,
                    Badge = id,
                    Meta = entry.Method + (entry.Resolution.Length == 0 ? "" : DT.T($" · 分辨率 {entry.Resolution} Å", $" · Resolution {entry.Resolution} Å")),
                    Extra = new Dictionary<string, string> { ["id"] = id }
                };
            }
            catch { return null; }
        });
        var results = await Task.WhenAll(tasks);
        var items = results.Where(x => x != null).Select(x => x!).ToList();

        return new SearchResult { Items = items, Total = total, HasMore = offset + items.Count < total };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var pdbId = ((context != null && context.TryGetValue("id", out var c) ? c : id)).ToUpperInvariant();
        var entry = await FetchEntryAsync(pdbId, ct);

        var uniprotIds = new List<string>();
        try { uniprotIds = await FetchPolymerEntitiesAsync(pdbId, entry.EntityCount, ct); } catch { }

        var infoRows = new List<KVRow> { new(DT.T("实验方法", "Experimental method"), entry.Method) };
        if (entry.Resolution.Length > 0) infoRows.Add(new KVRow(DT.T("分辨率", "Resolution"), $"{entry.Resolution} Å"));
        if (entry.ReleaseDate.Length > 0) infoRows.Add(new KVRow(DT.T("发布日期", "Release date"), entry.ReleaseDate));
        if (entry.MolecularWeight.Length > 0) infoRows.Add(new KVRow(DT.T("分子量", "Molecular weight"), entry.MolecularWeight));
        infoRows.Add(new KVRow(DT.T("聚合物实体", "Polymer entities"), entry.EntityCount.ToString()));
        if (entry.MonomerCount > 0) infoRows.Add(new KVRow(DT.T("单体数", "Monomers"), entry.MonomerCount.ToString()));
        if (entry.PolymerComposition.Length > 0) infoRows.Add(new KVRow(DT.T("组成", "Composition"), entry.PolymerComposition));

        var citeRows = new List<KVRow>();
        if (entry.CitationTitle.Length > 0) citeRows.Add(new KVRow(DT.T("标题", "Title"), entry.CitationTitle));
        if (entry.Journal.Length > 0) citeRows.Add(new KVRow(DT.T("期刊", "Journal"), entry.Journal + (entry.Year.Length == 0 ? "" : DT.T($"（{entry.Year}）", $"({entry.Year})"))));
        if (entry.Authors.Count > 0) citeRows.Add(new KVRow(DT.T("作者", "Authors"), ProviderHelpers.FormatAuthors(entry.Authors), copyable: entry.Authors.Count > 3));
        if (entry.Pubmed.Length > 0)
            citeRows.Add(new KVRow("PubMed", entry.Pubmed, copyable: true, xlinkTarget: ProviderHelpers.Xlink("pubmed", entry.Pubmed, $"PubMed: {entry.Pubmed}")));
        if (entry.Doi.Length > 0) citeRows.Add(new KVRow("DOI", entry.Doi, copyable: true, link: ProviderHelpers.DoiUrl(entry.Doi)));

        var sections = new List<KVSection> { new(DT.T("结构信息", "Structure Info"), infoRows) };
        if (citeRows.Count > 0) sections.Add(new KVSection(DT.T("主要引用", "Primary Citation"), citeRows));

        var xlinks = new List<XLink>();
        if (entry.Pubmed.Length > 0) xlinks.Add(ProviderHelpers.Xlink("pubmed", entry.Pubmed, $"PubMed: {entry.Pubmed}"));
        foreach (var uid in uniprotIds.Take(5)) xlinks.Add(ProviderHelpers.Xlink("uniprot", uid, $"UniProt: {uid}"));

        var pdbLower = pdbId.ToLowerInvariant();
        var imageUrl = $"https://cdn.rcsb.org/images/structures/{pdbLower}_assembly-1.jpeg";

        var headerMeta = new List<string>();
        if (entry.Method.Length > 0) headerMeta.Add(entry.Method);
        if (entry.Resolution.Length > 0) headerMeta.Add($"{entry.Resolution} Å");

        return new DetailModel
        {
            HeaderTitle = entry.Title.Length == 0 ? pdbId : entry.Title,
            HeaderSubtitle = pdbId,
            HeaderMeta = headerMeta,
            Sections = sections,
            Actions = new List<DetailAction> { new(DT.T("复制 PDB ID", "Copy PDB ID"), pdbId, DetailAction.ActionStyleKind.Secondary) },
            XLinks = xlinks,
            WebUrl = entry.WebUrl,
            ImageUrl = imageUrl
        };
    }

    private sealed class PdbEntry
    {
        public string Id = ""; public string Title = ""; public string Descriptor = "";
        public string Method = ""; public string Resolution = ""; public List<string> Authors = new();
        public string CitationTitle = ""; public string Journal = ""; public string Year = "";
        public string Pubmed = ""; public string Doi = ""; public string ReleaseDate = "";
        public string MolecularWeight = ""; public int EntityCount; public int MonomerCount;
        public string PolymerComposition = ""; public string WebUrl = "";
    }

    private async Task<PdbEntry> FetchEntryAsync(string id, CancellationToken ct)
    {
        var url = $"{EntryUrl}/{Urls.EncodePath(id)}";
        var d = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var info = d["rcsb_entry_info"];
        var cit = d["rcsb_primary_citation"];
        var structt = d["struct"];
        var exptl = d["exptl"].Array;

        var methods = info["experimental_method"].StrOr("")
            .Length > 0 ? info["experimental_method"].StrOr("")
            : (exptl.Count > 0 ? exptl[0]["method"].StrOr("") : "");
        var resArr = info["resolution_combined"].Array;

        return new PdbEntry
        {
            Id = d["entry"]["id"].StrOr(id),
            Title = structt["title"].StrOr(""),
            Descriptor = structt["pdbx_descriptor"].StrOr(""),
            Method = methods,
            Resolution = resArr.Count > 0 ? resArr[0].StrOr("") : "",
            Authors = cit["rcsb_authors"].Array.Select(x => x.StrOr("")).Where(s => s.Length > 0).ToList(),
            CitationTitle = cit["title"].StrOr(""),
            Journal = cit["journal_abbrev"].StrOr(""),
            Year = cit["year"].StrOr(""),
            Pubmed = FirstNonEmpty(cit["pdbx_database_id_PubMed"].StrOr(""), cit["rcsb_database_id_PubMed"].StrOr("")),
            Doi = FirstNonEmpty(cit["pdbx_database_id_DOI"].StrOr(""), cit["rcsb_database_id_DOI"].StrOr("")),
            ReleaseDate = d["rcsb_accession_info"]["initial_release_date"].StrOr(""),
            MolecularWeight = info["molecular_weight"].StrOr(""),
            EntityCount = info["entity_count"].IntOr(0),
            MonomerCount = info["deposited_polymer_monomer_count"].IntOr(0),
            PolymerComposition = info["polymer_composition"].StrOr(""),
            WebUrl = $"https://www.rcsb.org/structure/{id}"
        };
    }

    private async Task<List<string>> FetchPolymerEntitiesAsync(string pdbId, int entityCount, CancellationToken ct)
    {
        if (entityCount <= 0) return new List<string>();
        var tasks = Enumerable.Range(1, entityCount).Select(async entityId =>
        {
            var url = $"https://data.rcsb.org/rest/v1/core/polymer_entity/{pdbId}/{entityId}";
            try
            {
                var d = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
                return d["rcsb_polymer_entity_container_identifiers"]["uniprot_ids"].Array
                    .Select(x => x.StrOr("")).Where(s => s.Length > 0).ToList();
            }
            catch { return new List<string>(); }
        });
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(x => x).Distinct().ToList();
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
