using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-103, GCPC-104, GCPC-105: reproduces the audit's solution-folder and configuration-parsing
/// findings (finding I4, design.md F2 and F3) in the versioned certification corpus. GCPC-103/104
/// (solution-folder filtering) are fixed by T13: <c>SolutionFileReader</c> filters solution-folder
/// entries by project type GUID, so "Docs" never reaches the missing-project branch, while the
/// genuinely missing "ConfigurationShapes.Ghost" project is still diagnosed. GCPC-105..107
/// (configuration-parsing policy) land in T14; those cases still document the pre-fix baseline.
/// </summary>
public sealed class CertificationCorpusConfigurationTests
{
    private static readonly string SolutionPath = Path.Combine(
        CertificationCorpusPaths.RootPath,
        "ConfigurationShapes",
        "ConfigurationShapes.sln");

    [Fact]
    [Trait("Requirement", "GCPC-103")]
    public void ReadProjectPaths_ConfigurationShapesSln_ExcludesTheSolutionFolderButListsBothProjects()
    {
        Assert.True(File.Exists(SolutionPath), $"Expected fixture at '{SolutionPath}'.");

        var paths = SolutionFileReader.ReadProjectPaths(SolutionPath).ToArray();

        Assert.DoesNotContain("Docs", paths);
        Assert.Contains(
            paths,
            path => path.Contains("ConfigurationShapes.Ghost", StringComparison.Ordinal));
        Assert.Contains(
            paths,
            path => path.Contains("ConfigurationShapes.App", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-103")]
    [Trait("Requirement", "GCPC-104")]
    public async Task AnalyzeAsync_ConfigurationShapes_DoesNotDiagnoseTheSolutionFolderButStillDiagnosesTheGenuinelyMissingProject()
    {
        var diagnostics = await AnalyzeAndReadDiagnosticsAsync();

        var missingProjectDiagnostics = diagnostics.Records
            .Where(record => record.Code == "missing-project")
            .ToArray();

        // T13 (GCPC-103): the solution-folder line ("Docs") is filtered by project type GUID before
        // it ever reaches the missing-project branch, so no diagnostic names it.
        Assert.DoesNotContain(
            missingProjectDiagnostics,
            record => record.IdentityOrKey == "Docs");
        // GCPC-104: the genuinely missing project is still diagnosed, naming the missing path; the
        // diagnostic is published inside ConfigurationShapes.sln's own publication (SolutionPath,
        // read by AnalyzeAndReadDiagnosticsAsync below), which is how it names the referencing
        // solution.
        var missingGhost = Assert.Single(missingProjectDiagnostics);
        Assert.Contains("ConfigurationShapes.Ghost", missingGhost.IdentityOrKey, StringComparison.Ordinal);
        Assert.Contains("ConfigurationShapes.Ghost", missingGhost.Message, StringComparison.Ordinal);
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
