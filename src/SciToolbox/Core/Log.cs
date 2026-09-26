namespace SciToolbox.Core;

/// <summary>统一落盘日志，供全局异常处理与命令兜底使用。</summary>
public static class Log
{
    public static void Write(string source, Exception? ex)
    {
        if (ex == null) return;
        try
        {
            var path = System.IO.Path.Combine(Prefs.Dir, "crash.log");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:O}] {source}: {ex}\n\n");
        }
        catch { }
    }
}
