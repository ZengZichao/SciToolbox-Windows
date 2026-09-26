using System.Text.Json;
using System.Text.Json.Serialization;

namespace SciToolbox.Core;

// MARK: - SearchHistory（纯本地，无网络，无隐私风险）

public sealed class HistoryEntry
{
    public string Id { get; set; } = "";
    public string ToolId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string Query { get; set; } = "";
    public DateTime Timestamp { get; set; }

    [JsonIgnore]
    public string RelativeTime => Core.RelativeTime.Format(Timestamp);
}

/// <summary>查询历史管理器（对应 macOS 版 SearchHistory）。纯本地，按工具分组，最多 200 条。</summary>
public sealed class SearchHistory
{
    public static SearchHistory Shared { get; } = new();

    private const string StoreKey = "SciToolbox.SearchHistory";
    private const int MaxEntries = 200;

    public event Action? Changed;
    public List<HistoryEntry> Entries { get; private set; } = new();

    private SearchHistory() { Load(); }

    public void Add(string toolId, string toolName, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return;
        Entries.RemoveAll(e => e.ToolId == toolId && e.Query == trimmed);
        Entries.Insert(0, new HistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            ToolId = toolId,
            ToolName = toolName,
            Query = trimmed,
            Timestamp = DateTime.Now
        });
        if (Entries.Count > MaxEntries)
            Entries.RemoveRange(MaxEntries, Entries.Count - MaxEntries);
        Save();
    }

    public List<HistoryEntry> For(string toolId) => Entries.Where(e => e.ToolId == toolId).ToList();

    public HistoryEntry? Remove(string id)
    {
        var idx = Entries.FindIndex(e => e.Id == id);
        if (idx < 0) return null;
        var removed = Entries[idx];
        Entries.RemoveAt(idx);
        Save();
        return removed;
    }

    public void Restore(HistoryEntry entry)
    {
        if (Entries.Any(e => e.Id == entry.Id)) return;
        Entries.Insert(0, entry);
        Save();
    }

    public void ClearAll() { Entries.Clear(); Save(); }
    public void Clear(string toolId) { Entries.RemoveAll(e => e.ToolId == toolId); Save(); }

    public string ExportAsJson() => Serialize(Entries);

    private void Load()
    {
        try { Entries = Deserialize<List<HistoryEntry>>(Prefs.GetRaw(StoreKey, "[]")) ?? new(); }
        catch { Entries = new(); }
    }

    private void Save()
    {
        Prefs.SetRaw(StoreKey, Serialize(Entries));
        Changed?.Invoke();
    }

    private static string Serialize<T>(T obj) =>
        JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });
    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json); } catch { return default; }
    }
}

// MARK: - LocalFavorites（纯本地，可导出）

public sealed class FavoriteItem
{
    public string Id { get; set; } = "";
    public string ToolId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? Note { get; set; }
    public Dictionary<string, string>? Snapshot { get; set; }
    public DateTime Timestamp { get; set; }

    [JsonIgnore]
    public string RelativeTime => Core.RelativeTime.Format(Timestamp);
}

/// <summary>本地收藏管理器（对应 macOS 版 LocalFavorites）。纯本地、可导出为文本/JSON。</summary>
public sealed class LocalFavorites
{
    public static LocalFavorites Shared { get; } = new();

    private const string StoreKey = "SciToolbox.LocalFavorites";

    public event Action? Changed;
    public List<FavoriteItem> Favorites { get; private set; } = new();

    private LocalFavorites() { Load(); }

    public void Toggle(string toolId, string toolName, string itemId, string title,
                       string? subtitle = null, Dictionary<string, string>? snapshot = null, string? note = null)
    {
        var idx = Favorites.FindIndex(f => f.ItemId == itemId && f.ToolId == toolId);
        if (idx >= 0)
        {
            Favorites.RemoveAt(idx);
        }
        else
        {
            Favorites.Insert(0, new FavoriteItem
            {
                Id = Guid.NewGuid().ToString(),
                ToolId = toolId,
                ToolName = toolName,
                ItemId = itemId,
                Title = title,
                Subtitle = subtitle,
                Note = note,
                Snapshot = snapshot,
                Timestamp = DateTime.Now
            });
        }
        Save();
    }

    public void Add(string toolId, string toolName, string itemId, string title, string? subtitle = null)
    {
        if (Favorites.Any(f => f.ItemId == itemId && f.ToolId == toolId)) return;
        Favorites.Insert(0, new FavoriteItem
        {
            Id = Guid.NewGuid().ToString(),
            ToolId = toolId,
            ToolName = toolName,
            ItemId = itemId,
            Title = title,
            Subtitle = subtitle,
            Timestamp = DateTime.Now
        });
        Save();
    }

    public bool IsFavorited(string toolId, string itemId) =>
        Favorites.Any(f => f.ToolId == toolId && f.ItemId == itemId);

    public FavoriteItem? Favorite(string toolId, string itemId) =>
        Favorites.FirstOrDefault(f => f.ToolId == toolId && f.ItemId == itemId);

    public void UpdateNote(string id, string? note)
    {
        var f = Favorites.FirstOrDefault(x => x.Id == id);
        if (f != null) { f.Note = note; Save(); }
    }

    public FavoriteItem? Remove(string id)
    {
        var idx = Favorites.FindIndex(f => f.Id == id);
        if (idx < 0) return null;
        var removed = Favorites[idx];
        Favorites.RemoveAt(idx);
        Save();
        return removed;
    }

    public void Restore(FavoriteItem item)
    {
        if (Favorites.Any(f => f.Id == item.Id)) return;
        Favorites.Insert(0, item);
        Save();
    }

    public void ClearAll() { Favorites.Clear(); Save(); }

    public string ExportAsText()
    {
        var lines = new List<string>
        {
            DT.T("SciToolbox 收藏列表", "SciToolbox Favorites"),
            DT.T($"导出时间：{DateTime.Now:g}", $"Exported: {DateTime.Now:g}"),
            DT.T($"共 {Favorites.Count} 条", $"{Favorites.Count} items"),
            ""
        };
        foreach (var fav in Favorites)
        {
            lines.Add($"[{fav.ToolName}] {fav.Title}");
            if (!string.IsNullOrEmpty(fav.Subtitle)) lines.Add($"  {fav.Subtitle}");
            if (fav.Snapshot != null)
                foreach (var kv in fav.Snapshot.Take(4))
                    lines.Add($"  {kv.Key}: {kv.Value}");
            if (!string.IsNullOrEmpty(fav.Note)) lines.Add(DT.T($"  笔记：{fav.Note}", $"  Note: {fav.Note}"));
            lines.Add($"  ID: {fav.ItemId}");
            lines.Add("");
        }
        return string.Join("\n", lines);
    }

    public string ExportAsJson() =>
        JsonSerializer.Serialize(Favorites, new JsonSerializerOptions { WriteIndented = true });

    private void Load()
    {
        try { Favorites = Deserialize(Prefs.GetRaw(StoreKey, "[]")) ?? new(); }
        catch { Favorites = new(); }
    }

    private void Save()
    {
        Prefs.SetRaw(StoreKey, JsonSerializer.Serialize(Favorites));
        Changed?.Invoke();
    }

    private static List<FavoriteItem>? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<List<FavoriteItem>>(json); } catch { return null; }
    }
}
