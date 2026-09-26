using System.Text.Json;
using System.Text.Json.Serialization;

namespace SciToolbox.Core;

// MARK: - Models

/// <summary>集合中的一个实体（一条 accession / 基因 / 文献等）。</summary>
public sealed class CollectionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ToolId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? Note { get; set; }
    /// <summary>重新拉取详情所需的上下文；缺失时回退到 itemId 直接查询。</summary>
    public Dictionary<string, string>? Context { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string RelativeTime => Core.RelativeTime.Format(AddedAt);
}

/// <summary>一个项目集合（课题）。</summary>
public sealed class SciCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<CollectionEntry> Entries { get; set; } = new();

    [JsonIgnore]
    public string RelativeUpdated => Core.RelativeTime.Format(UpdatedAt);
}

/// <summary>加入集合的候选（用于从结果列表 / 详情页批量加入）。</summary>
public sealed class CollectionCandidate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ToolId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public Dictionary<string, string>? Context { get; set; }

    public CollectionEntry ToEntry() => new()
    {
        ToolId = ToolId,
        ToolName = ToolName,
        ItemId = ItemId,
        Title = Title,
        Subtitle = Subtitle,
        Context = Context
    };
}

/// <summary>
/// 项目集合管理器（对应 macOS 版 CollectionStore）。纯本地、UserDefaults、可导出。
/// 集合是「带课题维度的分组容器」，一个实体可同时属于多个集合。
/// </summary>
public sealed class CollectionStore
{
    public static CollectionStore Shared { get; } = new();

    private const string StoreKey = "SciToolbox.Collections";

    public event Action? Changed;
    public List<SciCollection> Collections { get; private set; } = new();

    private CollectionStore() { Load(); }

    private static string DedupKey(string toolId, string itemId) => $"{toolId}|{itemId}";

    // MARK: - Collection 级操作

    public SciCollection Create(string name)
    {
        var trimmed = name.Trim();
        var c = new SciCollection
        {
            Name = trimmed.Length == 0 ? L10n.T(L10n.Key.UnnamedCollection) : trimmed
        };
        Collections.Insert(0, c);
        Save();
        return c;
    }

    public void Rename(string id, string name)
    {
        var c = Collections.FirstOrDefault(x => x.Id == id);
        if (c == null) return;
        var trimmed = name.Trim();
        c.Name = trimmed.Length == 0 ? L10n.T(L10n.Key.UnnamedCollection) : name;
        c.UpdatedAt = DateTime.Now;
        Save();
    }

    public void UpdateNote(string id, string? note)
    {
        var c = Collections.FirstOrDefault(x => x.Id == id);
        if (c == null) return;
        c.Note = note;
        c.UpdatedAt = DateTime.Now;
        Save();
    }

    public SciCollection? Find(string id) => Collections.FirstOrDefault(c => c.Id == id);

    public SciCollection? Delete(string id)
    {
        var c = Collections.FirstOrDefault(x => x.Id == id);
        if (c == null) return null;
        Collections.Remove(c);
        Save();
        return c;
    }

    public void RestoreCollection(SciCollection c)
    {
        if (Collections.Any(x => x.Id == c.Id)) return;
        Collections.Insert(0, c);
        Save();
    }

    public void ClearAll() { Collections.Clear(); Save(); }

    // MARK: - Entry 级操作

    public void AddEntry(CollectionEntry entry, string collectionId)
    {
        var c = Find(collectionId);
        if (c == null) return;
        var key = DedupKey(entry.ToolId, entry.ItemId);
        if (c.Entries.Any(e => DedupKey(e.ToolId, e.ItemId) == key)) return;
        c.Entries.Add(entry);
        c.UpdatedAt = DateTime.Now;
        Save();
    }

    public int AddEntries(IEnumerable<CollectionEntry> entries, string collectionId)
    {
        var c = Find(collectionId);
        if (c == null) return 0;
        int added = 0;
        foreach (var e in entries)
        {
            var key = DedupKey(e.ToolId, e.ItemId);
            if (c.Entries.Any(x => DedupKey(x.ToolId, x.ItemId) == key)) continue;
            c.Entries.Add(e);
            added++;
        }
        if (added > 0) { c.UpdatedAt = DateTime.Now; Save(); }
        return added;
    }

    public CollectionEntry? RemoveEntry(string collectionId, string entryId)
    {
        var c = Find(collectionId);
        if (c == null) return null;
        var idx = c.Entries.FindIndex(e => e.Id == entryId);
        if (idx < 0) return null;
        var removed = c.Entries[idx];
        c.Entries.RemoveAt(idx);
        c.UpdatedAt = DateTime.Now;
        Save();
        return removed;
    }

    public void RestoreEntry(CollectionEntry entry, string collectionId)
    {
        var c = Find(collectionId);
        if (c == null) return;
        var key = DedupKey(entry.ToolId, entry.ItemId);
        if (c.Entries.Any(e => DedupKey(e.ToolId, e.ItemId) == key)) return;
        c.Entries.Insert(0, entry);
        c.UpdatedAt = DateTime.Now;
        Save();
    }

    public void UpdateEntryNote(string collectionId, string entryId, string? note)
    {
        var c = Find(collectionId);
        var e = c?.Entries.FirstOrDefault(x => x.Id == entryId);
        if (e == null) return;
        e.Note = note;
        c!.UpdatedAt = DateTime.Now;
        Save();
    }

    public bool Contains(string collectionId, string toolId, string itemId)
    {
        var c = Find(collectionId);
        return c?.Entries.Any(e => e.ToolId == toolId && e.ItemId == itemId) ?? false;
    }

    // MARK: - 导出

    public string ExportAsJson() =>
        JsonSerializer.Serialize(Collections, new JsonSerializerOptions { WriteIndented = true });

    // MARK: - 持久化

    private void Load()
    {
        try { Collections = Deserialize(Prefs.GetRaw(StoreKey, "[]")) ?? new(); }
        catch { Collections = new(); }
    }

    private void Save()
    {
        Prefs.SetRaw(StoreKey, JsonSerializer.Serialize(Collections));
        Changed?.Invoke();
    }

    private static List<SciCollection>? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<List<SciCollection>>(json); } catch { return null; }
    }
}
