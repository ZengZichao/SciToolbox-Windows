using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SciToolbox.Core;

/// <summary>外观主题模式。</summary>
public enum AppThemeMode { System, Light, Dark }

/// <summary>侧边栏密度（对应 macOS 版 SidebarDensity）。</summary>
public enum SidebarDensity { Compact, Normal, Spacious }

public static class SidebarDensityExtensions
{
    public static double FontSize(this SidebarDensity d) => d switch
    { SidebarDensity.Compact => 12, SidebarDensity.Spacious => 14, _ => 13 };
    public static double IconSize(this SidebarDensity d) => d switch
    { SidebarDensity.Compact => 11, SidebarDensity.Spacious => 14, _ => 12 };
    public static double MinRowHeight(this SidebarDensity d) => d switch
    { SidebarDensity.Compact => 26, SidebarDensity.Spacious => 36, _ => 30 };
    public static double Indent(this SidebarDensity d) => d switch
    { SidebarDensity.Compact => 10, SidebarDensity.Spacious => 14, _ => 12 };
}

/// <summary>
/// 主题管理器（对应 macOS 版 Theme.swift 的自适应色）。
/// 运行时把设计令牌写入 Application.Current.Resources，UI 用 DynamicResource 绑定即可随主题/语言刷新。
/// </summary>
public static class ThemeManager
{
    public static event Action? Changed;

    public static bool IsDark { get; private set; }

    /// <summary>强调色（浅色 / 深色各取一版，深色下提亮以保证作为文字时的对比达 WCAG AA）。</summary>
    private static Color AccentColor(bool dark) => dark ? C(0x5A, 0xA8, 0xDF) : C(0x17, 0x73, 0xB8);

    /// <summary>实心填充用的品牌蓝：不随主题变亮，始终与白色文字保持足够对比。</summary>
    private static Color AccentFillColor() => C(0x17, 0x73, 0xB8);

    public static AppThemeMode Mode
    {
        get => Enum.TryParse<AppThemeMode>(Prefs.GetString("appTheme", "System"), out var m) ? m : AppThemeMode.System;
        set { Prefs.SetString("appTheme", value.ToString()); Apply(); }
    }

    public static SidebarDensity Density
    {
        get => Enum.TryParse<SidebarDensity>(Prefs.GetString("sidebarDensity", "Normal"), out var d) ? d : SidebarDensity.Normal;
        set { Prefs.SetString("sidebarDensity", value.ToString()); Apply(); }
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = key?.GetValue("AppsUseLightTheme");
            if (v is int i) return i == 0;
        }
        catch { }
        return false;
    }

    private static bool _tracking;

    /// <summary>跟随系统深浅色：订阅 Windows 用户偏好变更，运行中即时换肤。</summary>
    public static void StartSystemTracking()
    {
        if (_tracking) return;
        _tracking = true;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (Mode != AppThemeMode.System) return;
            if (e.Category != Microsoft.Win32.UserPreferenceCategory.General &&
                e.Category != Microsoft.Win32.UserPreferenceCategory.Color) return;
            if (SystemPrefersDark() == IsDark) return;
            Apply();
        };
    }

    public static void Apply()
    {
        IsDark = Mode switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            _ => SystemPrefersDark()
        };

        var res = Application.Current.Resources;

        // 基础文本色（ink）
        Color ink = IsDark ? C(255, 255, 255) : C(28, 28, 30);
        Put(res, "Brush.Window", IsDark ? C(30, 30, 30) : C(255, 255, 255));
        Put(res, "Brush.Sidebar", IsDark ? C(28, 28, 28) : C(246, 246, 246));
        Put(res, "Brush.Card", IsDark ? C(42, 42, 42) : C(239, 239, 239));

        Put(res, "Brush.Ink", ink);
        PutAlpha(res, "Brush.Ink2", ink, 0.75);
        PutAlpha(res, "Brush.Ink3", ink, 0.70);
        PutAlpha(res, "Brush.Ink4", ink, 0.25);   // 仅装饰性

        PutAlpha(res, "Brush.Line", ink, 0.12);
        PutAlpha(res, "Brush.LineStrong", ink, 0.18);
        PutAlpha(res, "Brush.Divider", ink, 0.15);
        PutAlpha(res, "Brush.DividerSoft", ink, 0.10);
        PutAlpha(res, "Brush.Hover", ink, 0.06);

        Put(res, "Brush.Accent", AccentColor(IsDark));
        Put(res, "Brush.FocusRing", AccentColor(IsDark));
        // 实心控件（主按钮 / 选中态药丸 / 复选框 / 分类胶囊）上的文字恒为白色
        Put(res, "Brush.OnAccent", Colors.White);
        // 承载 OnAccent 文字的填充色：恒用较深的品牌蓝，保证白字对比度达 WCAG AA；
        // 文字与图标形式的强调色则用 AccentColor（深色模式下提亮）。
        Put(res, "Brush.AccentFill", AccentFillColor());
        PutAlpha(res, "Brush.AccentTint", AccentColor(IsDark), IsDark ? 0.22 : 0.12);

        Put(res, "Brush.SemanticError", IsDark ? C(0xD9, 0x4D, 0x47) : C(0xB8, 0x29, 0x24));
        Put(res, "Brush.SemanticWarning", IsDark ? C(0xD9, 0x8C, 0x33) : C(0x9E, 0x59, 0x0D));
        Put(res, "Brush.WarningBanner", C(0x99, 0x59, 0x1A));

        // 选中项底色（列表 / 结果行）
        PutAlpha(res, "Brush.Selected", AccentColor(IsDark), IsDark ? 0.20 : 0.14);

        // 分类色：文字 / 图标用（深色模式提亮）
        Put(res, "Brush.Category.Taxonomy", IsDark ? C(0x59, 0x9E, 0xF2) : C(0x1A, 0x61, 0xAD));
        Put(res, "Brush.Category.Gene", IsDark ? C(0x4D, 0xBF, 0x80) : C(0x21, 0x73, 0x40));
        Put(res, "Brush.Category.Protein", IsDark ? C(0xF2, 0xA6, 0x4D) : C(0xB8, 0x61, 0x0D));
        Put(res, "Brush.Category.Function", IsDark ? C(0x40, 0xBF, 0xBF) : C(0x0D, 0x73, 0x73));
        Put(res, "Brush.Category.Literature", IsDark ? C(0xB3, 0x73, 0xD9) : C(0x85, 0x47, 0xA8));

        // 分类色填充版：恒用深色档，供白字胶囊使用
        Put(res, "Brush.CategoryFill.Taxonomy", C(0x1A, 0x61, 0xAD));
        Put(res, "Brush.CategoryFill.Gene", C(0x21, 0x73, 0x40));
        Put(res, "Brush.CategoryFill.Protein", C(0xB8, 0x61, 0x0D));
        Put(res, "Brush.CategoryFill.Function", C(0x0D, 0x73, 0x73));
        Put(res, "Brush.CategoryFill.Literature", C(0x85, 0x47, 0xA8));

        // 品牌渐变
        var grad = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(AccentFillColor(), 0),
                new(C(0x26, 0xA6, 0xA6), 1)
            },
            new Point(0, 0), new Point(1, 1));
        grad.Freeze();
        res["Brush.BrandGradient"] = grad;

        // 文本色快捷（用于纯代码设置 Foreground）
        res["Color.Ink"] = ink;

        // 密度令牌：设置页改 Density 后，侧边栏等 UI 通过 DynamicResource 直接响应
        var d = Density;
        res["Density.RowHeight"] = d.MinRowHeight();
        res["Density.FontSize"] = d.FontSize();
        res["Density.IconSize"] = d.IconSize();
        res["Density.Indent"] = d.Indent();

        Changed?.Invoke();
    }

    public static Brush CategoryBrush(ToolCategory cat) =>
        Application.Current.Resources[CategoryKey(cat)] as Brush ?? AccentBrush;

    /// <summary>承载白色文字的胶囊 / 标签底色。</summary>
    public static Brush CategoryFillBrush(ToolCategory cat) =>
        Application.Current.Resources["Brush.CategoryFill." + cat] as Brush
        ?? Application.Current.Resources["Brush.AccentFill"] as Brush
        ?? AccentBrush;

    private static string CategoryKey(ToolCategory cat) => cat switch
    {
        ToolCategory.Taxonomy => "Brush.Category.Taxonomy",
        ToolCategory.Gene => "Brush.Category.Gene",
        ToolCategory.Protein => "Brush.Category.Protein",
        ToolCategory.Function => "Brush.Category.Function",
        ToolCategory.Literature => "Brush.Category.Literature",
        _ => "Brush.Accent"
    };

    public static SolidColorBrush Brush(string key) =>
        Application.Current.Resources[key] as SolidColorBrush
        ?? new SolidColorBrush(key == "Brush.Ink" ? Color.FromRgb(28, 28, 30) : Color.FromRgb(0x17, 0x73, 0xB8));
    public static SolidColorBrush AccentBrush => Brush("Brush.Accent");
    public static Brush AccentFillBrush =>
        Application.Current.Resources["Brush.AccentFill"] as Brush ?? AccentBrush;
    public static Brush BrandGradient => (Brush)Application.Current.Resources["Brush.BrandGradient"];

    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static void Put(ResourceDictionary res, string key, Color color)
    {
        var br = new SolidColorBrush(color);
        br.Freeze();
        res[key] = br;
    }

    private static void PutAlpha(ResourceDictionary res, string key, Color baseColor, double opacity)
    {
        var c = Color.FromArgb((byte)Math.Round(opacity * 255), baseColor.R, baseColor.G, baseColor.B);
        var br = new SolidColorBrush(c);
        br.Freeze();
        res[key] = br;
    }

    // SF Symbols 名 → Segoe Fluent / MDL2 字形映射（对应 macOS iconName）
    private static readonly Dictionary<string, string> IconGlyphs = new()
    {
        ["atom"] = "\uE945",           // protein
        ["flask"] = "\uE9EB",          // gene / sequence
        ["cube"] = "\uE809",           // pdb structure
        ["doc.text"] = "\uE8A5",       // pubmed / literature
        ["text.book.closed"] = "\uE82C",
        ["tree"] = "\uE8FD",           // taxonomy
        ["puzzle"] = "\uEA86",         // function / domain
        ["lightbulb"] = "\uEA80",
        ["globe"] = "\uE774",          // gbif
        ["list.bullet"] = "\uEA37",
        ["doc.on.doc"] = "\uE8C8",
        ["camera"] = "\uE722",
        ["magnifyingglass"] = "\uE721",
        ["books.vertical"] = "\uE8F1",
        ["cpu"] = "\uE950",
    };

    public static string GlyphFor(string iconName) =>
        IconGlyphs.TryGetValue(iconName, out var g) ? g : "\uE721";
}
