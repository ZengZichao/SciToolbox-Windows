using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SciToolbox.Core;

/// <summary>
/// 统一 HTTP 客户端（对应 macOS 版 APIClient）。
/// 内置 15s 超时、指数退避重试（超时 / 429 / 5xx）、错误友好化、NCBI 节流、磁盘缓存。
/// </summary>
public sealed class ApiClient
{
    public static ApiClient Shared { get; } = new();

    private readonly HttpClient _http;
    private const int MaxRetries = 2;

    /// 全局通知（网络层 → UI），对应 macOS 的 NotificationCenter 事件。
    public static event Action<string>? CacheHit;
    public static event Action<string>? ThrottleWaiting;
    public static event Action<string>? ThrottleResumed;

    private static string Version =
        typeof(ApiClient).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private ApiClient()
    {
        if (string.IsNullOrWhiteSpace(Version)) Version = "1.0.0";
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(16) // 略大于单请求 15s
        };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"SciToolbox/{Version} (Windows)");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json,*/*");
    }

    // MARK: - JSON GET

    public async Task<Json> GetJsonAsync(string urlString, string accept = "application/json,*/*",
                                         CancellationToken ct = default)
    {
        var data = await GetRawAsync(urlString, accept, ct);
        var text = Encoding.UTF8.GetString(data).Trim();
        if (text.Length == 0) return Json.Null; // 空响应体（如 204 No Content）视为空结果
        if (!Json.TryParse(text, out var json))
            throw ApiException.Parse("invalid JSON");
        return json;
    }

    // MARK: - JSON POST

    public async Task<Json> PostJsonAsync(string urlString, object body, string accept = "application/json",
                                          CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, SafeUrl(urlString));
        req.Headers.TryAddWithoutValidation("Accept", accept);
        var jsonBody = JsonSerializer.Serialize(body);
        req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var data = await PerformAsync(req, ct);
        var text = Encoding.UTF8.GetString(data).Trim();
        if (text.Length == 0) return Json.Null; // 空响应体视为无结果
        if (!Json.TryParse(text, out var json))
            throw ApiException.Parse("invalid JSON");
        return json;
    }

    // MARK: - Text GET

    public async Task<string> GetTextAsync(string urlString, CancellationToken ct = default)
    {
        var data = await GetRawAsync(urlString, "text/plain,*/*", ct);
        return Encoding.UTF8.GetString(data);
    }

    // MARK: - Raw GET

    public async Task<byte[]> GetRawAsync(string urlString, string accept = "*/*", CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, SafeUrl(urlString));
        req.Headers.TryAddWithoutValidation("Accept", accept);
        return await PerformAsync(req, ct);
    }

    // MARK: - Core request with retry + cache + throttle

    private async Task<byte[]> PerformAsync(HttpRequestMessage request, CancellationToken ct, int attempt = 0)
    {
        var urlString = request.RequestUri?.AbsoluteUri ?? "";
        var isGet = request.Method == HttpMethod.Get;
        var cacheKey = CacheKey(request);
        var cacheEnabled = ResponseCache.Shared.Enabled;

        // 磁盘缓存：仅 GET，命中直接返回
        if (isGet && cacheEnabled)
        {
            var cached = await ResponseCache.Shared.GetAsync(cacheKey);
            if (cached != null)
            {
                CacheHit?.Invoke(urlString);
                return cached;
            }
        }

        // NCBI 节流：强制 ≥3s 最小请求间隔
        var host = request.RequestUri?.Host ?? "";
        if (host.Contains("eutils.ncbi.nlm.nih.gov"))
        {
            ThrottleWaiting?.Invoke(host);
            await RequestThrottle.WaitIfNeededAsync(host, TimeSpan.FromSeconds(3), ct);
            ThrottleResumed?.Invoke(host);
        }

        try
        {
            var resp = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            int status = (int)resp.StatusCode;

            if (status == 429 || status >= 500)
            {
                resp.Dispose();
                if (attempt < MaxRetries)
                {
                    var delay = RetryDelayFor(status, resp.Headers.RetryAfter, attempt);
                    await Task.Delay(delay, ct);
                    return await PerformAsync(Clone(request), ct, attempt + 1);
                }
                throw ApiException.Http(status);
            }

            if (status < 200 || status > 299)
            {
                var code = status;
                resp.Dispose();
                throw ApiException.Http(code);
            }

            using (resp)
            {
                var data = await resp.Content.ReadAsByteArrayAsync(ct);
                if (isGet && cacheEnabled)
                    await ResponseCache.Shared.SetAsync(cacheKey, data);
                return data;
            }
        }
        catch (ApiException) { throw; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // 超时（非用户取消）→ 重试
            if (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
                await Task.Delay(delay, ct);
                return await PerformAsync(Clone(request), ct, attempt + 1);
            }
            throw ApiException.Timeout();
        }
        catch (HttpRequestException ex)
        {
            throw ApiException.Network(ex.Message);
        }
    }

    private static string SafeUrl(string urlString)
    {
        if (!Uri.TryCreate(urlString, UriKind.Absolute, out _))
            throw ApiException.InvalidInput("invalid URL");
        return urlString;
    }

    private static string CacheKey(HttpRequestMessage request)
    {
        var url = request.RequestUri?.AbsoluteUri ?? "";
        var accept = request.Headers.Accept.ToString();
        if (!request.Headers.TryGetValues("Accept", out var vals))
            accept = "";
        else
            accept = string.Join(",", vals);
        return $"{request.Method} {url} {accept}";
    }

    /// 深拷贝请求（重试用），HttpRequestMessage 不能被重复发送。
    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var h in request.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (request.Content != null)
        {
            var bytes = request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var h in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        return clone;
    }

    /// 429 尊重 Retry-After；5xx：1s、2s、4s；429 无头：0.5s、1s、2s。
    private static TimeSpan RetryDelayFor(int status, RetryConditionHeaderValue? retryAfter, int attempt)
    {
        if (status == 429 && retryAfter != null)
        {
            if (retryAfter.Delta.HasValue) return retryAfter.Delta.Value;
            if (retryAfter.Date.HasValue)
            {
                var wait = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }
        }
        double factor = Math.Pow(2, attempt);
        if (status >= 500) return TimeSpan.FromMilliseconds(factor * 1000);
        return TimeSpan.FromMilliseconds(factor * 500);
    }
}

/// <summary>线程安全的按 host 限速器（对应 macOS 版 RequestThrottle actor）。</summary>
public static class RequestThrottle
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, DateTime> Last = new();

    public static async Task WaitIfNeededAsync(string host, TimeSpan minInterval, CancellationToken ct = default)
    {
        TimeSpan toWait;
        lock (Gate)
        {
            var now = DateTime.UtcNow;
            if (Last.TryGetValue(host, out var last))
            {
                var elapsed = now - last;
                toWait = elapsed < minInterval ? minInterval - elapsed : TimeSpan.Zero;
            }
            else
            {
                toWait = TimeSpan.Zero;
            }
            Last[host] = now + toWait; // 预约下一次
        }
        if (toWait > TimeSpan.Zero)
            await Task.Delay(toWait, ct);
    }
}
