using System.Collections.Generic;

namespace SciToolbox.Core;

// MARK: - Tool Category

/// 工具分类。五大分类，对应 macOS 版 ToolCategory。
public enum ToolCategory
{
    Taxonomy,   // 分类与物种
    Gene,       // 基因与序列
    Protein,    // 蛋白与结构
    Function,   // 功能与通路
    Literature  // 文献
}

public static class ToolCategoryExtensions
{
    public static string LocalizedName(this ToolCategory cat) => cat switch
    {
        ToolCategory.Taxonomy => L10n.T(L10n.Key.CatTaxonomy),
        ToolCategory.Gene => L10n.T(L10n.Key.CatGene),
        ToolCategory.Protein => L10n.T(L10n.Key.CatProtein),
        ToolCategory.Function => L10n.T(L10n.Key.CatFunction),
        ToolCategory.Literature => L10n.T(L10n.Key.CatLiterature),
        _ => cat.ToString()
    };

    /// 单字缩写（色觉障碍友好，分类线索在颜色之外叠加文本标签）。
    public static string Abbreviation(this ToolCategory cat) => cat switch
    {
        ToolCategory.Taxonomy => L10n.T(L10n.Key.CatAbbrTaxonomy),
        ToolCategory.Gene => L10n.T(L10n.Key.CatAbbrGene),
        ToolCategory.Protein => L10n.T(L10n.Key.CatAbbrProtein),
        ToolCategory.Function => L10n.T(L10n.Key.CatAbbrFunction),
        ToolCategory.Literature => L10n.T(L10n.Key.CatAbbrLiterature),
        _ => ""
    };

    /// 分类在侧边栏 / 主题中的排序顺序。
    public static readonly ToolCategory[] Ordered =
    {
        ToolCategory.Taxonomy,
        ToolCategory.Gene,
        ToolCategory.Protein,
        ToolCategory.Function,
        ToolCategory.Literature
    };
}

// MARK: - Search Result

public sealed class SearchResult
{
    public List<ResultItem> Items { get; set; } = new();
    public int? Total { get; set; }
    public bool HasMore { get; set; }
}

public sealed class ResultItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? Badge { get; set; }
    public string? Meta { get; set; }
    /// Extra context passed to detail request (e.g. accession, taxid)
    public Dictionary<string, string>? Extra { get; set; }
}

// MARK: - Detail Model

public sealed class DetailModel
{
    public string HeaderTitle { get; set; } = "";
    public string? HeaderSubtitle { get; set; }
    public List<string>? HeaderMeta { get; set; }
    public List<KVSection> Sections { get; set; } = new();
    public List<FreeTextBlock> FreeTextBlocks { get; set; } = new();
    public List<DetailAction> Actions { get; set; } = new();
    public List<XLink> XLinks { get; set; } = new();
    public string? WebUrl { get; set; }
    /// Optional preview image URL (e.g. PDB structure image, AlphaFold cartoon)
    public string? ImageUrl { get; set; }
}

public sealed class KVSection
{
    public string? Title { get; set; }
    public List<KVRow> Rows { get; set; } = new();

    public KVSection() { }
    public KVSection(string? title, List<KVRow> rows)
    {
        Title = title;
        Rows = rows;
    }
}

public sealed class KVRow
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public bool Copyable { get; set; }
    /// 可选：点击 value 在浏览器打开此 URL（如 DOI 链接）。
    public string? Link { get; set; }
    /// 可选：点击 value 触发跨库互链跳转（如 PDB/GO/Pfam accession）。
    public XLink? XlinkTarget { get; set; }

    public KVRow(string key, string value, bool copyable = false, string? link = null, XLink? xlinkTarget = null)
    {
        Key = key;
        Value = value;
        Copyable = copyable;
        Link = link;
        XlinkTarget = xlinkTarget;
    }
}

public sealed class FreeTextBlock
{
    public string? Title { get; set; }
    public string Text { get; set; } = "";
    public bool Copyable { get; set; }

    public FreeTextBlock(string? title, string text, bool copyable = false)
    {
        Title = title;
        Text = text;
        Copyable = copyable;
    }
}

public sealed class DetailAction
{
    public enum ActionStyleKind { Primary, Secondary }
    public enum ActionKind { Copy, Export }

    public string Label { get; set; } = "";
    public string Payload { get; set; } = "";
    public ActionStyleKind Style { get; set; } = ActionStyleKind.Secondary;
    /// 动作类型：Copy 复制 payload 到剪贴板；Export 将 payload 写入文件。
    public ActionKind Kind { get; set; } = ActionKind.Copy;
    /// 当 Kind == Export 时使用的文件名与扩展名（不含点）。
    public string? ExportFileName { get; set; }
    public string? ExportExt { get; set; }

    public DetailAction() { }
    public DetailAction(string label, string payload, ActionStyleKind style,
                        ActionKind kind = ActionKind.Copy, string? exportFileName = null, string? exportExt = null)
    {
        Label = label;
        Payload = payload;
        Style = style;
        Kind = kind;
        ExportFileName = exportFileName;
        ExportExt = exportExt;
    }
}

public sealed class XLink
{
    public string ToolId { get; set; } = "";
    public string Query { get; set; } = "";
    public string Label { get; set; } = "";

    public XLink() { }
    public XLink(string toolId, string query, string label)
    {
        ToolId = toolId;
        Query = query;
        Label = label;
    }
}

// MARK: - Picker Option (for tools like KEGG/Ensembl that need a selector)

public sealed class PickerOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Example { get; set; }

    public PickerOption() { }
    public PickerOption(string id, string label, string? example = null)
    {
        Id = id;
        Label = label;
        Example = example;
    }
}
