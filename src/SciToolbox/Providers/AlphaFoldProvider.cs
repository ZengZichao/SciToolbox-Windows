using System.Globalization;
using SciToolbox.Core;

namespace SciToolbox.Providers;

/// <summary>AlphaFold 结构数据库 — EBI AlphaFold DB REST API（对应 AlphaFoldProvider.swift）。</summary>
public sealed class AlphaFoldProvider : ToolProviderBase
{
    public override string Id => "alphafold";
    public override string Name => DT.T("AlphaFold 结构", "AlphaFold Structure");
    public override ToolCategory Category => ToolCategory.Protein;
    public override string IconName => "cube";
    public override string Placeholder => DT.T("UniProt Accession（如 P04637）", "UniProt Accession (e.g. P04637)");
    public override string DataSourceNote => DT.T("数据来源：AlphaFold DB (alphafold.ebi.ac.uk)", "Data source: AlphaFold DB (alphafold.ebi.ac.uk)");

    private const string Base = "https://alphafold.ebi.ac.uk/api";

    public override async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var accession = query.Trim().ToUpperInvariant();
        if (accession.Length == 0)
            throw ApiException.InvalidInput(DT.T("请输入 UniProt Accession", "Enter a UniProt Accession"));

        var url = $"{Base}/prediction/{Urls.EncodePath(accession)}";
        Json data;
        try
        {
            data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        }
        catch (ApiException ex) when (ex.Kind == ApiException.KindType.Http && ex.StatusCode == 404)
        {
            return new SearchResult { Items = new(), Total = 0 };
        }

        var arr = data.Array;
        var rec = arr.Count > 0 ? arr[0] : data;
        var uniProtAccession = rec["uniprotAccession"].StrOr("");
        if (uniProtAccession.Length == 0) return new SearchResult { Items = new(), Total = 0 };

        var organism = rec["organismScientificName"].StrOr("");
        var modelVersion = rec["latestVersion"].IntOr(0).ToString(CultureInfo.InvariantCulture);
        var avgPlddt = (rec["globalMetricValue"].Double ?? 0).ToString("F1", CultureInfo.InvariantCulture);
        var uniprotDescription = rec["uniprotDescription"].StrOr("");
        var gene = rec["gene"].StrOr("");

        var item = new ResultItem
        {
            Id = uniProtAccession,
            Title = uniprotDescription.Length == 0 ? uniProtAccession : uniprotDescription,
            Subtitle = organism.Length == 0 ? gene : $"{gene} · {organism}",
            Badge = uniProtAccession,
            Meta = $"v{modelVersion} · pLDDT {avgPlddt}",
            Extra = new Dictionary<string, string> { ["accession"] = uniProtAccession }
        };
        return new SearchResult { Items = new List<ResultItem> { item }, Total = 1 };
    }

    public override async Task<DetailModel> DetailAsync(string id, Dictionary<string, string>? context, CancellationToken ct = default)
    {
        var accession = ((context != null && context.TryGetValue("accession", out var a) ? a : id)).ToUpperInvariant();
        var url = $"{Base}/prediction/{Urls.EncodePath(accession)}";
        var data = await ApiClient.Shared.GetJsonAsync(url, ct: ct);
        var arr = data.Array;
        var rec = arr.Count > 0 ? arr[0] : data;

        var uniProtAccession = rec["uniprotAccession"].StrOr(accession);
        var organism = rec["organismScientificName"].StrOr("");
        var taxId = rec["taxId"].IntOr(0).ToString(CultureInfo.InvariantCulture);
        var modelVersion = rec["latestVersion"].IntOr(0).ToString(CultureInfo.InvariantCulture);
        var uniprotDescription = rec["uniprotDescription"].StrOr("");
        var gene = rec["gene"].StrOr("");
        var uniprotId = rec["uniprotId"].StrOr("");
        var isReviewed = rec["isReviewed"].Bool ?? false;
        var modelCreatedDate = rec["modelCreatedDate"].StrOr("");
        var sequenceVersionDate = rec["sequenceVersionDate"].StrOr("");
        var sequence = rec["sequence"].StrOr("");
        var entryId = rec["entryId"].StrOr("");
        var pdbUrl = rec["pdbUrl"].StrOr("");
        var paeImageUrl = rec["paeImageUrl"].StrOr("");

        var plddt = rec["globalMetricValue"].Double;
        var fractionVeryLow = rec["fractionPlddtVeryLow"].Double;
        var fractionLow = rec["fractionPlddtLow"].Double;
        var fractionConfident = rec["fractionPlddtConfident"].Double;
        var fractionVeryHigh = rec["fractionPlddtVeryHigh"].Double;

        string Pct(double? f) => f.HasValue ? (f.Value * 100).ToString("F1", CultureInfo.InvariantCulture) + "%" : "";

        var infoRows = new List<KVRow>
        {
            new("UniProt", uniProtAccession, copyable: true, xlinkTarget: ProviderHelpers.Xlink("uniprot", uniProtAccession, $"UniProt: {uniProtAccession}")),
            new(DT.T("描述", "Description"), uniprotDescription),
        };
        if (gene.Length > 0) infoRows.Add(new KVRow(DT.T("基因", "Gene"), gene));
        if (uniprotId.Length > 0) infoRows.Add(new KVRow("UniProt ID", uniprotId, copyable: true));
        infoRows.Add(new KVRow(DT.T("物种", "Organism"), organism));
        infoRows.Add(new KVRow(DT.T("模型版本", "Model version"), "v" + modelVersion));
        if (modelCreatedDate.Length > 0) infoRows.Add(new KVRow(DT.T("模型创建日期", "Model creation date"), modelCreatedDate));
        if (sequenceVersionDate.Length > 0) infoRows.Add(new KVRow(DT.T("序列版本日期", "Sequence version date"), sequenceVersionDate));
        infoRows.Add(new KVRow(DT.T("已评审", "Reviewed"), isReviewed ? DT.T("是", "Yes") : DT.T("否", "No")));
        if (taxId.Length > 0 && taxId != "0") infoRows.Add(new KVRow("TaxID", taxId, copyable: true,
            xlinkTarget: ProviderHelpers.Xlink("ncbi_taxonomy", taxId, DT.T($"NCBI 分类: {organism}", $"NCBI Taxonomy: {organism}"))));
        if (entryId.Length > 0) infoRows.Add(new KVRow("Entry ID", entryId, copyable: true));

        var scoreRows = new List<KVRow>();
        if (plddt.HasValue) scoreRows.Add(new KVRow("pLDDT", plddt.Value.ToString("F2", CultureInfo.InvariantCulture)));
        if (fractionVeryHigh.HasValue) scoreRows.Add(new KVRow(DT.T("极高置信度", "Very high confidence"), Pct(fractionVeryHigh)));
        if (fractionConfident.HasValue) scoreRows.Add(new KVRow(DT.T("高置信度", "High confidence"), Pct(fractionConfident)));
        if (fractionLow.HasValue) scoreRows.Add(new KVRow(DT.T("低置信度", "Low confidence"), Pct(fractionLow)));
        if (fractionVeryLow.HasValue) scoreRows.Add(new KVRow(DT.T("极低置信度", "Very low confidence"), Pct(fractionVeryLow)));

        var sections = new List<KVSection> { new(DT.T("基本信息", "Basic Info"), infoRows) };
        if (scoreRows.Count > 0) sections.Add(new KVSection(DT.T("置信度评分", "Confidence Scores"), scoreRows));

        if (sequence.Length > 0)
        {
            var seqRows = new List<KVRow>
            {
                new(DT.T("长度", "Length"), sequence.Length + " aa"),
                new(DT.T("序列", "Sequence"), new string(sequence.Take(200).ToArray()) + (sequence.Length > 200 ? "…" : ""), copyable: true)
            };
            sections.Add(new KVSection(DT.T("序列", "Sequence"), seqRows));
        }

        var xlinks = new List<XLink> { ProviderHelpers.Xlink("uniprot", uniProtAccession, $"UniProt: {uniProtAccession}") };
        if (taxId.Length > 0 && taxId != "0")
            xlinks.Add(ProviderHelpers.Xlink("ncbi_taxonomy", taxId, DT.T($"NCBI 分类: {organism}", $"NCBI Taxonomy: {organism}")));

        var actions = new List<DetailAction> { new(DT.T("复制 Accession", "Copy Accession"), uniProtAccession, DetailAction.ActionStyleKind.Secondary) };
        if (sequence.Length > 0)
        {
            var header = $">{uniProtAccession} {uniprotDescription}";
            actions.Add(new DetailAction(DT.T("复制序列", "Copy Sequence"), sequence, DetailAction.ActionStyleKind.Primary));
            actions.Add(new DetailAction(DT.T("复制 FASTA", "Copy FASTA"), ExportUtil.Fasta(header, sequence), DetailAction.ActionStyleKind.Secondary));
        }
        if (pdbUrl.Length > 0)
        {
            actions.Add(new DetailAction(DT.T("复制 PDB URL", "Copy PDB URL"), pdbUrl, DetailAction.ActionStyleKind.Secondary));
            sections[0].Rows.Add(new KVRow(DT.T("PDB 结构", "PDB Structure"), pdbUrl, copyable: true, link: pdbUrl));
        }

        var imageUrl = paeImageUrl.Length == 0
            ? $"https://alphafold.ebi.ac.uk/api/preview/{uniProtAccession}"
            : paeImageUrl;

        var headerMeta = new List<string>();
        if (organism.Length > 0) headerMeta.Add(organism);
        headerMeta.Add("v" + modelVersion);

        return new DetailModel
        {
            HeaderTitle = uniprotDescription.Length == 0 ? uniProtAccession : uniprotDescription,
            HeaderSubtitle = uniProtAccession,
            HeaderMeta = headerMeta,
            Sections = sections,
            Actions = actions,
            XLinks = xlinks,
            WebUrl = $"https://alphafold.ebi.ac.uk/entry/{uniProtAccession}",
            ImageUrl = imageUrl
        };
    }
}
