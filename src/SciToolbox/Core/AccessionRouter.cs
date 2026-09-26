using System.Text.RegularExpressions;

namespace SciToolbox.Core;

/// <summary>
/// Accession 智能识别路由（对应 macOS 版 AccessionRouter）。
/// 纯函数、无副作用、无网络——仅做字符串模式识别，返回带置信度的候选列表。
/// 路由决策（直接跳转 vs 弹窗选择）由调用方根据 confidence 阈值决定。
/// </summary>
public static class AccessionRouter
{
    public sealed class Match
    {
        public string ToolId { get; init; } = "";
        public string Query { get; init; } = "";
        public double Confidence { get; init; }
        public string Reason { get; init; } = "";
    }

    private static bool IsMatch(string input, string pattern) =>
        Regex.IsMatch(input, pattern);

    /// <summary>识别一段输入，返回按置信度降序、按 toolId 去重后的候选列表。</summary>
    public static List<Match> Classify(string raw)
    {
        var input = raw?.Trim() ?? "";
        var matches = new List<Match>();
        if (input.Length == 0) return matches;

        // 1) DOI → Europe PMC
        var doiMatch = Regex.Match(input, @"10\.\d{4,9}/\S+");
        if (doiMatch.Success)
            matches.Add(new Match { ToolId = "europepmc", Query = doiMatch.Value, Confidence = 0.95, Reason = "DOI → Europe PMC" });

        // 2) GO 术语（GO:0008150）
        if (IsMatch(input, @"(?i)^GO:\d+$"))
            matches.Add(new Match { ToolId = "go", Query = input.ToUpperInvariant(), Confidence = 0.95, Reason = DT.T("GO 术语编号", "GO term ID") });

        // 3) Ensembl ID（ENSG / ENST / ENSP ...）
        if (IsMatch(input, @"(?i)^ENS[FGTPE]\d{11}$"))
            matches.Add(new Match { ToolId = "ensembl", Query = input, Confidence = 0.95, Reason = "Ensembl ID" });

        // 4) Pfam 结构域（PF00001）
        if (IsMatch(input, @"^PF\d{5}$"))
            matches.Add(new Match { ToolId = "pfam", Query = input.ToUpperInvariant(), Confidence = 0.90, Reason = DT.T("Pfam 结构域", "Pfam domain") });

        // 5) KEGG 条目（K/C/M/D/H + 5 位数字）
        if (IsMatch(input, @"^[KCMDH]\d{5}$"))
            matches.Add(new Match { ToolId = "kegg", Query = input.ToUpperInvariant(), Confidence = 0.85, Reason = DT.T("KEGG 条目", "KEGG entry") });

        // 6) UniProt Accession（官方正则：6 位或 10 位）
        if (IsMatch(input, @"^[OPQ][0-9][A-Z0-9]{3}[0-9]$") ||
            IsMatch(input, @"^[A-NR-Z][0-9]([A-Z][A-Z0-9]{2}[0-9]){1,2}$"))
            matches.Add(new Match { ToolId = "uniprot", Query = input, Confidence = 0.90, Reason = "UniProt Accession" });

        // 7) PDB 结构 ID（4 位，以数字开头，如 1ABC / 4HHB）；排除纯数字
        if (IsMatch(input, @"^[0-9][A-Za-z0-9]{3}$") && !IsMatch(input, @"^\d+$"))
            matches.Add(new Match { ToolId = "pdb", Query = input.ToUpperInvariant(), Confidence = 0.95, Reason = DT.T("PDB 结构 ID", "PDB structure ID") });

        // 8) 4 位全大写字母：既可能是 PDB 也可能是基因符号（歧义，低置信度双候选）
        if (IsMatch(input, @"^[A-Z]{4}$"))
        {
            matches.Add(new Match { ToolId = "pdb", Query = input.ToUpperInvariant(), Confidence = 0.50, Reason = DT.T("可能为 PDB ID", "Maybe a PDB ID") });
            matches.Add(new Match { ToolId = "ncbi_gene", Query = input, Confidence = 0.50, Reason = DT.T("可能为基因符号", "Maybe a gene symbol") });
        }

        // 9) 基因符号（字母开头，可含数字，长度 ≥ 2，且未被更精确规则命中）
        if (IsMatch(input, @"^[A-Za-z][A-Za-z0-9]*$") && input.Length >= 2)
        {
            if (!matches.Any(m => m.ToolId == "ncbi_gene"))
            {
                bool hasDigit = input.Any(char.IsDigit);
                double conf = (hasDigit || input.Length > 4) ? 0.85 : 0.60;
                matches.Add(new Match { ToolId = "ncbi_gene", Query = input, Confidence = conf, Reason = DT.T("基因符号", "Gene symbol") });
            }
        }

        // 10) 纯数字：PMID 与 TaxID 无法仅凭形态区分（歧义，双候选）；4 位纯数字补充 PDB 第三候选
        if (IsMatch(input, @"^\d+$"))
        {
            matches.Add(new Match { ToolId = "pubmed", Query = input, Confidence = 0.70, Reason = DT.T("可能为 PubMed ID", "Maybe a PubMed ID") });
            matches.Add(new Match { ToolId = "ncbi_taxonomy", Query = input, Confidence = 0.70, Reason = DT.T("可能为 TaxID", "Maybe a TaxID") });
            if (input.Length == 4)
                matches.Add(new Match { ToolId = "pdb", Query = input, Confidence = 0.45, Reason = DT.T("可能为 PDB ID", "Maybe a PDB ID") });
        }

        // 去重：同 toolId 仅保留最高置信度候选，按置信度降序
        return matches
            .GroupBy(m => m.ToolId)
            .Select(g => g.OrderByDescending(m => m.Confidence).First())
            .OrderByDescending(m => m.Confidence)
            .ToList();
    }

    public enum DecisionKind { Route, Choose }

    public sealed class Decision
    {
        public DecisionKind Kind { get; init; }
        public Match? Match { get; init; }
        public List<Match> Matches { get; init; } = new();
    }

    /// <summary>
    /// 路由决策：单一候选 → 直接跳转；最高 ≥0.85 且与次优差距 ≥0.2 → 直接跳转；其余 → 弹窗选择。
    /// </summary>
    public static Decision Decide(List<Match> matches)
    {
        if (matches.Count == 0) return new Decision { Kind = DecisionKind.Choose, Matches = matches };
        if (matches.Count == 1) return new Decision { Kind = DecisionKind.Route, Match = matches[0] };
        var top = matches[0];
        var second = matches[1];
        if (top.Confidence >= 0.85 && (top.Confidence - second.Confidence) >= 0.2)
            return new Decision { Kind = DecisionKind.Route, Match = top };
        return new Decision { Kind = DecisionKind.Choose, Matches = matches };
    }
}
