using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-103, GCPC-104, GCPC-105..107: reproduces the audit's solution-folder and
/// configuration-parsing findings (finding I4, design.md F2 and F3) in the versioned certification
/// corpus, and proves the T13/T14 fixes: <c>SolutionFileReader</c> filters solution-folder entries by
/// project type GUID, so "Docs" never reaches the missing-project branch, while the genuinely
/// missing "ConfigurationShapes.Ghost" project is still diagnosed; and
/// <c>ConfigurationDocumentReader</c> tolerates the syntax the .NET configuration provider accepts
/// (comments, a trailing comma, a UTF-8 BOM) while still rejecting an unterminated document and a
/// duplicate key.
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
    public async Task AnalyzeAsync_ConfigurationShapes_ProviderToleratedSyntaxIsNotDiagnosedAsMalformed(
        string relativeAppsettingsName)
    {
        var diagnostics = await AnalyzeAndReadDiagnosticsAsync();

        // T14 (GCPC-105): comments, a trailing comma and a UTF-8 BOM are accepted by the .NET
        // configuration provider, so ConfigurationDocumentReader's tolerant JsonDocumentOptions and
        // BOM stripping now accept them too — no false "malformed-configuration-document" diagnostic.
        Assert.DoesNotContain(
            diagnostics.Records,
            record => record.Code == "malformed-configuration-document"
                && record.IdentityOrKey is not null
                && record.IdentityOrKey.Contains(relativeAppsettingsName, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-106")]
    public async Task AnalyzeAsync_ConfigurationShapes_StillDiagnosesTheUnterminatedDocumentAsMalformed()
    {
        var diagnostics = await AnalyzeAndReadDiagnosticsAsync();

        // Both before and after T14: the .NET configuration provider also rejects an unterminated
        // object, so this stays diagnosed as malformed (GCPC-106).
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == "malformed-configuration-document"
                && record.IdentityOrKey is not null
                && record.IdentityOrKey.Contains("appsettings.Unterminated.json", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-107")]
    public async Task AnalyzeAsync_ConfigurationShapes_NowDiagnosesTheDuplicateKeyDocumentAndPromotesNoBinding()
    {
        var diagnostics = await AnalyzeAndReadDiagnosticsAsync();

        // T14 (GCPC-107): the .NET configuration provider rejects a duplicate key, so
        // ConfigurationDocumentReader now detects the collision (case-insensitively) and diagnoses it
        // as malformed instead of silently promoting an ambiguous binding.
        Assert.Contains(
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
