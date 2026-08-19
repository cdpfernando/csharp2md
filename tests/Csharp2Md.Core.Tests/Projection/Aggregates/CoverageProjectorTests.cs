using System.Text.Json;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Projection.Aggregates;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

[Trait("Category", "Integration")]
public sealed class CoverageProjectorTests : IDisposable
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("Probe.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Probe.cs");
    private static readonly DetectorId DetectorId = DetectorId.Create("io.csharp2md.probe");
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-coverage-").FullName;
    private readonly string _input = Directory.CreateTempSubdirectory("csharp2md-coverage-input-").FullName;

    public static TheoryData<DiagnosticStage> DiagnosticStages => new()
    {
        DiagnosticStage.Inventory,
        DiagnosticStage.Evaluation,
        DiagnosticStage.Workspace,
        DiagnosticStage.Compilation,
        DiagnosticStage.Document,
        DiagnosticStage.Validation,
        DiagnosticStage.Generator,
        DiagnosticStage.Detector,
        DiagnosticStage.Persistence,
        DiagnosticStage.Projection,
    };

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        Directory.Delete(_input, recursive: true);
    }

    [Fact]
    public void SyntaxOnlyScope_IsApplicableButExplicitlyNotAttemptedSemantically()
    {
        var result = Project(AnalysisMode.SyntaxOnly, [Project(FactResolution.Syntactic)]);

        var coverage = Assert.Single(result.Coverage);
        Assert.Equal(CoverageApplicability.Applicable, coverage.Applicability);
        Assert.Equal(CoverageAttempt.NotAttempted, coverage.Attempt);
        Assert.Equal(FactResolution.Syntactic, coverage.Resolution);
        Assert.Equal(1, result.Summary.UnattemptedScopes);
    }

    [Fact]
    public void HealthySemanticScope_IsAttemptedAndExact()
    {
        var result = Project(AnalysisMode.Semantic, [Project(FactResolution.Exact)]);

        var coverage = Assert.Single(result.Coverage);
        Assert.Equal(CoverageAttempt.Attempted, coverage.Attempt);
        Assert.Equal(FactResolution.Exact, coverage.Resolution);
        Assert.Equal(1, result.Summary.ExactScopes);
        Assert.Equal(0, result.Summary.DegradedScopes);
    }

    [Fact]
    public void SemanticFallback_IsAttemptedSyntacticAndReferencesItsDiagnostic()
    {
        var diagnostic = Diagnostic(DiagnosticStage.Evaluation, ProjectId.ToFactId());

        var result = Project(
            AnalysisMode.Semantic,
            [Project(FactResolution.Syntactic)],
            [diagnostic]);

        var coverage = Assert.Single(result.Coverage);
        Assert.Equal(CoverageAttempt.Attempted, coverage.Attempt);
        Assert.Equal(FactResolution.Syntactic, coverage.Resolution);
        Assert.Equal([diagnostic.Id], coverage.DiagnosticIds.ToArray());
        Assert.Equal(1, result.Summary.DegradedScopes);
    }

    [Theory]
    [InlineData(FactResolution.Exact)]
    [InlineData(FactResolution.Partial)]
    [InlineData(FactResolution.Syntactic)]
    [InlineData(FactResolution.Unresolved)]
    [InlineData(FactResolution.NotApplicable)]
    public void ResolutionQualities_RemainDistinctInCoverage(FactResolution resolution)
    {
        var result = Project(AnalysisMode.Semantic, [Project(resolution)]);

        Assert.Equal(resolution, Assert.Single(result.Coverage).Resolution);
        Assert.Equal(
            resolution is FactResolution.NotApplicable ? CoverageApplicability.NotApplicable : CoverageApplicability.Applicable,
            result.Coverage[0].Applicability);
    }

    [Fact]
    public void DetectorNotApplicable_RemainsDistinctFromAnEmptyAttempt()
    {
        var detector = new DetectorCoverageInput(
            DocumentId.ToFactId(),
            FactLevel.Document,
            DetectorId,
            CoverageApplicability.NotApplicable,
            CoverageAttempt.NotAttempted,
            FactResolution.NotApplicable,
            []);

        var result = Project(AnalysisMode.SyntaxOnly, [], detectorScopes: [detector]);

        var coverage = Assert.Single(result.Coverage);
        Assert.Equal(DetectorId, coverage.DetectorId);
        Assert.Equal(CoverageApplicability.NotApplicable, coverage.Applicability);
        Assert.Equal(CoverageAttempt.NotAttempted, coverage.Attempt);
        Assert.Equal(FactResolution.NotApplicable, coverage.Resolution);
    }

    [Fact]
    public void DetectorFailure_IsAttemptedDegradedAndReferencesItsScopedDiagnostic()
    {
        var diagnostic = Diagnostic(DiagnosticStage.Detector, DocumentId.ToFactId());
        var detector = new DetectorCoverageInput(
            DocumentId.ToFactId(),
            FactLevel.Document,
            DetectorId,
            CoverageApplicability.Applicable,
            CoverageAttempt.Attempted,
            FactResolution.Unresolved,
            []);

        var result = Project(AnalysisMode.Semantic, [], [diagnostic], [detector]);

        var coverage = Assert.Single(result.Coverage);
        Assert.Equal(CoverageAttempt.Attempted, coverage.Attempt);
        Assert.Equal(FactResolution.Unresolved, coverage.Resolution);
        Assert.Equal([diagnostic.Id], coverage.DiagnosticIds.ToArray());
    }

    [Fact]
    public void EveryInventoriedHierarchyScope_GetsItsOwnCoverageEntry()
    {
        var solutionId = FactIdGrammar.Create("solution", ("name", "Probe"));
        IFact[] facts =
        [
            new SolutionFact(Header(solutionId, FactKind.Solution, FactResolution.NotApplicable), "Probe", [ProjectId]),
            Project(FactResolution.Partial),
            new TargetFact(Header(TargetId.ToFactId(), FactKind.Target, FactResolution.Exact), TargetId, ProjectId, "net10.0"),
            new DocumentFact(Header(DocumentId.ToFactId(), FactKind.Document, FactResolution.Syntactic), DocumentId, ProjectId, "Probe.cs", [], []),
        ];

        var result = Project(AnalysisMode.Semantic, facts);

        Assert.Equal([FactLevel.Solution, FactLevel.Project, FactLevel.Target, FactLevel.Document], result.Coverage.Select(static entry => entry.FactLevel).Order().ToArray());
        Assert.Equal(4, result.Summary.TotalScopes);
    }

    [Theory]
    [MemberData(nameof(DiagnosticStages))]
    public void EveryDiagnosticStage_IsProjectedDeterministically(DiagnosticStage stage)
    {
        var diagnostic = Diagnostic(stage, ProjectId.ToFactId());

        var result = Project(AnalysisMode.Semantic, [Project(FactResolution.Syntactic)], [diagnostic, diagnostic]);

        var projected = Assert.Single(result.Diagnostics);
        Assert.Equal(stage.ToString().ToLowerInvariant(), projected.Stage);
        Assert.Equal(diagnostic.Id.Value, projected.Id);
        Assert.Equal(1, result.Summary.DiagnosticCount);
    }

    [Fact]
    public void UnsafeOperationalDetail_IsRedactedFromMachineDiagnostics()
    {
        var diagnostic = AnalysisDiagnostic.Create(
            "C2M-TEST-REDact",
            DiagnosticSeverity.Warning,
            DiagnosticStage.Compilation,
            TargetId.ToFactId(),
            "Failure at Probe.Run() in C:\\repo\\Probe.cs:line 4",
            [new DiagnosticData("detail", "/home/user/repo/Probe.cs")]);

        var result = Project(AnalysisMode.Semantic, [], [diagnostic]);

        var projected = Assert.Single(result.Diagnostics);
        Assert.Equal("Diagnostic detail was redacted from machine output.", projected.Message);
        Assert.Empty(projected.Data);
        Assert.DoesNotContain("C:\\repo", projected.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("/home/user", projected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageAndDiagnosticOrdering_IsCanonicalAndDeduplicated()
    {
        var projectDiagnostic = Diagnostic(DiagnosticStage.Evaluation, ProjectId.ToFactId());
        var documentDiagnostic = Diagnostic(DiagnosticStage.Document, DocumentId.ToFactId());
        IFact[] facts =
        [
            new DocumentFact(Header(DocumentId.ToFactId(), FactKind.Document, FactResolution.Syntactic), DocumentId, ProjectId, "Probe.cs", [], []),
            Project(FactResolution.Syntactic),
        ];

        var result = Project(
            AnalysisMode.Semantic,
            facts,
            [documentDiagnostic, projectDiagnostic, projectDiagnostic]);

        Assert.Equal(
            result.Coverage.OrderBy(static entry => entry.ScopeId.Value, StringComparer.Ordinal),
            result.Coverage);
        Assert.Equal(
            result.Diagnostics.OrderBy(static entry => entry.Id, StringComparer.Ordinal),
            result.Diagnostics);
    }

    [Fact]
    public void ConflictingCoverageForOneDetectorScope_IsRejected()
    {
        var exact = Detector(FactResolution.Exact);
        var unresolved = Detector(FactResolution.Unresolved);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Project(AnalysisMode.Semantic, [], detectorScopes: [exact, unresolved]));

        Assert.Contains("Conflicting coverage entries", exception.Message, StringComparison.Ordinal);
        Assert.Contains(DetectorId.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Writer_EmitsMachineFactsWhileAuditLogOnlySummarizesThem()
    {
        var diagnostic = Diagnostic(DiagnosticStage.Evaluation, ProjectId.ToFactId());
        var projection = Project(AnalysisMode.Semantic, [Project(FactResolution.Syntactic)], [diagnostic]);

        Write(projection);

        var diagnostics = File.ReadAllText(Path.Combine(_root, "raw", "facts", "diagnostics.json"));
        var coverage = File.ReadAllText(Path.Combine(_root, "raw", "facts", "coverage.json"));
        var log = File.ReadAllText(Path.Combine(_root, "raw", "log.md"));
        Assert.Contains(diagnostic.Code, diagnostics, StringComparison.Ordinal);
        Assert.Contains(ProjectId.Value, coverage, StringComparison.Ordinal);
        Assert.Contains("- diagnostics: 1", log, StringComparison.Ordinal);
        Assert.Contains("- degraded_scopes: 1", log, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostic.Code, log, StringComparison.Ordinal);
        Assert.DoesNotContain(ProjectId.Value, log, StringComparison.Ordinal);
    }

    [Fact]
    public Task Writer_CoverageMatchesApprovedSpecDerivedSnapshot()
    {
        var projection = Project(AnalysisMode.SyntaxOnly, [Project(FactResolution.Syntactic)]);
        Write(projection);
        var coveragePath = Path.Combine(_root, "raw", "facts", "coverage.json");
        using var document = JsonDocument.Parse(File.ReadAllText(coveragePath));
        var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal("project", entry.GetProperty("fact_level").GetString());
        Assert.Equal("not-attempted", entry.GetProperty("attempt").GetString());
        Assert.Equal("syntactic", entry.GetProperty("resolution").GetString());
        return Verifier.Verify(File.ReadAllText(coveragePath), "json").UseDirectory("snapshots");
    }

    private void Write(CoverageProjectionResult projection) =>
        new CanonicalAggregateWriter().Write(
            _root,
            _input,
            force: false,
            new AggregateOutputSnapshot(
                "architecture",
                "system-design",
                "3.0.0",
                AnalysisMode.Semantic,
                AnalysisMode.Semantic,
                TrustMode.TrustedSolution,
                [],
                new ManifestCoverage(1, 1, 1),
                [],
                projection),
            TimeProvider.System);

    private static CoverageProjectionResult Project(
        AnalysisMode mode,
        IEnumerable<IFact> facts,
        IEnumerable<AnalysisDiagnostic>? diagnostics = null,
        IEnumerable<DetectorCoverageInput>? detectorScopes = null) =>
        CoverageProjector.Project(new CoverageProjectionRequest(
            mode,
            facts.ToImmutableArray(),
            (diagnostics ?? []).ToImmutableArray(),
            (detectorScopes ?? []).ToImmutableArray()));

    private static ProjectFact Project(FactResolution resolution) => new(
        Header(ProjectId.ToFactId(), FactKind.Project, resolution),
        ProjectId,
        "Probe",
        "Probe.csproj",
        [],
        []);

    private static DetectorCoverageInput Detector(FactResolution resolution) => new(
        DocumentId.ToFactId(),
        FactLevel.Document,
        DetectorId,
        CoverageApplicability.Applicable,
        CoverageAttempt.Attempted,
        resolution,
        []);

    private static FactHeader Header(FactId id, FactKind kind, FactResolution resolution) =>
        FactHeader.Create(id, kind, resolution, [new FactProvenance("test", "1")]);

    private static AnalysisDiagnostic Diagnostic(DiagnosticStage stage, FactId scopeId) =>
        AnalysisDiagnostic.Create(
            $"C2M-{stage.ToString().ToUpperInvariant()}-TEST",
            DiagnosticSeverity.Warning,
            stage,
            scopeId,
            $"Controlled {stage.ToString().ToLowerInvariant()} degradation.");
}
