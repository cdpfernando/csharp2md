using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;

namespace Csharp2Md.Core.Tests.Facts.Metadata;

public sealed class FactMetadataTests
{
    private static readonly ProjectFactId Project = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId Document = DocumentFactId.Create(Project, "Features/Handler.cs");

    [Fact]
    public void Evidence_ValidRange_RetainsDocumentRelativeLocation()
    {
        var evidence = new Evidence(Document, "Features/Handler.cs", 2, 3, 4, 5);

        Assert.Equal(Document, evidence.DocumentId);
        Assert.Equal("Features/Handler.cs", evidence.RelativePath);
        Assert.Equal((2, 3, 4, 5), (evidence.StartLine, evidence.StartColumn, evidence.EndLine, evidence.EndColumn));
    }

    [Theory]
    [InlineData("C:/repo/Handler.cs")]
    [InlineData("/repo/Handler.cs")]
    [InlineData("Features\\Handler.cs")]
    [InlineData("Features/../Handler.cs")]
    [InlineData("Features//Handler.cs")]
    public void Evidence_AbsoluteOrNonNormalizedPath_IsRejected(string path) =>
        Assert.Throws<ArgumentException>(() => new Evidence(Document, path, 1, 1, 1, 1));

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    public void Evidence_NonPositiveCoordinate_IsRejected(int startLine, int startColumn, int endLine, int endColumn) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Evidence(Document, "Features/Handler.cs", startLine, startColumn, endLine, endColumn));

    [Theory]
    [InlineData(2, 1, 1, 9)]
    [InlineData(2, 4, 2, 3)]
    public void Evidence_EndBeforeStart_IsRejected(int startLine, int startColumn, int endLine, int endColumn) =>
        Assert.Throws<ArgumentException>(() =>
            new Evidence(Document, "Features/Handler.cs", startLine, startColumn, endLine, endColumn));

    [Fact]
    public void Evidence_CanonicalOrdering_UsesDocumentPathAndRangeOrdinally()
    {
        var second = new Evidence(Document, "Features/Handler.cs", 2, 1, 2, 2);
        var first = new Evidence(Document, "Features/Handler.cs", 1, 1, 1, 2);

        Assert.Equal([first, second], new[] { second, first }.Order().ToArray());
    }

    [Fact]
    public void Provenance_EngineOnly_RetainsVersionWithoutDetector()
    {
        var provenance = new FactProvenance("csharp2md", "3.0.0");

        Assert.Equal("csharp2md", provenance.EngineId);
        Assert.Equal("3.0.0", provenance.EngineVersion);
        Assert.Null(provenance.DetectorId);
        Assert.Null(provenance.DetectorVersion);
    }

    [Theory]
    [InlineData("", "1.0.0")]
    [InlineData("engine", "")]
    public void Provenance_EmptyEngineIdentityOrVersion_IsRejected(string engineId, string engineVersion) =>
        Assert.Throws<ArgumentException>(() => new FactProvenance(engineId, engineVersion));

    [Fact]
    public void Provenance_DetectorIdentityWithoutVersion_IsRejected()
    {
        var detector = DetectorId.Create("io.csharp2md.http");

        Assert.Throws<ArgumentException>(() => new FactProvenance("csharp2md", "3.0.0", detector));
    }

    [Fact]
    public void Provenance_DetectorVersionWithoutIdentity_IsRejected() =>
        Assert.Throws<ArgumentException>(() => new FactProvenance("csharp2md", "3.0.0", detectorVersion: "1.0.0"));

    [Fact]
    public void Diagnostic_DataAndEvidenceInsertionOrder_ProducesSameIdAndCanonicalCollections()
    {
        var early = new Evidence(Document, "Features/Handler.cs", 1, 1, 1, 2);
        var late = new Evidence(Document, "Features/Handler.cs", 4, 1, 4, 2);
        var first = AnalysisDiagnostic.Create(
            "C2M1001",
            DiagnosticSeverity.Warning,
            DiagnosticStage.Document,
            Document.ToFactId(),
            "Binding degraded",
            [new("target", "net10.0"), new("reason", "error symbol")],
            [late, early]);
        var second = AnalysisDiagnostic.Create(
            "C2M1001",
            DiagnosticSeverity.Warning,
            DiagnosticStage.Document,
            Document.ToFactId(),
            "Binding degraded",
            [new("reason", "error symbol"), new("target", "net10.0")],
            [early, late]);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(["reason", "target"], first.Data.Select(static item => item.Key));
        Assert.Equal([early, late], first.Evidence.ToArray());
    }

    [Fact]
    public void Diagnostic_MessageOrDataChange_ChangesDeterministicIdentity()
    {
        var baseline = AnalysisDiagnostic.Create(
            "C2M1001", DiagnosticSeverity.Error, DiagnosticStage.Validation, Project.ToFactId(), "Duplicate ID", [new("id", "one")]);
        var changedMessage = AnalysisDiagnostic.Create(
            "C2M1001", DiagnosticSeverity.Error, DiagnosticStage.Validation, Project.ToFactId(), "Missing ID", [new("id", "one")]);
        var changedData = AnalysisDiagnostic.Create(
            "C2M1001", DiagnosticSeverity.Error, DiagnosticStage.Validation, Project.ToFactId(), "Duplicate ID", [new("id", "two")]);

        Assert.NotEqual(baseline.Id, changedMessage.Id);
        Assert.NotEqual(baseline.Id, changedData.Id);
        Assert.StartsWith("id1:diagnostic;stage=validation;scope=", baseline.Id.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_ScopeAndExtension_AreAddressableTypedValues()
    {
        var detector = DetectorId.Create("io.csharp2md.http");
        var diagnostic = AnalysisDiagnostic.Create(
            "C2M2001",
            DiagnosticSeverity.Warning,
            DiagnosticStage.Detector,
            Document.ToFactId(),
            "Detector degraded",
            extensionId: detector);

        Assert.Equal(Document.ToFactId(), diagnostic.ScopeId);
        Assert.Equal(detector, diagnostic.ExtensionId);
        Assert.Equal(DiagnosticStage.Detector, diagnostic.Stage);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }
}
