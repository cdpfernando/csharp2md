using System.Text.Json;
using System.Text.Json.Serialization;

namespace Csharp2Md.Analysis.Tests.Certification;

/// <summary>
/// Reads the labeled corpora authored in T53 for T54's engine-certification runner. Read-only: this
/// type never writes a label, and nothing in the production pipeline references it -- the corpora are
/// not product surface (D-03, GCPC-081).
/// </summary>
internal static class LabeledCorpusReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ImmutableArray<LabeledCorpusEntry> Read(CertifiedArea area)
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "CertificationCorpus",
            "labels",
            FileNameFor(area));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Expected labeled corpus at '{path}'.", path);
        }

        var dtos = JsonSerializer.Deserialize<ImmutableArray<LabelDto>>(File.ReadAllText(path), Options);
        return dtos.Select(dto => dto.ToEntry()).ToImmutableArray();
    }

    public static IEnumerable<CertifiedArea> AllAreas() =>
    [
        CertifiedArea.EntryPoint,
        CertifiedArea.LinkedCall,
        CertifiedArea.Contract,
        CertifiedArea.Persistence,
    ];

    private static string FileNameFor(CertifiedArea area) => area switch
    {
        CertifiedArea.EntryPoint => "entry-points.json",
        CertifiedArea.LinkedCall => "linked-calls.json",
        CertifiedArea.Contract => "contracts.json",
        CertifiedArea.Persistence => "persistence.json",
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, null),
    };

    private sealed record LabelDto(
        string Id,
        LabelKind Kind,
        ExpectedState Expected,
        string SourceFile,
        string? RelatedSourceFile,
        string Symbol,
        string Rationale)
    {
        public LabeledCorpusEntry ToEntry() =>
            new(Id, Kind, Expected, SourceFile, RelatedSourceFile, Symbol, Rationale);
    }
}
