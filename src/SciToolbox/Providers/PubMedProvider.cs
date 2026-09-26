using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>PubMed 文献检索，经 NCBI E-utilities（对应 PubMedProvider.swift）。</summary>
public sealed class PubMedProvider : ToolProviderBase
{
    public override string Id => "pubmed";
    public override string Name => DT.T("PubMed 文献", "PubMed Literature");
    public override ToolCategory Category => ToolCategory.Literature;
    public override string IconName => "doc.text";
    public override string Placeholder => DT.T("关键词 / PMID / DOI", "Keyword / PMID / DOI");
    public override string DataSourceNote => DT.T("数据来源：NCBI PubMed (eutils.ncbi.nlm.nih.gov)", "Data source: NCBI PubMed (eutils.ncbi.nlm.nih.gov)");

    public override IReadOnlyList<PickerOption> PickerOptions { get; } = new List<PickerOption>
    {
        new("all", DT.T("全部时间", "All time"), "cancer"),
        new("5y", DT.T("近5年", "Past 5y"), "CRISPR"),
        new("10y", DT.T("近10年", "Past 10y"), "p53"),
        new("2y", DT.T("近2年", "Past 2y"), "SARS-CoV-2"),
    };
    public override string? DefaultPickerId => "all";

    private const string Base = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils";

    public override Task<SearchResult> SearchAsync(string query, CancellationToken ct = default) =>
        SearchAsync(query, 0, null, ct);

    public override async Task<SearchResult> SearchAsync(string query, int offset, string? pickerId, CancellationToken ct = default)
    {
        const int pageSize = 20;
        var yearFilter = BuildYearFilter(pickerId ?? DefaultPickerId ?? "all");
        var fullQuery = yearFilter.Length == 0 ? query : $"{query} AND {yearFilter}";

        var searchUrl = BuildUrl("esearch", new Dictionary<string, string>
        {
            ["db"] = "pubmed", ["term"] = fullQuery, ["retmode"] = "json",
            ["retmax"] = pageSize.ToString(), ["retstart"] = offset.ToString()
        });
        var searchResult = await ApiClient.Shared.GetJsonAsync(searchUrl, ct: ct);
        var ids = searchResult["esearchresult"]["idlist"].Array.Select(x => x.StrOr("")).Where(s => s.Length > 0).ToList();
        var total = searchResult["esearchresult"]["count"].Int ?? ids.Count;

        if (ids.Count == 0) return new SearchResult { Items = new(), Total = 0 };

        var sumUrl = BuildUrl("esummary", new Dictionary<string, string>
        {
            ["db"] = "pubmed", ["id"] = string.Join(",", ids), ["retmode"] = "json"
        });
        var sum = await ApiClient.Shared.GetJsonAsync(sumUrl, ct: ct);
        var map = sum["result"];

        var items = new List<ResultItem>();
        foreach (var id in ids)
        {
            var a = map[id];
            var title = a["title"].StrOr("");
            var authors = a["authors"].Array.Select(x => x["name"].StrOr("")).Where(s => s.Length > 0).ToList();
            var journal = FirstNonEmpty(a["fulljournalname"].StrOr(""), a["source"].StrOr(""));
            var pubdate = a["pubdate"].StrOr("");

            var authorPrefix = string.Join(", ", authors.Take(3)) + (authors.Count > 3 ? DT.T(" 等", " et al.") : "");
            items.Add(new ResultItem
            {
                Id = id,
                Title = title.Length == 0 ? DT.T("（无标题）", "(No title)") : title,
                Subtitle = authors.Count == 0 ? journal : authorPrefix,
                Badge = $"PMID: {id}",
                Meta = journal + (pubdate.Length == 0 ? "" : $" · {pubdate}"),
                Extra = new Dictionary<string, string> { ["pmid"] = id }
            });
        }
        return new SearchResult { Items = items, Total = total, HasMore = offset + items.Count < total };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var pmid = context != null && context.TryGetValue("pmid", out var p) ? p : id;

        var abstractUrl = BuildUrl("efetch", new Dictionary<string, string>
        {
            ["db"] = "pubmed", ["id"] = pmid, ["retmode"] = "text", ["rettype"] = "abstract"
        });
        var abstractText = await ApiClient.Shared.GetTextAsync(abstractUrl, ct);

        var sumUrl = BuildUrl("esummary", new Dictionary<string, string>
        {
            ["db"] = "pubmed", ["id"] = pmid, ["retmode"] = "json"
        });
        var sum = await ApiClient.Shared.GetJsonAsync(sumUrl, ct: ct);
        var a = sum["result"][pmid];

        var title = a["title"].StrOr("");
        var authors = a["authors"].Array.Select(x => x["name"].StrOr("")).Where(s => s.Length > 0).ToList();
        var journal = FirstNonEmpty(a["fulljournalname"].StrOr(""), a["source"].StrOr(""));
        var pubdate = a["pubdate"].StrOr("");
        var volume = a["volume"].StrOr("");
        var issue = a["issue"].StrOr("");
        var pages = a["pages"].StrOr("");
        var doi = a["doi"].StrOr("");
        var pubtypes = a["pubtype"].Array.Select(x => x.StrOr("")).Where(s => s.Length > 0).ToList();

        var rows = new List<KVRow>
        {
            new("PMID", pmid, copyable: true),
            new("DOI", doi, copyable: true, link: ProviderHelpers.DoiUrl(doi)),
            new(DT.T("作者", "Authors"), ProviderHelpers.FormatAuthors(authors), copyable: authors.Count > 3),
            new(DT.T("期刊", "Journal"), journal),
            new(DT.T("发表日期", "Published"), pubdate),
        };
        if (volume.Length > 0 || issue.Length > 0 || pages.Length > 0)
            rows.Add(new KVRow(DT.T("卷期页", "Volume/Issue/Pages"), $"{volume}({issue}): {pages}"));
        if (pubtypes.Count > 0)
            rows.Add(new KVRow(DT.T("类型", "Type"), string.Join(", ", pubtypes)));

        var xlinks = new List<XLink>();
        if (doi.Length > 0) xlinks.Add(ProviderHelpers.Xlink("europepmc", doi, $"Europe PMC: {doi}"));

        var freeTexts = new List<FreeTextBlock>();
        if (abstractText.Trim().Length > 0)
            freeTexts.Add(new FreeTextBlock(DT.T("摘要", "Abstract"), abstractText.Trim(), copyable: true));

        var actions = new List<DetailAction>();
        if (authors.Count > 0)
        {
            var bib = ExportUtil.Bibtex(pmid, title, authors, journal, pubdate);
            actions.Add(new DetailAction(DT.T("复制 BibTeX", "Copy BibTeX"), bib, DetailAction.ActionStyleKind.Secondary));
            actions.Add(new DetailAction(DT.T("导出 BibTeX", "Export BibTeX"), bib, DetailAction.ActionStyleKind.Secondary,
                DetailAction.ActionKind.Export, $"PMID_{pmid}", "bib"));
            var ris = ExportUtil.Ris(title, authors, journal, pubdate, pmid, doi, volume, issue, pages);
            actions.Add(new DetailAction(DT.T("复制 RIS", "Copy RIS"), ris, DetailAction.ActionStyleKind.Secondary));
            actions.Add(new DetailAction(DT.T("导出 RIS", "Export RIS"), ris, DetailAction.ActionStyleKind.Secondary,
                DetailAction.ActionKind.Export, $"PMID_{pmid}", "ris"));
        }

        var headerMeta = new List<string>();
        if (journal.Length > 0) headerMeta.Add(journal);
        if (pubdate.Length > 0) headerMeta.Add(pubdate);

        return new DetailModel
        {
            HeaderTitle = title.Length == 0 ? $"PMID: {pmid}" : title,
            HeaderSubtitle = string.Join(", ", authors.Take(3)) + (authors.Count > 3 ? DT.T(" 等", " et al.") : ""),
            HeaderMeta = headerMeta,
            Sections = new List<KVSection> { new(DT.T("文献信息", "Article Info"), rows) },
            FreeTextBlocks = freeTexts,
            Actions = actions,
            XLinks = xlinks,
            WebUrl = $"https://pubmed.ncbi.nlm.nih.gov/{pmid}/"
        };
    }

    private static string BuildUrl(string tool, Dictionary<string, string> parameters) =>
        Urls.BuildQueryURL($"{Base}/{tool}.fcgi", parameters);

    private static string BuildYearFilter(string filterId)
    {
        int currentYear = DateTime.Now.Year;
        int? start = filterId switch
        {
            "2y" => currentYear - 2,
            "5y" => currentYear - 5,
            "10y" => currentYear - 10,
            _ => null
        };
        if (!start.HasValue) return "";
        return $"(\"{start}\"[PDAT] : \"{currentYear + 1}\"[PDAT])";
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
