using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SciToolbox.Core;

/// <summary>
/// 磁盘缓存（TTL，无用户数据，对应 macOS 版 ResponseCache）。
/// 仅存「请求 → 响应」，绝不记录任何用户/设备标识。
/// </summary>
public sealed class ResponseCache
{
    public static ResponseCache Shared { get; } = new();

    private readonly string _directory;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(2);

    private ResponseCache()
    {
        var dir = Path.Combine(Prefs.Dir, "Cache");
        Directory.CreateDirectory(dir);
        _directory = dir;
    }

    public bool Enabled
    {
        get => Prefs.GetBool("cacheEnabled", true);
        set => Prefs.SetBool("cacheEnabled", value);
    }

    public async Task<byte[]?> GetAsync(string key)
    {
        var file = PathFor(key);
        try
        {
            if (!File.Exists(file)) return null;
            var meta = new FileInfo(file);
            var ttl = TtlFor(key);
            if (DateTime.UtcNow - meta.LastWriteTimeUtc > ttl)
            {
                try { File.Delete(file); } catch { }
                return null;
            }
            return await Task.Run(() => File.ReadAllBytes(file));
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAsync(string key, byte[] data)
    {
        var file = PathFor(key);
        await Task.Run(() =>
        {
            try { File.WriteAllBytes(file, data); } catch { }
        });
    }

    public void Clear()
    {
        try
        {
            if (Directory.Exists(_directory))
                foreach (var f in Directory.GetFiles(_directory))
                    try { File.Delete(f); } catch { }
        }
        catch { }
    }

    private string PathFor(string key)
    {
        // 用 SHA256 十六进制摘要作文件名（与 macOS P3-5 一致）
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(_directory, hash + ".cache");
    }

    private TimeSpan TtlFor(string key)
    {
        // 搜索结果 TTL 更短（30 分钟），稳定数据用默认 2 小时
        if (key.Contains("search")) return TimeSpan.FromMinutes(30);
        return _defaultTtl;
    }
}
