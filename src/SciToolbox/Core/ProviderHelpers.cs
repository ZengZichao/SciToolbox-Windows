using System.Globalization;
using System.Text;

namespace SciToolbox.Core;

/// <summary>URL 构建辅助（对应 macOS 版 buildQueryURL / urlEncodePath）。</summary>
public static class Urls
{
    /// <summary>把参数拼到 base URL 的 query 上（对 key/value 做 percent 编码，空格转 %20）。</summary>
    public static string BuildQueryURL(string baseUrl, IDictionary<string, string> parameters)
    {
        if (parameters.Count == 0) return baseUrl;
        var sb = new StringBuilder(baseUrl);
        sb.Append(baseUrl.Contains('?') ? '&' : '?');
        bool first = true;
        foreach (var kv in parameters)
        {
            if (!first) sb.Append('&');
            first = false;
            sb.Append(EscapeQueryString(kv.Key)).Append('=').Append(EscapeQueryString(kv.Value));
        }
        return sb.ToString();
    }

    private static string EscapeQueryString(string s)
    {
        // 与 Swift URLQueryItem 对齐：使用 RFC 3986 百分号编码（空格 -> %20）
        return Uri.EscapeDataString(s ?? "");
    }

    /// <summary>为 URL 路径片段做 percent 编码（对应 .urlPathAllowed）。</summary>
    public static string EncodePath(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            // unreserved: A-Z a-z 0-9 - . _ ~ ；path 允许的 sub-delims 与 : @ 保持原样
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                c == '-' || c == '.' || c == '_' || c == '~' || c == '!' || c == '$' ||
                c == '&' || c == '\'' || c == '(' || c == ')' || c == '*' || c == '+' ||
                c == ',' || c == ';' || c == '=' || c == ':' || c == '@')
            {
                sb.Append(c);
            }
            else
            {
                foreach (byte b in Encoding.UTF8.GetBytes(c.ToString()))
                    sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }
}

/// <summary>跨 Provider 复用的纯函数（对应 macOS 版 ProviderHelpers）。</summary>
public static class ProviderHelpers
{
    /// <summary>截断式作者列表格式化："A, B, C 等"（超过 max 用「等 / et al.」）。</summary>
    public static string FormatAuthors(IReadOnlyList<string> authors, int max = 3)
    {
        if (authors == null || authors.Count == 0) return "";
        if (authors.Count <= max) return string.Join(", ", authors);
        return string.Join(", ", authors.Take(max)) + DT.T(" 等", " et al.");
    }

    /// <summary>构建跨库互链。query 会做首尾空白清理。</summary>
    public static XLink Xlink(string toolId, string query, string label) =>
        new(toolId, query.Trim(), label);

    /// <summary>将可能的 DOI 归一化为可点击的 https URL；非法返回 null。</summary>
    public static string? DoiUrl(string doi)
    {
        var d = doi?.Trim() ?? "";
        if (d.Length == 0) return null;
        var lower = d.ToLowerInvariant();
        if (lower.StartsWith("http://") || lower.StartsWith("https://")) return d;
        if (lower.StartsWith("doi:")) return "https://doi.org/" + d.Substring(4);
        if (d.StartsWith("10.")) return "https://doi.org/" + d;
        return null;
    }

    /// <summary>是否为可识别的 URL（用于正文行内可点击渲染）。</summary>
    public static bool IsUrl(string s)
    {
        var l = s?.ToLowerInvariant() ?? "";
        return l.StartsWith("http://") || l.StartsWith("https://");
    }

    /// <summary>
    /// 统一分页结果（解析一致性增强）。
    /// fetchedTotal：API 直接给出的真实总数；为 null 时按「本页是否装满」启发式推断。
    /// alreadyLoaded：翻页前已加载条数（首屏传 0）。
    /// </summary>
    public static (int? total, bool hasMore) Paginate(int itemCount, int pageSize, int? fetchedTotal = null, int alreadyLoaded = 0)
    {
        if (fetchedTotal.HasValue)
            return (fetchedTotal, alreadyLoaded + itemCount < fetchedTotal.Value);
        return (null, itemCount >= pageSize);
    }
}
