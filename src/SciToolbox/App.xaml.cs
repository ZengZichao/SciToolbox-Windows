using System.Windows;
using System.Windows.Threading;
using SciToolbox.Core;

namespace SciToolbox;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Write("AppDomain", args.ExceptionObject as Exception);

        // 诊断模式：--selftest [toolId] [query]  —— 无头跑一次检索+详情，落盘 JSON 后退出
        if (e.Args.Length > 0 && e.Args[0] == "--selftest")
        {
            // 在线程池上下文执行，避免在 UI 线程 GetResult 造成同步上下文死锁
            System.Threading.Tasks.Task.Run(() => RunSelfTestAsync(e)).GetAwaiter().GetResult();
            Shutdown();
            return;
        }

        ThemeManager.Apply();
        ThemeManager.StartSystemTracking();
        AppLanguage.Shared.Changed += () => Dispatcher.Invoke(() => ThemeManager.Apply());
        NetworkMonitor.Shared.Refresh();

        if (IsTraceRequested(e.Args)) TraceLog.Attach();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // 冒烟模式：--smoke —— 依次打开每个页面并造出收藏 / 集合 / 对比数据，
        // 强制所有视图模板与行模板实例化，配合 --trace 验证界面绑定是否全部有效。
        if (e.Args.Length > 0 && e.Args[0] == "--smoke")
        {
            SeedSmokeData();
            var ids = new[] { "home", "favorites", "history", "collections", "settings", "uniprot", "pubmed", "pdb", "global" };
            int i = 0;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(350)
            };
            timer.Tick += (_, _) =>
            {
                if (i < ids.Length)
                {
                    if (ids[i] == "global") window.Vm.OpenGlobalSearch("test");
                    else window.Vm.OpenToolById(ids[i]);
                    if (ids[i] == "uniprot") window.Vm.SearchVmInstance?.Prefill("P12345", true);
                    if (ids[i] == "collections") window.Vm.CollectionsVmInstance.SelectCollection(SciToolbox.Core.CollectionStore.Shared.Collections[0].Id);
                    i++;
                    return;
                }
                timer.Stop();
                window.Vm.ShowShortcuts = true;
                window.Vm.ShowCommandPalette = true;
                window.Vm.ShowCommandPalette = false;
                window.Vm.ShowShortcuts = false;
                Shutdown();
            };
            timer.Start();
        }
        // 演示模式：--demo [toolId] [query] —— 自动导航+检索+开详情（用于确定性验证）
        if (e.Args.Length > 0 && e.Args[0] == "--demo")
        {
            var tool = e.Args.Length > 1 ? e.Args[1] : "uniprot";
            var q = e.Args.Length > 2 ? e.Args[2] : "P12345";
            window.Vm.StartDemo(tool, q);
        }
    }

    private static bool IsTraceRequested(string[] args) => args.Contains("--trace");

    /// <summary>为 --smoke 造一点本地数据，让收藏 / 集合 / 对比的行模板真正被实例化。</summary>
    private static void SeedSmokeData()
    {
        SciToolbox.Core.LocalFavorites.Shared.Add("uniprot", "UniProt", "P12345", "Smoke favorite", "subtitle");
        SciToolbox.Core.SearchHistory.Shared.Add("uniprot", "UniProt", "smoke-query");

        var store = SciToolbox.Core.CollectionStore.Shared;
        if (store.Collections.Count == 0)
        {
            var c = store.Create("Smoke collection");
            store.AddEntries(new[]
            {
                new SciToolbox.Core.CollectionEntry
                {
                    ToolId = "uniprot", ToolName = "UniProt", ItemId = "P12345",
                    Title = "Smoke entry", Subtitle = "entry subtitle", Note = "note",
                    Context = new System.Collections.Generic.Dictionary<string, string> { ["accession"] = "P12345" }
                }
            }, c.Id);
        }

        var model = new SciToolbox.Core.DetailModel
        {
            HeaderTitle = "Compare smoke",
            Sections = new() { new SciToolbox.Core.KVSection { Title = "S", Rows = new() { new SciToolbox.Core.KVRow("K", "V", false, null, null) } } }
        };
        SciToolbox.Core.ComparisonStore.Shared.Add("uniprot", "UniProt", SciToolbox.Core.ToolCategory.Protein,
            "P12345", "Compare smoke", null, model);
    }
    private async Task RunSelfTestAsync(StartupEventArgs e)
    {
        var toolId = e.Args.Length > 1 ? e.Args[1] : "uniprot";
        var query = e.Args.Length > 2 ? e.Args[2] : "P12345";
        var report = new System.Collections.Generic.Dictionary<string, object?>();
        try
        {
            var provider = ToolRegistry.Shared.Find(toolId);
            report["tool"] = toolId;
            report["query"] = query;
            if (provider == null) { report["error"] = "provider not found"; }
            else
            {
                var res = await provider.SearchAsync(query);
                report["resultCount"] = res.Items.Count;
                report["firstTitle"] = res.Items.Count > 0 ? res.Items[0].Title : null;
                report["firstId"] = res.Items.Count > 0 ? res.Items[0].Id : null;
                if (res.Items.Count > 0)
                {
                    var first = res.Items[0];
                    var detail = await provider.DetailAsync(first.Id, first.Extra);
                    report["detailHeader"] = detail.HeaderTitle;
                    report["detailSections"] = detail.Sections.Count;
                    report["detailXlinks"] = detail.XLinks.Count;
                }
                report["ok"] = true;
            }
        }
        catch (Exception ex) { report["ok"] = false; report["exception"] = ex.ToString(); }
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(System.IO.Path.Combine(Prefs.Dir, "selftest.json"), json);
        }
        catch { }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Write("Dispatcher", e.Exception);
        e.Handled = true; // keep app alive for diagnosis
    }
}

/// <summary>把 WPF 绑定/资源错误写进日志（--trace 时启用），用于验证界面绑定是否全部生效。</summary>
internal static class TraceLog
{
    private static readonly System.Collections.Generic.List<string> Errors = new();

    public static void Attach()
    {
        System.Diagnostics.PresentationTraceSources.Refresh();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level =
            System.Diagnostics.SourceLevels.Warning;
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(new Listener());
    }

    public static void Record(string line)
    {
        lock (Errors)
        {
            Errors.Add(line);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Prefs.Dir, "binding.log"), line + "\n"); }
            catch { }
        }
    }

    private sealed class Listener : System.Diagnostics.TraceListener
    {
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            Record(message);
        }
    }
}
