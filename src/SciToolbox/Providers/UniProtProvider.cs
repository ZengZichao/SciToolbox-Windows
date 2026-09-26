using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>UniProt 蛋白质数据库 — 直连 REST API（对应 UniProtProvider.swift）。</summary>
public sealed class UniProtProvider : ToolProviderBase
{
    public override string Id => "uniprot";
    public override string Name => DT.T("UniProt 蛋白质", "UniProt Proteins");
    public override ToolCategory Category => ToolCategory.Protein;
    public override string IconName => "atom";
    public override string Placeholder => DT.T("蛋白名 / 基因名 / Accession", "Protein / Gene / Accession");
    public override string DataSourceNote => DT.T("数据来源：UniProt REST API (rest.uniprot.org)", "Data source: UniProt REST API (rest.uniprot.org)");

    private const string BaseUrl = "https://rest.uniprot.org/uniprotkb";

    public override Task<SearchResult> SearchAsync(string query, CancellationToken ct = default) =>
        SearchAsync(query, 0, null, ct);

    public override async Task<SearchResult> SearchAsync(string query, int offset, string? pickerId, CancellationToken ct = default)
    {
        const int pageSize = 30;
        const string fields = "accession,id,gene_names,protein_name,organism_name,length,reviewed";
        var url = Urls.BuildQueryURL(BaseUrl + "/search", new Dictionary<string, string>
        {
            ["query"] = query, ["format"] = "json", ["size"] = pageSize.ToString(),
            ["offset"] = offset.ToString(), ["fields"] = fields
        });
        var json = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var items = new List<ResultItem>();
        foreach (var r in json["results"].Array)
        {
            var accession = r["primaryAccession"].StrOr("");
            var pName = ProteinName(r);
            var geneNames = r["genes"].Array.Select(g => g["geneName"]["value"].StrOr("")).Where(s => s.Length > 0).ToList();
            var organism = r["organism"]["scientificName"].StrOr("");
            var length = r["sequence"]["length"].IntOr(0);
            var reviewed = IsReviewed(r);

            items.Add(new ResultItem
            {
                Id = accession,
                Title = pName.Length == 0 ? accession : pName,
                Subtitle = geneNames.Count == 0 ? organism : string.Join(", ", geneNames) + " · " + organism,
                Badge = accession,
                Meta = (reviewed ? DT.T("已审核", "Reviewed") : DT.T("未审核", "Unreviewed")) + " · " + length + " aa",
                Extra = new Dictionary<string, string> { ["accession"] = accession }
            });
        }

        var (total, hasMore) = ProviderHelpers.Paginate(items.Count, pageSize);
        return new SearchResult { Items = items, Total = total, HasMore = hasMore };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var accession = context != null && context.TryGetValue("accession", out var a) ? a : id;
        var url = BaseUrl + "/" + Urls.EncodePath(accession) + "?format=json";
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var funcText = "";
        foreach (var c in data["comments"].Array)
            if (c["commentType"].StrOr("") == "FUNCTION")
                funcText = string.Join(" ", c["texts"].Array.Select(t => t["value"].StrOr("")).Where(s => s.Length > 0));

        var domains = new List<(string type, string id, string name, XLink? xlink)>();
        var xlinks = new List<XLink>();

        foreach (var d in data["dbReferences"].Array)
        {
            var type = d["type"].StrOr("");
            if (type == "GO")
            {
                var term = d["properties"]["term"].StrOr("");
                var sep = term.IndexOf('!');
                if (sep >= 0)
                {
                    var goId = term.Substring(0, sep);
                    var goName = term.Substring(sep + 1).Trim();
                    xlinks.Add(ProviderHelpers.Xlink("go", goId, "GO: " + (goName.Length == 0 ? goId : goName)));
                }
            }
            else if (type == "Pfam" || type == "InterPro")
            {
                var pfId = d["id"].StrOr("");
                var nm = d["properties"]["entry name"].StrOr(d["properties"]["entryName"].StrOr(""));
                XLink? xlink = type == "Pfam" ? ProviderHelpers.Xlink("pfam", pfId, "Pfam: " + (nm.Length == 0 ? pfId : nm)) : null;
                domains.Add((type, pfId, nm, xlink));
                if (type == "Pfam")
                    xlinks.Add(ProviderHelpers.Xlink("pfam", pfId, "Pfam: " + (nm.Length == 0 ? pfId : nm)));
            }
            else if (type == "PDB")
            {
                var pdbId = d["id"].StrOr("");
                if (pdbId.Length > 0) xlinks.Add(ProviderHelpers.Xlink("pdb", pdbId, $"PDB: {pdbId}"));
            }
            else if (type == "PubMed")
            {
                var pmid = d["id"].StrOr("");
                if (pmid.Length > 0) xlinks.Add(ProviderHelpers.Xlink("pubmed", pmid, $"PubMed: {pmid}"));
            }
            else if (type == "Ensembl")
            {
                var ensId = d["id"].StrOr("");
                if (ensId.Length > 0) xlinks.Add(ProviderHelpers.Xlink("ensembl", ensId, $"Ensembl: {ensId}"));
            }
            else if (type == "AlphaFoldDB")
            {
                var afId = d["id"].StrOr("");
                if (afId.Length > 0) xlinks.Add(ProviderHelpers.Xlink("alphafold", afId, $"AlphaFold: {afId}"));
            }
        }

        var pName2 = ProteinName(data);
        var geneNames2 = data["genes"].Array.Select(g => g["geneName"]["value"].StrOr("")).Where(s => s.Length > 0).ToList();
        var organism2 = data["organism"]["scientificName"].StrOr("");
        var lineage = data["organism"]["lineage"].Array.Select(x => x.StrOr("")).Where(s => s.Length > 0).ToList();
        var length2 = data["sequence"]["length"].IntOr(0);
        var sequence = data["sequence"]["value"].StrOr("");
        var reviewed2 = IsReviewed(data);
        var entryType = data["entryType"].StrOr("");

        var sections = new List<KVSection>
        {
            new(DT.T("基本信息", "Basic Info"), new List<KVRow>
            {
                new("Accession", accession, copyable: true, link: "https://www.uniprot.org/uniprotkb/" + accession),
                new(DT.T("类型", "Type"), entryType),
                new(DT.T("审核状态", "Review Status"), reviewed2 ? DT.T("已审核 (Swiss-Prot)", "Reviewed (Swiss-Prot)") : DT.T("未审核 (TrEMBL)", "Unreviewed (TrEMBL)")),
                new(DT.T("蛋白名称", "Protein Name"), pName2),
                new(DT.T("基因名", "Gene Name"), string.Join(", ", geneNames2)),
                new(DT.T("物种", "Organism"), organism2),
                new(DT.T("序列长度", "Sequence Length"), length2 + " aa")
            })
        };

        if (lineage.Count > 0)
            sections.Add(new KVSection(DT.T("分类谱系", "Lineage"),
                lineage.Select((name, i) => new KVRow((i + 1).ToString(), name)).ToList()));

        if (domains.Count > 0)
            sections.Add(new KVSection(DT.T("结构域", "Domains"),
                domains.Select(dm => new KVRow(dm.type, dm.id + " — " + dm.name, copyable: true, xlinkTarget: dm.xlink)).ToList()));

        var freeTexts = new List<FreeTextBlock>();
        if (funcText.Length > 0)
            freeTexts.Add(new FreeTextBlock(DT.T("功能", "Function"), funcText, copyable: true));

        var actions = new List<DetailAction>();
        if (sequence.Length > 0)
        {
            actions.Add(new DetailAction(DT.T("复制序列", "Copy Sequence"), sequence, DetailAction.ActionStyleKind.Primary));
            var fastaHeader = accession + " " + pName2 + " " + organism2;
            actions.Add(new DetailAction(DT.T("复制 FASTA", "Copy FASTA"), ExportUtil.Fasta(fastaHeader, sequence), DetailAction.ActionStyleKind.Secondary));
        }

        return new DetailModel
        {
            HeaderTitle = pName2.Length == 0 ? accession : pName2,
            HeaderSubtitle = accession,
            HeaderMeta = new List<string> { reviewed2 ? DT.T("已审核", "Reviewed") : DT.T("未审核", "Unreviewed"), organism2, length2 + " aa" },
            Sections = sections,
            FreeTextBlocks = freeTexts,
            Actions = actions,
            XLinks = xlinks,
            WebUrl = "https://www.uniprot.org/uniprotkb/" + accession
        };
    }

    private static bool IsReviewed(Json r)
    {
        var flag = r["reviewed"].Bool;
        if (flag.HasValue) return flag.Value;
        return (r["entryType"].StrOr("")).Contains("reviewed");
    }

    private static string ProteinName(Json r)
    {
        var pd = r["proteinDescription"];
        var rec = pd["recommendedName"]["fullName"]["value"].StrOr("");
        if (rec.Length > 0) return rec;
        var sub = pd["submissionNames"].Array.FirstOrDefault();
        if (sub != null) return sub["value"].StrOr("");
        var alt = pd["alternativeNames"].Array.FirstOrDefault();
        if (alt != null) return alt["fullName"]["value"].StrOr("");
        return "";
    }
}
