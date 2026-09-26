using System.IO;
using System.Text.Json;

namespace SciToolbox.Core;

/// <summary>
/// 轻量本地偏好存储（对应 macOS 版 UserDefaults）。
/// 纯本地 JSON 文件，位于 %LOCALAPPDATA%\SciToolbox\prefs.json。
/// 绝不联网、不含任何用户标识。
/// </summary>
public static class Prefs
{
    private static readonly object Gate = new();
    private static Dictionary<string, JsonElement> _data = new();
    private static string FilePath => Path.Combine(Dir, "prefs.json");
    private static bool _loaded;

    public static string Dir
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(baseDir, "SciToolbox");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (File.Exists(FilePath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                foreach (var p in doc.RootElement.EnumerateObject())
                    _data[p.Name] = p.Value.Clone();
            }
        }
        catch
        {
            _data = new Dictionary<string, JsonElement>();
        }
    }

    private static void Save()
    {
        try
        {
            var opts = new JsonSerializerOptions { WriteIndented = false };
            // JsonElement 必须原样交给序列化器：先转成 string 会把数组写成带引号的文本，
            // 下次读回来就无法反序列化（收藏 / 历史 / 集合会静默丢失）。
            var dict = new Dictionary<string, JsonElement>(_data);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dict, opts));
        }
        catch { /* best-effort persistence */ }
    }

    public static string GetString(string key, string def = "")
    {
        lock (Gate)
        {
            EnsureLoaded();
            if (_data.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString()!;
            return def;
        }
    }

    public static void SetString(string key, string value)
    {
        lock (Gate)
        {
            EnsureLoaded();
            _data[key] = JsonSerializer.SerializeToElement(value);
            Save();
        }
    }

    public static bool GetBool(string key, bool def)
    {
        lock (Gate)
        {
            EnsureLoaded();
            if (_data.TryGetValue(key, out var el))
            {
                if (el.ValueKind == JsonValueKind.True) return true;
                if (el.ValueKind == JsonValueKind.False) return false;
            }
            return def;
        }
    }

    public static void SetBool(string key, bool value)
    {
        lock (Gate)
        {
            EnsureLoaded();
            _data[key] = JsonSerializer.SerializeToElement(value);
            Save();
        }
    }

    /// 读取一段原始 JSON 文本（用于历史 / 收藏 / 集合的持久化）。
    public static string GetRaw(string key, string def = "")
    {
        lock (Gate)
        {
            EnsureLoaded();
            if (!_data.TryGetValue(key, out var el)) return def;
            return el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : el.GetRawText();
        }
    }

    /// 写入一段原始 JSON 文本。
    public static void SetRaw(string key, string rawJson)
    {
        lock (Gate)
        {
            EnsureLoaded();
            try { _data[key] = JsonDocument.Parse(rawJson).RootElement.Clone(); }
            catch { _data[key] = JsonSerializer.SerializeToElement(rawJson); }
            Save();
        }
    }

    public static void Remove(string key)
    {
        lock (Gate)
        {
            EnsureLoaded();
            _data.Remove(key);
            Save();
        }
    }
}
