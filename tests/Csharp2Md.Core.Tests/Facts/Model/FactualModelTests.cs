using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Facts.Model;

public sealed class FactualModelTests
{
    private static readonly ProjectFactId Project = ProjectFactId.Create("src/App/App.csproj");
    private static readonly TargetFactId Target = TargetFactId.Create(Project, "net10.0");
    private static readonly DocumentFactId Document = DocumentFactId.Create(Project, "Feature.cs");

    public static TheoryData<FactResolution[], FactResolution> DocumentResolutionCases => new()
    {
        { [], FactResolution.NotApplicable },
        { [FactResolution.NotApplicable], FactResolution.NotApplicable },
        { [FactResolution.Exact], FactResolution.Exact },
        { [FactResolution.Exact, FactResolution.NotApplicable, FactResolution.Exact], FactResolution.Exact },
        { [FactResolution.Syntactic], FactResolution.Syntactic },
        { [FactResolution.Syntactic, FactResolution.NotApplicable, FactResolution.Syntactic], FactResolution.Syntactic },
        { [FactResolution.Unresolved], FactResolution.Unresolved },
        { [FactResolution.Unresolved, FactResolution.NotApplicable, FactResolution.Unresolved], FactResolution.Unresolved },
        { [FactResolution.Partial], FactResolution.Partial },
        { [FactResolution.Exact, FactResolution.Syntactic], FactResolution.Partial },
        { [FactResolution.Exact, FactResolution.Unresolved], FactResolution.Partial },
        { [FactResolution.Syntactic, FactResolution.Unresolved], FactResolution.Partial },
        { [FactResolution.Partial, FactResolution.Exact], FactResolution.Partial },
    };

    [Theory]
    [MemberData(nameof(DocumentResolutionCases))]
    public void AggregateDocument_ResolutionQualities_FollowNormativeTable(
        FactResolution[] resolutions,
        FactResolution expected) =>
        Assert.Equal(expected, FactResolutionAlgebra.AggregateDocument(resolutions));

    [Fact]
    public void AggregateDocument_UnknownResolution_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FactResolutionAlgebra.AggregateDocument([(FactResolution)99]));

    [Fact]
    public void FactHeader_Collections_AreImmutableDeduplicatedAndCanonical()
    {
        var detector = DetectorId.Create("io.csharp2md.syntax");
        var lateProvenance = new FactProvenance("engine", "2", detector, "1");
        var earlyProvenance = new FactProvenance("engine", "1");
        var lateEvidence = new Evidence(Document, "Feature.cs", 2, 1, 2, 2);
        var earlyEvidence = new Evidence(Document, "Feature.cs", 1, 1, 1, 2);
        var lateDiagnostic = DiagnosticId.Create("document", Document.ToFactId(), "B", "b");
        var earlyDiagnostic = DiagnosticId.Create("document", Document.ToFactId(), "A", "a");

        var header = FactHeader.Create(
            Document.ToFactId(),
            FactKind.Document,
            FactResolution.Syntactic,
            [lateProvenance, earlyProvenance, earlyProvenance],
            [lateEvidence, earlyEvidence, earlyEvidence],
            [lateDiagnostic, earlyDiagnostic, earlyDiagnostic]);

        Assert.Equal([earlyProvenance, lateProvenance], header.Provenance.ToArray());
        Assert.Equal([earlyEvidence, lateEvidence], header.Evidence.ToArray());
        Assert.Equal([earlyDiagnostic, lateDiagnostic], header.DiagnosticIds.ToArray());
    }

    [Fact]
    public void EverySpecializedFact_ContainsOnlyFactualDomainTypes()
    {
        var factualTypes = new[]
        {
            typeof(SolutionFact), typeof(ProjectFact), typeof(TargetFact), typeof(DocumentFact),
            typeof(SourceSectionFact), typeof(SymbolFact), typeof(ComponentFact), typeof(RelationFact), typeof(CoverageFact),
        };

        var forbiddenNamespacePrefixes = new[]
        {
            "Microsoft.CodeAnalysis", "System.IO", "YamlDotNet", "Csharp2Md.Core.Rendering", "Csharp2Md.Cli",
        };

        Assert.All(
            factualTypes.SelectMany(static type => type.GetProperties()),
            property => Assert.DoesNotContain(
                forbiddenNamespacePrefixes,
                prefix => property.PropertyType.FullName?.StartsWith(prefix, StringComparison.Ordinal) is true));
    }

    [Fact]
    public void SymbolFact_ErrorSymbolState_IsRepresentedWithoutForcingExactness()
    {
        var symbolId = SymbolFactId.CreateSyntactic(Project, "Feature.cs", "class", "Feature");
        var fact = new SymbolFact(
            Header(symbolId.ToFactId(), FactKind.Symbol, FactResolution.Unresolved),
            symbolId,
            Document,
            "class",
            ContainsErrorSymbol: true,
            [],
            [],
            []);

        Assert.True(fact.ContainsErrorSymbol);
        Assert.Equal(FactResolution.Unresolved, fact.Header.Resolution);
    }

    [Fact]
    public void RuntimeRelation_UnprovedTarget_PreservesNullTargetReasonAndUnresolvedResolution()
    {
        var relationId = RelationFactId.Create(Document.ToFactId(), "http", "GET /payments", 1);
        var fact = new RelationFact(
            Header(relationId.ToFactId(), FactKind.Relation, FactResolution.Unresolved),
            relationId,
            Document.ToFactId(),
            TargetId: null,
            RelationPartition.Http,
            "http-request",
            "No matching component identity was proved.");

        Assert.True(fact.IsRuntime);
        Assert.Null(fact.TargetId);
        Assert.Equal("No matching component identity was proved.", fact.UnresolvedReason);
        Assert.Equal(FactResolution.Unresolved, fact.Header.Resolution);
    }

    [Theory]
    [InlineData(RelationPartition.CompileTime, false)]
    [InlineData(RelationPartition.Inheritance, false)]
    [InlineData(RelationPartition.DependencyInjection, true)]
    [InlineData(RelationPartition.Http, true)]
    [InlineData(RelationPartition.Grpc, true)]
    [InlineData(RelationPartition.Events, true)]
    public void RelationPartition_RuntimeClassification_IsExplicit(RelationPartition partition, bool expected)
    {
        var relationId = RelationFactId.Create(Document.ToFactId(), partition.ToString(), "claim", 1);
        var fact = new RelationFact(
            Header(relationId.ToFactId(), FactKind.Relation, FactResolution.Exact),
            relationId,
            Document.ToFactId(),
            Target.ToFactId(),
            partition,
            "relation",
            null);

        Assert.Equal(expected, fact.IsRuntime);
    }

    [Fact]
    public void ConfigurationResolution_RemainsDistinctFromFactResolution()
    {
        Assert.NotEqual(typeof(FactResolution), typeof(ConfigurationResolution));
        Assert.Equal(
            ["HardCoded", "Dynamic", "Unresolved", "NotApplicable"],
            Enum.GetNames<ConfigurationResolution>());
    }

    [Fact]
    public void CoverageFact_TracksDiagnosticReferencesForInventoriedScope()
    {
        var diagnostic = DiagnosticId.Create("compilation", Target.ToFactId(), "C2M3001", "unavailable");
        var coverage = new CoverageFact(
            Target.ToFactId(),
            FactLevel.Target,
            null,
            CoverageApplicability.Applicable,
            CoverageAttempt.Attempted,
            FactResolution.Syntactic,
            [diagnostic]);

        Assert.Equal(Target.ToFactId(), coverage.ScopeId);
        Assert.Equal(FactResolution.Syntactic, coverage.Resolution);
        Assert.Equal([diagnostic], coverage.DiagnosticIds.ToArray());
    }

    private static FactHeader Header(FactId id, FactKind kind, FactResolution resolution) =>
        FactHeader.Create(id, kind, resolution, [new FactProvenance("csharp2md", "3.0.0")]);
}
