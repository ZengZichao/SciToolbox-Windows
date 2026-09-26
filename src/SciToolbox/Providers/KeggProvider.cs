using System.Text.RegularExpressions;
using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>KEGG REST API — 纯文本响应，自动识别数据库类型（对应 KEGGProvider.swift）。</summary>
public sealed class KeggProvider : ToolProviderBase
{
    public override string Id => "kegg";
    public override string Name => DT.T("KEGG 在线检索", "KEGG Online Search");
    public override ToolCategory Category => ToolCategory.Function;
    public override string IconName => "network";
    public override string Placeholder => DT.T("关键词（如 glycolysis）", "Keyword (e.g. glycolysis)");
    public override string DataSourceNote => DT.T("数据来源：KEGG REST API (rest.kegg.jp)", "Data source: KEGG REST API (rest.kegg.jp)");

    private const string Base = "https://rest.kegg.jp";

    public override Task<SearchResult> SearchAsync(string query, CancellationToken ct = default) =>
        SearchAsync(query, 0, null, ct);

    public override async Task<SearchResult> SearchAsync(string query, int offset, string? pickerId, CancellationToken ct = default)
    {
        var db = DetectDatabase(query);
        if (offset > 0) return new SearchResult { Items = new(), Total = 0 };
        var url = $"{Base}/find/{Urls.EncodePath(db)}/{Urls.EncodePath(query)}";

        string text;
        try
        {
            text = await ApiClient.Shared.GetTextAsync(url, ct);
        }
        catch (ApiException ex) when (ex.Kind == ApiException.KindType.Http && ex.StatusCode == 404)
        {
            return new SearchResult { Items = new(), Total = 0 };
        }

        if (text.Length == 0 || text.StartsWith("404"))
            return new SearchResult { Items = new(), Total = 0 };

        var items = new List<ResultItem>();
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0) continue;
            var parts = line.Split('\t', 2);
            if (parts.Length == 2)
            {
                var entry = parts[0];
                var desc = parts[1];
                items.Add(new ResultItem
                {
                    Id = entry,
                    Title = desc,
                    Badge = entry,
                    Extra = new Dictionary<string, string> { ["entry"] = entry }
                });
            }
        }
        return new SearchResult { Items = items, Total = items.Count };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var entry = context != null && context.TryGetValue("entry", out var e) ? e : id;
        var url = $"{Base}/get/{Urls.EncodePath(entry)}";

        string text;
        try
        {
            text = await ApiClient.Shared.GetTextAsync(url, ct);
        }
        catch (ApiException ex) when (ex.Kind == ApiException.KindType.Http && ex.StatusCode == 404)
        {
            throw ApiException.NotFound(DT.T($"未找到条目 {entry}", $"Entry not found: {entry}"));
        }

        if (text.Length == 0 || text.StartsWith("404"))
            throw ApiException.NotFound(DT.T("未找到相关条目", "No matching entry found"));

        var sections = ParseEntry(text);
        var kvSections = new List<KVSection>();
        var rows = new List<KVRow>();
        var currentField = "";

        foreach (var sec in sections)
        {
            if (sec.Field != currentField)
            {
                if (rows.Count > 0) { kvSections.Add(new KVSection(null, rows)); rows = new List<KVRow>(); }
                currentField = sec.Field;
            }
            rows.Add(new KVRow(sec.Field, sec.Value, copyable: true));
        }
        if (rows.Count > 0) kvSections.Add(new KVSection(null, rows));

        var actions = new List<DetailAction>
        {
            new(DT.T("复制条目", "Copy Entry"), entry, DetailAction.ActionStyleKind.Secondary),
            new(DT.T("复制详情", "Copy Details"), string.Join("\n", sections.Select(s => $"{s.Field}\t{s.Value}")), DetailAction.ActionStyleKind.Secondary)
        };

        string? imageUrl = entry.StartsWith("C") ? $"https://www.kegg.jp/Fig/compound/{entry}.gif"
            : entry.StartsWith("D") ? $"https://www.kegg.jp/Fig/drug/{entry}.gif"
            : entry.StartsWith("G") ? $"https://www.kegg.jp/Fig/glycan/{entry}.gif"
            : null;

        return new DetailModel
        {
            HeaderTitle = entry,
            HeaderSubtitle = sections.Count > 0 ? sections[0].Value : null,
            Sections = kvSections,
            Actions = actions,
            WebUrl = $"https://www.kegg.jp/entry/{entry}",
            ImageUrl = imageUrl
        };
    }

    private sealed class EntrySection { public string Field = ""; public string Value = ""; }

    private static List<EntrySection> ParseEntry(string text)
    {
        var outList = new List<EntrySection>();
        EntrySection? current = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var lineStr = rawLine.TrimEnd('\r');
            if (lineStr.Length == 0) continue;
            if (lineStr.StartsWith(" "))
            {
                if (current != null)
                    current.Value += "\n" + lineStr.Trim();
            }
            else
            {
                if (current != null) outList.Add(current);
                var spaceIdx = lineStr.IndexOf(' ');
                if (spaceIdx >= 0)
                    current = new EntrySection { Field = lineStr.Substring(0, spaceIdx), Value = lineStr.Substring(spaceIdx + 1) };
                else
                    current = new EntrySection { Field = lineStr, Value = "" };
            }
        }
        if (current != null) outList.Add(current);
        return outList;
    }

    private static string DetectDatabase(string query)
    {
        var q = query.Trim();
        if (Regex.IsMatch(q, @"^K\d{5}$")) return "ko";
        if (Regex.IsMatch(q, @"^M\d{5}$")) return "module";
        if (Regex.IsMatch(q, @"^C\d{5}$")) return "compound";
        if (Regex.IsMatch(q, @"^D\d{5}$")) return "drug";
        if (Regex.IsMatch(q, @"^H\d{5}$")) return "disease";
        if (Regex.IsMatch(q, @"^\d+\.\d+\.\d+\.\d+$")) return "enzyme";
        if (Regex.IsMatch(q, @"^\w{2,5}\d{5}$")) return "pathway";
        if (Regex.IsMatch(q, @"^T\d{5}$")) return "genome";
        return "pathway";
    }
}
