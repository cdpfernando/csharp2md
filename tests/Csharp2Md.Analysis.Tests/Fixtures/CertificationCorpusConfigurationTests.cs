using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-103, GCPC-105: reproduces the audit's solution-folder and configuration-parsing findings
/// (finding I4, design.md F2 and F3) in the versioned certification corpus. The fixes — stop
/// diagnosing solution folders as missing projects, and align configuration parsing with the .NET
/// configuration provider — land in later phases (T13, T14); this task only proves the current,
/// pre-fix diagnostic set is present and reproducible in CI.
/// </summary>
public sealed class CertificationCorpusConfigurationTests
{
    private static readonly string SolutionPath = Path.Combine(
        CertificationCorpusPaths.RootPath,
        "ConfigurationShapes",
        "ConfigurationShapes.sln");

    [Fact]
    [Trait("Requirement", "GCPC-103")]
    public void ReadProjectPaths_ConfigurationShapesSln_ListsTheSolutionFolderAndTheMissingProjectSeparately()
    {
        Assert.True(File.Exists(SolutionPath), $"Expected fixture at '{SolutionPath}'.");

        var paths = SolutionFileReader.ReadProjectPaths(SolutionPath).ToArray();

        Assert.Contains("Docs", paths);
        Assert.Contains(
            paths,
            path => path.Contains("ConfigurationShapes.Ghost", StringComparison.Ordinal));
        Assert.Contains(
            paths,
            path => path.Contains("ConfigurationShapes.App", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-103")]
    public async Task AnalyzeAsync_ConfigurationShapes_CurrentlyDiagnosesTheSolutionFolderAsAMissingProjectSeparatelyFromTheGenuinelyMissingOne()
    {
        var diagnostics = await AnalyzeAndReadDiagnosticsAsync();

        var missingProjectDiagnostics = diagnostics.Records
            .Where(record => record.Code == "missing-project")
            .ToArray();

        // Documented pre-fix baseline (F3, GCPC-103): SolutionFileReader does not filter by project
        // type GUID, so the solution-folder line ("Docs") reaches the same missing-project branch as
        // the genuinely missing project. T13 (a later phase) is what stops the folder being diagnosed.
        Assert.Contains(
            missingProjectDiagnostics,
            record => record.IdentityOrKey == "Docs");
        Assert.Contains(
            missingProjectDiagnostics,
            record => record.IdentityOrKey is not null
                && record.IdentityOrKey.Contains("ConfigurationShapes.Ghost", StringComparison.Ordinal));
        Assert.Equal(2, missingProjectDiagnostics.Length);
    }

    [Theory]
    [Trait("Requirement", "GCPC-105")]
    [InlineData("appsettings.Comments.json")]
    [InlineData("appsettings.TrailingComma.json")]
    [InlineData("appsettings.Bom.json")]
    public async Task AnalyzeAsync_ConfigurationShapes_CurrentlyDiagnosesProviderToleratedSyntaxAsMalformed(
        string relativeAppsettingsName)
    {
        var diagnostics = await AnalyzeAndReadDiagnosticsAsync();

        // Documented pre-fix baseline (F2, GCPC-105): JsonDocument.Parse's strict default options
        // reject comments, a trailing comma and a UTF-8 BOM that the .NET configuration provider
        // accepts, so each is currently a false "malformed-configuration-document" diagnostic. T14 (a
        // later phase) is what aligns parsing with the provider.
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == "malformed-configuration-document"
                && record.IdentityOrKey is not null
                && record.IdentityOrKey.Contains(relativeAppsettingsName, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-105")]
    public async Task AnalyzeAsync_ConfigurationShapes_CurrentlyDiagnosesTheUnterminatedDocumentAsMalformed()
    {
        var diagnostics = await AnalyzeAndReadDiagnosticsAsync();

        // Both today and after T14: the .NET configuration provider also rejects an unterminated
        // object, so this stays diagnosed as malformed.
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == "malformed-configuration-document"
                && record.IdentityOrKey is not null
                && record.IdentityOrKey.Contains("appsettings.Unterminated.json", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-105")]
    public async Task AnalyzeAsync_ConfigurationShapes_CurrentlyPromotesTheDuplicateKeyDocumentWithNoDiagnostic()
    {
        var diagnostics = await AnalyzeAndReadDiagnosticsAsync();

        // Documented pre-fix baseline (F2, GCPC-105/GCPC-107): JsonDocument.Parse accepts a duplicate
        // top-level key (last one wins), unlike the .NET configuration provider, which rejects it. So
        // today this ambiguous document is silently promoted with no diagnostic at all — the opposite
        // divergence from the comments/trailing-comma/BOM cases above. T14 closes this by rejecting it.
        Assert.DoesNotContain(
            diagnostics.Records,
            record => record.Code == "malformed-configuration-document"
                && record.IdentityOrKey is not null
                && record.IdentityOrKey.Contains("appsettings.DuplicateKey.json", StringComparison.Ordinal));
    }

    private static async Task<DiagnosticsEnvelope> AnalyzeAndReadDiagnosticsAsync()
    {
        Assert.True(File.Exists(SolutionPath), $"Expected fixture at '{SolutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([SolutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(SolutionPath), out var publication));

        var diagnosticsFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "diagnostics.json");
        return CanonicalJson.Read<DiagnosticsEnvelope>(diagnosticsFragment.Payload.AsSpan());
    }
}
