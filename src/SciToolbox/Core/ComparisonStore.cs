using SciToolbox.Core;

namespace SciToolbox.Core;

/// <summary>对齐对比表中的一行（以 canonical id 作为跨条目对齐键）。</summary>
public sealed class CompareField
{
    public string Id { get; init; } = "";      // "name"/"source"/... 或 "attr:<字段名>"
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public bool Copyable { get; init; }
}

/// <summary>一条对比条目（承载已抓取或待抓取的详情）。</summary>
public sealed class ComparisonItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public string ToolId { get; init; } = "";
    public string ToolName { get; init; } = "";
    public ToolCategory Category { get; init; }
    public string ItemId { get; init; } = "";
    public string? Query { get; init; }
    public Dictionary<string, string>? Context { get; init; }
    public DetailModel? Detail { get; set; }
    public bool Loading { get; set; }
}

/// <summary>
/// 跨库并排对比容器（全局单例，对应 macOS 版 ComparisonStore）。
/// 管理已加入对比的条目，按需异步抓取缺失详情，最多 4 条。
/// </summary>
public sealed class ComparisonStore
{
    public static ComparisonStore Shared { get; } = new();

    public const int MaxItems = 4;

    public event Action? Changed;
    public List<ComparisonItem> Items { get; } = new();

    public bool Contains(string toolId, string itemId) =>
        Items.Any(i => i.ToolId == toolId && i.ItemId == itemId);

    public void Add(string toolId, string toolName, ToolCategory category, string itemId,
                    string? query = null, Dictionary<string, string>? context = null, DetailModel? detail = null)
    {
        if (Contains(toolId, itemId))
        {
            Toast.Show(L10n.T(L10n.Key.AlreadyInCompare));
            return;
        }
        if (Items.Count >= MaxItems)
        {
            Toast.Show(L10n.T(L10n.Key.CompareLimit, MaxItems));
            return;
        }
        var item = new ComparisonItem
        {
            ToolId = toolId, ToolName = toolName, Category = category,
            ItemId = itemId, Query = query, Context = context, Detail = detail,
            Loading = detail == null
        };
        Items.Add(item);
        Toast.Show(L10n.T(L10n.Key.AddedToCompare, Items.Count, MaxItems));
        Changed?.Invoke();
        if (detail == null) _ = LoadDetailAsync(item.Id);
    }

    public void Remove(ComparisonItem item)
    {
        Items.RemoveAll(i => i.Id == item.Id);
        Changed?.Invoke();
    }

    public void Clear()
    {
        Items.Clear();
        Changed?.Invoke();
    }

    private async Task LoadDetailAsync(Guid cid)
    {
        var item = Items.FirstOrDefault(i => i.Id == cid);
        if (item == null) return;
        var provider = ToolRegistry.Shared.Find(item.ToolId);
        if (provider == null) return;
        try
        {
            var model = await provider.DetailAsync(item.ItemId, item.Context);
            var cur = Items.FirstOrDefault(i => i.Id == cid);
            if (cur != null) { cur.Detail = model; cur.Loading = false; Changed?.Invoke(); }
        }
        catch (Exception ex)
        {
            var cur = Items.FirstOrDefault(i => i.Id == cid);
            if (cur != null)
            {
                cur.Detail = new DetailModel
                {
                    HeaderTitle = item.ItemId,
                    Sections = new List<KVSection>
                    {
                        new(L10n.T(L10n.Key.Hint), new List<KVRow>
                        {
                            new(DT.T("状态", "Status"), L10n.T(L10n.Key.DetailLoadFailed, ex.Message))
                        })
                    }
                };
                cur.Loading = false;
                Changed?.Invoke();
            }
        }
    }

    /// 抽出跨库一致的对比字段（对应 DetailModel.comparisonFields）。
    public static List<CompareField> FieldsOf(DetailModel model, string sourceName, ToolCategory category)
    {
        var fields = new List<CompareField>
        {
            new() { Id = "name", Label = L10n.T(L10n.Key.Name), Value = model.HeaderTitle, Copyable = true },
            new() { Id = "source", Label = L10n.T(L10n.Key.Source), Value = sourceName },
            new() { Id = "category", Label = L10n.T(L10n.Key.Category), Value = category.LocalizedName() },
        };
        if (!string.IsNullOrEmpty(model.HeaderSubtitle))
            fields.Add(new CompareField { Id = "subtitle", Label = L10n.T(L10n.Key.SpeciesDesc), Value = model.HeaderSubtitle! });

        var primary = model.Sections.FirstOrDefault(s => s.Rows.Count > 0);
        if (primary != null)
            foreach (var row in primary.Rows.Take(8))
                fields.Add(new CompareField { Id = "attr:" + row.Key, Label = row.Key, Value = row.Value, Copyable = row.Copyable });

        return fields;
    }
}
