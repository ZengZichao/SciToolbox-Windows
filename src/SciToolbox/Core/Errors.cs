namespace SciToolbox.Core;

/// <summary>
/// Provider 层 UI 文案（KV 键名、区块标题、动作按钮、meta 等）的双语入口（对应 macOS 版 DT）。
/// 与 L10n（枚举字典）互补：此处面向散落在 15 个 Provider 中的数据字面量，直接给出中英两份文案。
/// </summary>
public static class DT
{
    public static string T(string zh, string en) =>
        AppLanguage.Cur == AppLanguage.Language.Zh ? zh : en;
}

/// <summary>网络 / 解析 / HTTP 错误，携带本地化友好描述（对应 macOS 版 APIError）。</summary>
public sealed class ApiException : Exception
{
    public enum KindType { Network, Timeout, Http, Parse, NotFound, InvalidInput, Server }

    public KindType Kind { get; }
    public int StatusCode { get; }

    public ApiException(KindType kind, string message = "", int statusCode = 0) : base(message)
    {
        Kind = kind;
        StatusCode = statusCode;
    }

    public static ApiException Network(string msg) => new(KindType.Network, msg);
    public static ApiException Timeout() => new(KindType.Timeout);
    public static ApiException Http(int code) => new(KindType.Http, "", code);
    public static ApiException Parse(string msg) => new(KindType.Parse, msg);
    public static ApiException NotFound(string msg) => new(KindType.NotFound, msg);
    public static ApiException InvalidInput(string msg) => new(KindType.InvalidInput, msg);
    public static ApiException Server(string msg) => new(KindType.Server, msg);

    /// 本地化的友好错误描述。
    public string FriendlyMessage => Kind switch
    {
        KindType.Network => L10n.T(L10n.Key.NetworkError, Message),
        KindType.Timeout => L10n.T(L10n.Key.TimeoutError),
        KindType.Http => StatusCode switch
        {
            429 => L10n.T(L10n.Key.Http429),
            404 => L10n.T(L10n.Key.Http404),
            >= 500 and <= 599 => L10n.T(L10n.Key.Http5xx, StatusCode),
            _ => L10n.T(L10n.Key.HttpOther, StatusCode)
        },
        KindType.Parse => L10n.T(L10n.Key.ParseError, Message),
        KindType.NotFound => Message,
        KindType.InvalidInput => Message,
        KindType.Server => Message,
        _ => Message
    };
}
