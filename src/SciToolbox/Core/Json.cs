using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace SciToolbox.Core;

/// <summary>
/// 动态 JSON 辅助包装器（对应 macOS 版 Models.swift 的 <c>JSON</c> 结构）。
/// 用于灵活解析各数据库的复杂响应，字段缺失时安全返回 null / 默认值。
/// 基于 System.Text.Json 的 <see cref="JsonElement"/>。
/// </summary>
public sealed class Json
{
    public static readonly Json Null = new(default);

    private readonly JsonElement _el;

    public Json(JsonElement el) => _el = el;

    public static Json Parse(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return new Json(doc.RootElement.Clone());
        }
        catch
        {
            return Null;
        }
    }

    public static bool TryParse(string text, out Json json)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            json = new Json(doc.RootElement.Clone());
            return true;
        }
        catch
        {
            json = Null;
            return false;
        }
    }

    private bool HasValue => _el.ValueKind != JsonValueKind.Undefined;

    public bool IsNull => !HasValue || _el.ValueKind == JsonValueKind.Null;

    /// 字符串提取：string 直接返回；number 返回其字面量；其余 null。
    public string? String
    {
        get
        {
            if (!HasValue) return null;
            switch (_el.ValueKind)
            {
                case JsonValueKind.String: return _el.GetString();
                case JsonValueKind.Number: return _el.GetRawText();
                case JsonValueKind.True: return "true";
                case JsonValueKind.False: return "false";
                default: return null;
            }
        }
    }

    public string StrOr(string def) => String ?? def;

    /// 整数提取：number 取整；字符串若能解析也返回（NCBI count/slen 等为字符串）。
    public int? Int
    {
        get
        {
            if (!HasValue) return null;
            if (_el.ValueKind == JsonValueKind.Number && _el.TryGetInt32(out var i)) return i;
            if (_el.ValueKind == JsonValueKind.Number) return (int)_el.GetDouble();
            if (_el.ValueKind == JsonValueKind.String &&
                int.TryParse(_el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var si)) return si;
            return null;
        }
    }

    public int IntOr(int def) => Int ?? def;

    public double? Double
    {
        get
        {
            if (!HasValue) return null;
            if (_el.ValueKind == JsonValueKind.Number) return _el.GetDouble();
            if (_el.ValueKind == JsonValueKind.String &&
                double.TryParse(_el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
            return null;
        }
    }

    public bool? Bool
    {
        get
        {
            if (!HasValue) return null;
            switch (_el.ValueKind)
            {
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Number: return _el.GetDouble() != 0;
                case JsonValueKind.String:
                    var s = _el.GetString();
                    if (bool.TryParse(s, out var b)) return b;
                    return null;
                default: return null;
            }
        }
    }

    public IReadOnlyList<Json> Array
    {
        get
        {
            var list = new List<Json>();
            if (HasValue && _el.ValueKind == JsonValueKind.Array)
                foreach (var e in _el.EnumerateArray())
                    list.Add(new Json(e));
            return list;
        }
    }

    public Dictionary<string, Json> Dict
    {
        get
        {
            var d = new Dictionary<string, Json>();
            if (HasValue && _el.ValueKind == JsonValueKind.Object)
                foreach (var p in _el.EnumerateObject())
                    d[p.Name] = new Json(p.Value);
            return d;
        }
    }

    /// 按 key 索引；不存在返回 Null。
    public Json this[string key]
    {
        get
        {
            if (HasValue && _el.ValueKind == JsonValueKind.Object &&
                _el.TryGetProperty(key, out var child))
                return new Json(child);
            return Null;
        }
    }

    /// 按下标索引（数组）；越界返回 Null。
    public Json this[int index]
    {
        get
        {
            if (!HasValue || _el.ValueKind != JsonValueKind.Array) return Null;
            if (index < 0) return Null;
            int i = 0;
            foreach (var e in _el.EnumerateArray())
            {
                if (i == index) return new Json(e);
                i++;
            }
            return Null;
        }
    }

    public bool IsEmpty
    {
        get
        {
            if (!HasValue) return true;
            switch (_el.ValueKind)
            {
                case JsonValueKind.Null: return true;
                case JsonValueKind.Object: return !_el.EnumerateObject().Any();
                case JsonValueKind.Array: return !_el.EnumerateArray().Any();
                case JsonValueKind.String: return _el.GetString()?.Length == 0;
                default: return false;
            }
        }
    }

    /// 数组元素数量（非数组返回 0）。
    public int Count
    {
        get
        {
            if (!HasValue || _el.ValueKind != JsonValueKind.Array) return 0;
            return _el.GetArrayLength();
        }
    }
}
