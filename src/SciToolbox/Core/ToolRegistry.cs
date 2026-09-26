using System.Collections.Generic;
using SciToolbox.Providers;

namespace SciToolbox.Core;

/// <summary>
/// 数据源 Provider 协议（对应 macOS 版 ToolProvider）。每个公开数据库实现一个 provider。
/// </summary>
public interface IToolProvider
{
    /// <summary>唯一标识（如 "uniprot"）。</summary>
    string Id { get; }
    /// <summary>展示名（如 "UniProt 蛋白质"）。</summary>
    string Name { get; }
    /// <summary>首页分组分类。</summary>
    ToolCategory Category { get; }
    /// <summary>图标标识（本项目用 Segoe MDL2 / 文字简写映射，见 Theme）。</summary>
    string IconName { get; }
    /// <summary>搜索框占位文本。</summary>
    string Placeholder { get; }
    /// <summary>数据来源署名。</summary>
    string DataSourceNote { get; }

    /// <summary>可选下拉选项（如 KEGG 数据库列表、Ensembl 物种列表）。</summary>
    IReadOnlyList<PickerOption>? PickerOptions { get; }
    /// <summary>默认下拉选项 ID。</summary>
    string? DefaultPickerId { get; }

    /// <summary>基础检索（offset=0，无分页）。</summary>
    Task<SearchResult> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>带分页与下拉选择的检索。</summary>
    Task<SearchResult> SearchAsync(string query, int offset, string? pickerId, CancellationToken ct = default);

    /// <summary>拉取条目详情。context 携带列表项的额外字段。</summary>
    Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default);
}

/// <summary>Provider 基类，提供默认实现（对应 macOS 的协议扩展默认实现）。</summary>
public abstract class ToolProviderBase : IToolProvider
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract ToolCategory Category { get; }
    public abstract string IconName { get; }
    public abstract string Placeholder { get; }
    public abstract string DataSourceNote { get; }

    public virtual IReadOnlyList<PickerOption>? PickerOptions => null;
    public virtual string? DefaultPickerId => null;

    public abstract Task<SearchResult> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>默认：offset>0 返回空；否则委托基础检索。支持分页的 provider 应重写。</summary>
    public virtual Task<SearchResult> SearchAsync(string query, int offset, string? pickerId, CancellationToken ct = default)
    {
        if (offset > 0)
            return Task.FromResult(new SearchResult { Items = new(), Total = 0, HasMore = false });
        return SearchAsync(query, ct);
    }

    public abstract Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default);
}

/// <summary>
/// 所有 provider 的注册表，按分类分组（对应 macOS 版 ToolRegistry）。
/// </summary>
public sealed class ToolRegistry
{
    public static ToolRegistry Shared { get; } = new();

    public IReadOnlyList<IToolProvider> Providers { get; }

    private readonly Dictionary<string, IToolProvider> _byId = new();

    private ToolRegistry()
    {
        var list = new List<IToolProvider>
        {
            new UniProtProvider(),
            new PdbProvider(),
            new AlphaFoldProvider(),
            new PubMedProvider(),
            new NcbiTaxonomyProvider(),
            new NcbiGeneProvider(),
            new EnsemblProvider(),
            new KeggProvider(),
            new GoProvider(),
            new PfamProvider(),
            new BacDiveProvider(),
            new MGnifyProvider(),
            new GbifProvider(),
            new EuropePmcProvider(),
            new GtdbOfficialProvider(),
        };
        Providers = list;
        foreach (var p in list) _byId[p.Id] = p;
    }

    /// <summary>按分类分组（保持定义顺序），只返回非空分组。</summary>
    public List<(ToolCategory category, List<IToolProvider> items)> Grouped()
    {
        var result = new List<(ToolCategory, List<IToolProvider>)>();
        foreach (var cat in ToolCategoryExtensions.Ordered)
        {
            var items = Providers.Where(p => p.Category == cat).ToList();
            if (items.Count > 0) result.Add((cat, items));
        }
        return result;
    }

    public IToolProvider? Find(string id) => _byId.TryGetValue(id, out var p) ? p : null;
}
