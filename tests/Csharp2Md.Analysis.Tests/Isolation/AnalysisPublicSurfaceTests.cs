using System.Runtime.CompilerServices;
using Csharp2Md.Analysis;

namespace Csharp2Md.Analysis.Tests.Isolation;

public sealed class AnalysisPublicSurfaceTests
{
    private static readonly HashSet<string> AllowedPublicTypeNames =
    [
        "IAnalysisEngine",
        "AnalysisEngine",
        "AnalysisRequest",
        "AnalysisResult",
        "SolutionOutcome",
        "StageReport",
        "PublicationStatus",
        "ITransactionalStore",
        "IStoreSession",
        "StagedFragment",
        "ArtifactRole",
        "CommittedPublication",
        "FactualSnapshot",
        "PublicationRejectedException",
        "DiagnosticRecord",
    ];

    private static readonly string[] ForbiddenSurfaceTokens =
    [
        "Pass",
        "Classifier",
        "Adapter",
        "Extractor",
        "Stage",
    ];

    [Fact]
    [Trait("Requirement", "ENG-11")]
    [Trait("Requirement", "STOR-35")]
    public void PublicSurface_ContainsOnlyAllowlistedTypes()
    {
        var extras = PublicSurfaceTypes()
            .Where(type => !IsAllowlisted(type))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(
            extras.Length == 0,
            $"Public Analysis type(s) outside the allowlist: {FormatNames(extras)}.");
    }

    [Fact]
    [Trait("Requirement", "STOR-35")]
    public void PublicSurface_IncludesFactualSnapshotAndPublicationRejectedException()
    {
        var names = PublicSurfaceTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("FactualSnapshot", names);
        Assert.Contains("PublicationRejectedException", names);
    }

    [Fact]
    [Trait("Requirement", "STOR-35")]
    public void PublicSurface_DoesNotIncludeFactualPackageReaderOrPackageReadResult()
    {
        var names = PublicSurfaceTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("FactualPackageReader", names);
        Assert.DoesNotContain("PackageReadResult", names);
    }

    [Fact]
    [Trait("Requirement", "ENG-12")]
    [Trait("Requirement", "STOR-35")]
    public void PublicSurface_DoesNotExposePassClassifierAdapterOrStageTypes()
    {
        var offending = PublicSurfaceTypes()
            .Where(type => !AllowedPublicTypeNames.Contains(type.Name))
            .Where(type => LooksLikePassClassifierAdapterOrStage(type.Name))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(
            offending.Length == 0,
            $"Public Analysis type(s) that look like a pass, classifier, adapter, extractor or stage: {FormatNames(offending)}.");
    }

    [Fact]
    [Trait("Requirement", "ROSE-31")]
    public void PublicSurface_DoesNotExportInventoryStageSemanticAnalysisStageOrObservationExtractor()
    {
        var names = PublicSurfaceTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("InventoryStage", names);
        Assert.DoesNotContain("SemanticAnalysisStage", names);
        Assert.DoesNotContain("ObservationExtractor", names);
        Assert.DoesNotContain("BoundSolution", names);
        Assert.Contains("DiagnosticRecord", names);
    }

    [Fact]
    [Trait("Requirement", "CLLF-01")]
    public void PublicSurface_DoesNotExportInvokesPassOrExecutesPass()
    {
        var names = PublicSurfaceTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("InvokesPass", names);
        Assert.DoesNotContain("ExecutesPass", names);
        Assert.DoesNotContain("ClassifierContext", names);
        Assert.DoesNotContain("IClassifierPass", names);
    }

    private static IEnumerable<Type> PublicSurfaceTypes() =>
        typeof(AssemblyMarker).Assembly
            .GetExportedTypes()
            .Where(type => type.IsPublic || type.IsNestedPublic)
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Where(type => !type.Name.Contains('<', StringComparison.Ordinal));

    private static bool IsAllowlisted(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (AllowedPublicTypeNames.Contains(current.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikePassClassifierAdapterOrStage(string typeName) =>
        ForbiddenSurfaceTokens.Any(token => typeName.Contains(token, StringComparison.Ordinal));

    private static string FormatNames(IReadOnlyList<string?> names) =>
        names.Count == 0 ? "<none>" : string.Join(", ", names);
}
