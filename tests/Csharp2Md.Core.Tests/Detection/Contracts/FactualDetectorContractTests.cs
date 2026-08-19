using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Detection.Contracts;

public sealed class FactualDetectorContractTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Feature.cs");

    [Fact]
    public void Descriptor_StableIdVersionLevelsAndKinds_AreRequiredAndCanonical()
    {
        var descriptor = DetectorDescriptor.Create(
            DetectorId.Create("io.csharp2md.http"),
            "1.2.0",
            [DetectorLevel.Document, DetectorLevel.Project, DetectorLevel.Document],
            [FactKind.Relation, FactKind.Symbol, FactKind.Relation]);

        Assert.Equal("id1:detector;name=io.csharp2md.http", descriptor.Id.Value);
        Assert.Equal("1.2.0", descriptor.Version);
        Assert.Equal([DetectorLevel.Project, DetectorLevel.Document], descriptor.SupportedLevels.ToArray());
        Assert.Equal([FactKind.Symbol, FactKind.Relation], descriptor.SupportedFactKinds.ToArray());
    }

    [Fact]
    public void Descriptor_EmptyVersion_IsRejected() =>
        Assert.Throws<ArgumentException>(() => DetectorDescriptor.Create(
            DetectorId.Create("io.csharp2md.http"),
            " ",
            [DetectorLevel.Document],
            [FactKind.Relation]));

    [Fact]
    public void Descriptor_NoSupportedLevel_IsRejected() =>
        Assert.Throws<ArgumentException>(() => DetectorDescriptor.Create(
            DetectorId.Create("io.csharp2md.http"),
            "1.0.0",
            [],
            [FactKind.Relation]));

    [Fact]
    public void Descriptor_NoSupportedFactKind_IsRejected() =>
        Assert.Throws<ArgumentException>(() => DetectorDescriptor.Create(
            DetectorId.Create("io.csharp2md.http"),
            "1.0.0",
            [DetectorLevel.Document],
            []));

    [Fact]
    public void ProjectAndDocumentDetectors_AreIndependentGranularityContracts()
    {
        Assert.False(typeof(IDocumentFactDetector).IsAssignableFrom(typeof(IProjectFactDetector)));
        Assert.False(typeof(IProjectFactDetector).IsAssignableFrom(typeof(IDocumentFactDetector)));
        Assert.Equal(typeof(DocumentDetectionContext), typeof(IDocumentFactDetector).GetMethod(nameof(IDocumentFactDetector.Detect))!.GetParameters().Single().ParameterType);
        Assert.Equal(typeof(ProjectDetectionContext), typeof(IProjectFactDetector).GetMethod(nameof(IProjectFactDetector.Detect))!.GetParameters().Single().ParameterType);
    }

    [Fact]
    public void DetectionContexts_ExposeOnlyFactualInputsAtTheirDeclaredGranularity()
    {
        var contextTypes = new[] { typeof(ProjectDetectionContext), typeof(DocumentDetectionContext) };
        var forbidden = new[] { "Microsoft.CodeAnalysis", "System.IO", "Csharp2Md.Core.Rendering", "Csharp2Md.Core.Facts.Storage" };

        Assert.All(
            contextTypes.SelectMany(static type => type.GetProperties()),
            property => Assert.DoesNotContain(
                forbidden,
                prefix => property.PropertyType.FullName?.StartsWith(prefix, StringComparison.Ordinal) is true));
    }

    [Fact]
    public void DetectorResult_FactsAndDiagnostics_AreCanonicallyOrdered()
    {
        var earlySymbolId = SymbolFactId.CreateSyntactic(ProjectId, "Feature.cs", "class", "A");
        var lateSymbolId = SymbolFactId.CreateSyntactic(ProjectId, "Feature.cs", "class", "B");
        var earlyFact = Symbol(earlySymbolId);
        var lateFact = Symbol(lateSymbolId);
        var earlyDiagnostic = AnalysisDiagnostic.Create(
            "A", DiagnosticSeverity.Warning, DiagnosticStage.Detector, DocumentId.ToFactId(), "a");
        var lateDiagnostic = AnalysisDiagnostic.Create(
            "B", DiagnosticSeverity.Warning, DiagnosticStage.Detector, DocumentId.ToFactId(), "b");

        var result = DetectorResult.Create([lateFact, earlyFact], [lateDiagnostic, earlyDiagnostic]);

        Assert.Equal([earlyFact, lateFact], result.Facts.ToArray());
        Assert.Equal(
            new[] { lateDiagnostic, earlyDiagnostic }.Order().ToArray(),
            result.Diagnostics.ToArray());
    }

    [Fact]
    public void DetectorResult_Surface_HasNoPresentationPersistenceOrCallbackMembers()
    {
        var propertyTypes = typeof(DetectorResult).GetProperties().Select(static property => property.PropertyType).ToArray();

        Assert.DoesNotContain(propertyTypes, static type => typeof(Delegate).IsAssignableFrom(type));
        Assert.DoesNotContain(propertyTypes, static type => type == typeof(Stream));
        Assert.All(propertyTypes, static type => Assert.DoesNotContain("Rendering", type.FullName, StringComparison.Ordinal));
        Assert.All(propertyTypes, static type => Assert.DoesNotContain("Storage", type.FullName, StringComparison.Ordinal));
    }

    [Fact]
    public void DetectorVersion_IsProvenanceAndDoesNotChangeStableDetectorId()
    {
        var first = DetectorDescriptor.Create(
            DetectorId.Create("io.csharp2md.http"), "1.0.0", [DetectorLevel.Document], [FactKind.Relation]);
        var second = DetectorDescriptor.Create(
            DetectorId.Create("io.csharp2md.http"), "2.0.0", [DetectorLevel.Document], [FactKind.Relation]);

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Version, second.Version);
    }

    private static SymbolFact Symbol(SymbolFactId id) => new(
        FactHeader.Create(id.ToFactId(), FactKind.Symbol, FactResolution.Syntactic),
        id,
        DocumentId,
        "class",
        false,
        [],
        [],
        [],
        Semantics: null,
        Name: "C",
        FullyQualifiedName: "global::C",
        Namespace: null,
        ContainingType: null,
        ContainingSymbolId: null,
        Signature: "class C",
        Arity: 0,
        ParameterTypes: []);
}
