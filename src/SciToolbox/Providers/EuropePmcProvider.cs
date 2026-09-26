using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>Europe PMC 文献检索 — EBI REST API（对应 EuropePMCProvider.swift）。</summary>
public sealed class EuropePmcProvider : ToolProviderBase
{
    public override string Id => "europepmc";
    public override string Name => DT.T("Europe PMC 文献", "Europe PMC Literature");
    public override ToolCategory Category => ToolCategory.Literature;
    public override string IconName => "books.vertical";
    public override string Placeholder => DT.T("关键词 / DOI / PMID", "Keyword / DOI / PMID");
    public override string DataSourceNote => DT.T("数据来源：EBI Europe PMC (www.ebi.ac.uk/europepmc)", "Data source: EBI Europe PMC (www.ebi.ac.uk/europepmc)");

    private const string Base = "https://www.ebi.ac.uk/europepmc/webservices/rest";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        if (query.Length < 2)
            throw ApiException.InvalidInput(DT.T("检索词至少 2 个字符", "Query must be at least 2 characters"));

        var url = Urls.BuildQueryURL($"{Base}/search", new Dictionary<string, string>
        {
            ["query"] = query, ["format"] = "json", ["pageSize"] = "20", ["resultType"] = "core"
        });
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var items = new List<ResultItem>();
        foreach (var rec in data["resultList"]["result"].Array)
        {
            var id = rec["id"].StrOr("");
            var source = rec["source"].StrOr("");
            var pmid = rec["pmid"].StrOr("");
            var title = rec["title"].StrOr("");
            var authorString = rec["authorString"].StrOr("");
            var journal = FirstNonEmpty(rec["journalTitle"].StrOr(""), rec["journalInfo"]["journalTitle"].StrOr(""));
            var pubYear = FirstNonEmpty(rec["pubYear"].StrOr(""), rec["journalInfo"]["dateOfPublication"].StrOr(""));
            var citedBy = rec["citedByCount"].IntOr(0);
            var isOpenAccess = rec["isOpenAccess"].StrOr("") == "Y";

            var badge = pmid.Length == 0 ? $"{source}:{id}" : $"PMID: {pmid}";
            var meta = journal
                + (pubYear.Length == 0 ? "" : $" · {pubYear}")
                + (citedBy > 0 ? $" · {DT.T($"引用 {citedBy}", $"Cited {citedBy}")}" : "")
                + (isOpenAccess ? " · OA" : "");
            items.Add(new ResultItem
            {
                Id = $"{source}:{id}",
                Title = title.Length == 0 ? DT.T("（无标题）", "(No title)") : title,
                Subtitle = authorString,
                Badge = badge,
                Meta = meta,
                Extra = new Dictionary<string, string> { ["source"] = source, ["id"] = id, ["pmid"] = pmid }
            });
        }

        var total = data["hitCount"].Int ?? items.Count;
        return new SearchResult { Items = items, Total = total };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var source = context != null && context.TryGetValue("source", out var s) ? s : "MED";
        var articleId = context != null && context.TryGetValue("id", out var i) ? i : id;
        var url = $"{Base}/article/{Urls.EncodePath(source)}/{Urls.EncodePath(articleId)}?format=json";
        var resp = await ApiClient.Shared.GetJsonAsync(url, ct: ct);

        var result = resp["result"];
        var rec = result.IsNull ? resp : result;

        var realId = rec["id"].StrOr(articleId);
        var pmid = rec["pmid"].StrOr("");
        var doi = rec["doi"].StrOr("");
        var title = rec["title"].StrOr("");
        var authorString = rec["authorString"].StrOr("");
        var journal = FirstNonEmpty(rec["journalTitle"].StrOr(""), rec["journalInfo"]["journalTitle"].StrOr(""));
        var pubYear = FirstNonEmpty(rec["pubYear"].StrOr(""), rec["journalInfo"]["dateOfPublication"].StrOr(""));
        var pubType = rec["pubType"].StrOr("");
        var citedBy = rec["citedByCount"].IntOr(0);
        var isOpenAccess = rec["isOpenAccess"].StrOr("") == "Y";
        var abstractText = rec["abstractText"].StrOr("");

        var rows = new List<KVRow>();
        if (pmid.Length > 0) rows.Add(new KVRow("PMID", pmid, copyable: true));
        rows.Add(new KVRow("ID", $"{source}:{realId}", copyable: true));
        if (doi.Length > 0) rows.Add(new KVRow("DOI", doi, copyable: true));
        if (journal.Length > 0) rows.Add(new KVRow(DT.T("期刊", "Journal"), journal));
        if (pubYear.Length > 0) rows.Add(new KVRow(DT.T("发表年份", "Year"), pubYear));
        if (pubType.Length > 0) rows.Add(new KVRow(DT.T("类型", "Type"), pubType));
        if (authorString.Length > 0) rows.Add(new KVRow(DT.T("作者", "Authors"), authorString));
        if (citedBy > 0) rows.Add(new KVRow(DT.T("被引次数", "Cited By"), citedBy.ToString()));
        rows.Add(new KVRow(DT.T("开放获取", "Open Access"), isOpenAccess ? DT.T("是", "Yes") : DT.T("否", "No")));

        var sections = new List<KVSection> { new(DT.T("文献信息", "Article Info"), rows) };

        var freeTexts = new List<FreeTextBlock>();
        if (abstractText.Length > 0)
            freeTexts.Add(new FreeTextBlock(DT.T("摘要", "Abstract"), abstractText, copyable: true));

        var xlinks = new List<XLink>();
        if (pmid.Length > 0) xlinks.Add(new XLink("pubmed", pmid, $"PubMed: {pmid}"));
        if (doi.Length > 0) xlinks.Add(new XLink("pdb", doi, DT.T("按 DOI 搜索 PDB", "Search PDB by DOI")));

        var webUrl = pmid.Length > 0
            ? $"https://www.europepmc.org/article/MED/{pmid}"
            : $"https://www.europepmc.org/article/{source}/{realId}";

        var actions = new List<DetailAction> { new(DT.T("复制 ID", "Copy ID"), realId, DetailAction.ActionStyleKind.Secondary) };
        if (title.Length > 0)
        {
            var authorList = authorString.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList();
            var bib = ExportUtil.Bibtex(pmid, title, authorList, journal, pubYear);
            actions.Add(new DetailAction(DT.T("复制 BibTeX", "Copy BibTeX"), bib, DetailAction.ActionStyleKind.Secondary));
            actions.Add(new DetailAction(DT.T("导出 BibTeX", "Export BibTeX"), bib, DetailAction.ActionStyleKind.Secondary,
                DetailAction.ActionKind.Export, $"EuropePMC_{realId}", "bib"));
            var ris = ExportUtil.Ris(title, authorList, journal, pubYear, pmid, doi);
            actions.Add(new DetailAction(DT.T("复制 RIS", "Copy RIS"), ris, DetailAction.ActionStyleKind.Secondary));
            actions.Add(new DetailAction(DT.T("导出 RIS", "Export RIS"), ris, DetailAction.ActionStyleKind.Secondary,
                DetailAction.ActionKind.Export, $"EuropePMC_{realId}", "ris"));
        }

        var headerMeta = new List<string>();
        if (journal.Length > 0) headerMeta.Add(journal);
        if (pubYear.Length > 0) headerMeta.Add(pubYear);

        return new DetailModel
        {
            HeaderTitle = title.Length == 0 ? $"ID: {realId}" : title,
            HeaderSubtitle = authorString,
            HeaderMeta = headerMeta,
            Sections = sections,
            FreeTextBlocks = freeTexts,
            Actions = actions,
            XLinks = xlinks,
            WebUrl = webUrl
        };
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
