namespace SciToolbox.Core;

/// <summary>NCBI 分类等级名映射（对应 macOS 版 RankName）。</summary>
public static class RankName
{
    private static readonly Dictionary<string, string> ZhMap = new()
    {
        ["superkingdom"] = "超界", ["kingdom"] = "界", ["subkingdom"] = "亚界",
        ["superphylum"] = "总门", ["phylum"] = "门", ["subphylum"] = "亚门",
        ["superclass"] = "总纲", ["class"] = "纲", ["subclass"] = "亚纲", ["infraclass"] = "下纲",
        ["cohort"] = "股", ["superorder"] = "总目", ["order"] = "目", ["suborder"] = "亚目", ["infraorder"] = "下目",
        ["superfamily"] = "总科", ["family"] = "科", ["subfamily"] = "亚科", ["tribe"] = "族", ["subtribe"] = "亚族",
        ["genus"] = "属", ["subgenus"] = "亚属", ["species"] = "种", ["subspecies"] = "亚种",
        ["variety"] = "变种", ["form"] = "变型", ["strain"] = "菌株", ["biotype"] = "生物型",
        ["clade"] = "支系", ["no rank"] = "未定级", ["isolate"] = "分离株"
    };

    private static readonly Dictionary<string, string> EnMap = new()
    {
        ["superkingdom"] = "Superkingdom", ["kingdom"] = "Kingdom", ["subkingdom"] = "Subkingdom",
        ["superphylum"] = "Superphylum", ["phylum"] = "Phylum", ["subphylum"] = "Subphylum",
        ["superclass"] = "Superclass", ["class"] = "Class", ["subclass"] = "Subclass", ["infraclass"] = "Infraclass",
        ["cohort"] = "Cohort", ["superorder"] = "Superorder", ["order"] = "Order", ["suborder"] = "Suborder", ["infraorder"] = "Infraorder",
        ["superfamily"] = "Superfamily", ["family"] = "Family", ["subfamily"] = "Subfamily", ["tribe"] = "Tribe", ["subtribe"] = "Subtribe",
        ["genus"] = "Genus", ["subgenus"] = "Subgenus", ["species"] = "Species", ["subspecies"] = "Subspecies",
        ["variety"] = "Variety", ["form"] = "Form", ["strain"] = "Strain", ["biotype"] = "Biotype",
        ["clade"] = "Clade", ["no rank"] = "No rank", ["isolate"] = "Isolate"
    };

    public static string Localized(string rank) =>
        (AppLanguage.Cur == AppLanguage.Language.Zh ? ZhMap : EnMap).TryGetValue(rank, out var v) ? v : rank;
}

/// <summary>GTDB 分类等级名映射（对应 macOS 版 GTDBRank）。</summary>
public static class GtdbRank
{
    private static readonly Dictionary<string, string> ZhNames = new()
    {
        ["d"] = "域", ["p"] = "门", ["c"] = "纲", ["o"] = "目", ["f"] = "科", ["g"] = "属", ["s"] = "种"
    };
    private static readonly Dictionary<string, string> EnNames = new()
    {
        ["d"] = "Domain", ["p"] = "Phylum", ["c"] = "Class", ["o"] = "Order", ["f"] = "Family", ["g"] = "Genus", ["s"] = "Species"
    };

    public static string Name(string prefix) =>
        (AppLanguage.Cur == AppLanguage.Language.Zh ? ZhNames : EnNames).TryGetValue(prefix, out var v) ? v : prefix;
}
