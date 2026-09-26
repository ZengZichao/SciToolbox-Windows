using SciToolbox.Core;

namespace SciToolbox.Core;

/// <summary>相对时间格式化（对应 macOS 版 RelativeDateTimeFormatter，随语言切换）。</summary>
public static class RelativeTime
{
    public static string Format(DateTime offset, DateTime? reference = null)
    {
        var now = reference ?? DateTime.Now;
        var ts = offset - now;
        bool future = ts.TotalSeconds >= 0;
        var abs = ts.Duration();
        bool zh = AppLanguage.Cur == AppLanguage.Language.Zh;

        string unit(int n, string zhUnit, string enUnit) =>
            zh ? $"{n}{zhUnit}" : $"{n} {enUnit}{(n == 1 ? "" : "s")}";

        string s;
        if (abs.TotalSeconds < 45) s = zh ? "刚刚" : "just now";
        else if (abs.TotalMinutes < 60) s = unit(Math.Max(1, (int)abs.TotalMinutes), " 分钟前", "min ago");
        else if (abs.TotalHours < 24) s = unit((int)abs.TotalHours, " 小时前", "hr ago");
        else if (abs.TotalDays < 30) s = unit((int)abs.TotalDays, " 天前", "day ago");
        else if (abs.TotalDays < 365) s = unit((int)(abs.TotalDays / 30), " 个月前", "month ago");
        else s = unit((int)(abs.TotalDays / 365), " 年前", "year ago");

        if (!future && abs.TotalSeconds >= 45)
            s = zh ? s : s; // 英文 "min ago" 已含方向；中文「前」
        else if (future && abs.TotalSeconds >= 45)
            s = zh ? s.Replace("前", "后") : "in " + s;

        return s;
    }
}
