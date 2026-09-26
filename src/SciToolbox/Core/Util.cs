using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace SciToolbox.Core;

// MARK: - Clipboard

/// <summary>剪贴板 + Toast 提示（对应 macOS 版 Clipboard / AppToast）。</summary>
public static class ClipboardUtil
{
    /// <summary>复制文本到剪贴板并返回是否成功；失败时提示。</summary>
    public static bool Copy(string? text, string? tip = null)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Clipboard.Clear();
                Clipboard.SetText(text);
            });
            if (tip != null) Toast.Show(tip);
            return true;
        }
        catch
        {
            Toast.Show(L10n.T(L10n.Key.CopyFailed));
            return false;
        }
    }

    public static void ShowToast(string message) => Toast.Show(message);

    public static void ShowUndoableToast(string message, Action undo) => Toast.Show(message, undo);
}

// MARK: - Toast（简单浮层 + 撤销支持）

/// <summary>全局 Toast 服务（对应 macOS 版 AppToast）。UI 层订阅 <see cref="Changed"/> 渲染浮层。</summary>
public sealed class Toast
{
    public static Toast Shared { get; } = new();

    public string? Message { get; private set; }
    public bool IsVisible { get; private set; }
    public Action? UndoAction { get; private set; }

    /// <summary>状态变化通知（message / visible / undo）。</summary>
    public event Action? Changed;

    private System.Timers.Timer? _hideTimer;

    public static void Show(string msg, Action? undo = null) => Shared.ShowInternal(msg, undo);
    public static void ShowUndoableToast(string msg, Action undo) => Shared.ShowInternal(msg, undo);

    private void ShowInternal(string msg, Action? undo)
    {
        _hideTimer?.Dispose();
        UndoAction = undo;
        Message = msg;
        IsVisible = true;
        Raise();
        _hideTimer = new System.Timers.Timer(2000) { AutoReset = false };
        _hideTimer.Elapsed += (_, _) =>
        {
            var disp = Application.Current?.Dispatcher;
            disp?.BeginInvoke(() =>
            {
                IsVisible = false;
                UndoAction = null;
                Raise();
            });
        };
        _hideTimer.Start();
    }

    public void PerformUndo()
    {
        UndoAction?.Invoke();
        _hideTimer?.Dispose();
        IsVisible = false;
        UndoAction = null;
        Raise();
    }

    private void Raise() => Changed?.Invoke();
}

// MARK: - Export helpers

/// <summary>FASTA / BibTeX / RIS / CSV 生成 + 保存面板（对应 macOS 版 ExportUtil）。</summary>
public static class ExportUtil
{
    public static string Fasta(string header, string sequence)
    {
        var sb = new StringBuilder();
        sb.Append('>').Append(header).Append('\n');
        for (int i = 0; i < sequence.Length; i += 60)
            sb.Append(sequence, i, Math.Min(60, sequence.Length - i)).Append('\n');
        return sb.ToString().TrimEnd('\n');
    }

    public static string Bibtex(string pmid, string title, IReadOnlyList<string> authors, string journal, string year)
    {
        var baseKey = (authors.Count > 0 ? authors[0] : "Anonymous") + (string.IsNullOrEmpty(year) ? "nd" : year);
        var key = new string(baseKey.Where(c => char.IsLetterOrDigit(c)).ToArray());
        var sb = new StringBuilder();
        sb.Append('@').Append("article{").Append(key).Append(",\n");
        sb.Append("  title = {").Append(title).Append("},\n");
        sb.Append("  author = {").Append(string.Join(" and ", authors)).Append("},\n");
        sb.Append("  journal = {").Append(journal).Append("},\n");
        sb.Append("  year = {").Append(year).Append("},\n");
        sb.Append("  note = {PMID: ").Append(pmid).Append("}\n");
        sb.Append('}');
        return sb.ToString();
    }

    public static string Ris(string title, IReadOnlyList<string> authors, string journal, string year,
                             string pmid = "", string doi = "", string volume = "", string issue = "", string pages = "")
    {
        var lines = new List<string> { "TY  - JOUR", $"TI  - {title}" };
        foreach (var a in authors) lines.Add($"AU  - {a}");
        if (!string.IsNullOrEmpty(journal)) lines.Add($"JO  - {journal}");
        if (!string.IsNullOrEmpty(year)) lines.Add($"PY  - {year}");
        if (!string.IsNullOrEmpty(volume)) lines.Add($"VL  - {volume}");
        if (!string.IsNullOrEmpty(issue)) lines.Add($"IS  - {issue}");
        if (!string.IsNullOrEmpty(pages)) lines.Add($"SP  - {pages}");
        if (!string.IsNullOrEmpty(pmid)) lines.Add($"AN  - PMID:{pmid}");
        if (!string.IsNullOrEmpty(doi)) lines.Add($"DO  - {doi}");
        lines.Add("ER  - ");
        return string.Join("\n", lines);
    }

    /// 生成 CSV 文本（RFC 4180 风格：含逗号/引号/换行的单元格用双引号包裹并转义）。
    public static string BuildCsv(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        string Escape(string s)
        {
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
        var lines = new List<string> { string.Join(",", header.Select(Escape)) };
        foreach (var r in rows)
            lines.Add(string.Join(",", r.Select(Escape)));
        return string.Join("\n", lines);
    }

    /// 弹出保存面板，将文本写入用户指定文件。返回是否成功保存。
    public static bool SaveTextFile(string content, string defaultName, string ext = "csv")
    {
        bool ok = false;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var panel = new SaveFileDialog
            {
                FileName = SanitizeFileName(defaultName) + "." + ext,
                Filter = FilterFor(ext),
                AddExtension = true,
                DefaultExt = ext
            };
            if (panel.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(panel.FileName, content, new UTF8Encoding(false));
                    ok = true;
                }
                catch
                {
                    ok = false;
                    Toast.Show(L10n.T(L10n.Key.ExportFailed2));
                }
            }
        });
        return ok;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string FilterFor(string ext) => ext.ToLowerInvariant() switch
    {
        "json" => "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
        "csv" => "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
        "fasta" or "fa" or "faa" or "fna" or "ffn" => "FASTA 文件 (*.fasta;*.fa;*.faa;*.fna;*.ffn)|*.fasta;*.fa;*.faa;*.fna;*.ffn|所有文件 (*.*)|*.*",
        "ris" => "RIS 文件 (*.ris)|*.ris|所有文件 (*.*)|*.*",
        "bib" => "BibTeX 文件 (*.bib)|*.bib|所有文件 (*.*)|*.*",
        _ => "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"
    };
}

// MARK: - Network Monitor（离线感知）

/// <summary>网络连通性监控（对应 macOS 版 NetworkMonitor，基于 NWPathMonitor）。</summary>
public sealed class NetworkMonitor
{
    public static NetworkMonitor Shared { get; } = new();

    private bool _isOnline = true;
    public bool IsOnline
    {
        get => _isOnline;
        private set
        {
            if (_isOnline == value) return;
            _isOnline = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;

    private NetworkMonitor()
    {
        try
        {
            NetworkChange.NetworkAddressChanged += (_, _) => Refresh();
            NetworkChange.NetworkAvailabilityChanged += (_, _) => Refresh();
        }
        catch { /* 某些环境不支持事件订阅，忽略 */ }
        Refresh();
    }

    public void Refresh()
    {
        try
        {
            IsOnline = NetworkInterface.GetIsNetworkAvailable();
        }
        catch
        {
            IsOnline = true;
        }
    }
}
