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
    public void FactResolution_DeclaresMembersInDescendingConfidenceOrder() =>
        Assert.Equal(
            ["Exact", "Partial", "Syntactic", "Heuristic", "Candidate", "Unresolved", "NotApplicable"],
            Enum.GetNames<FactResolution>());

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
            typeof(DatabaseObjectFact), typeof(DatabaseColumnFact),
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
            [],
            Semantics: null,
            Name: "Feature",
            FullyQualifiedName: "global::Feature",
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: "class Feature",
            Arity: 0,
            ParameterTypes: []);

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
    public void FactKind_DeclaresThePersistenceNodeMembers() =>
        Assert.Equal(
            ["Solution", "Project", "Target", "Document", "SourceSection", "Symbol", "Component", "Relation", "DatabaseObject", "DatabaseColumn"],
            Enum.GetNames<FactKind>());

    [Fact]
    public void DatabaseObjectFact_IsAFactCarryingItsTypedIdentityConnectionKindAndProvenName()
    {
        var objectId = DatabaseObjectFactId.Create(
            DatabaseObjectFactId.UnknownConnection,
            DatabaseObjectKind.Table,
            "tb_order");
        var fact = new DatabaseObjectFact(
            Header(objectId.ToFactId(), FactKind.DatabaseObject, FactResolution.Exact),
            objectId,
            DatabaseObjectFactId.UnknownConnection,
            DatabaseObjectKind.Table,
            "tb_order");

        Assert.IsAssignableFrom<IFact>(fact);
        Assert.Equal(objectId, fact.ObjectId);
        Assert.Equal(objectId.ToFactId(), fact.Header.Id);
        Assert.Equal(FactKind.DatabaseObject, fact.Header.Kind);
        Assert.Equal("unknown", fact.ConnectionName);
        Assert.Equal(DatabaseObjectKind.Table, fact.Kind);
        Assert.Equal("tb_order", fact.Name);
        Assert.Equal(FactResolution.Exact, fact.Header.Resolution);
    }

    [Fact]
    public void DatabaseColumnFact_IsAFactReferencingItsOwningObjectAndProvenName()
    {
        var objectId = DatabaseObjectFactId.Create(
            DatabaseObjectFactId.UnknownConnection,
            DatabaseObjectKind.Table,
            "tb_order");
        var columnId = DatabaseColumnFactId.Create(objectId, "order_status");
        var fact = new DatabaseColumnFact(
            Header(columnId.ToFactId(), FactKind.DatabaseColumn, FactResolution.Exact),
            columnId,
            objectId,
            "order_status");

        Assert.IsAssignableFrom<IFact>(fact);
        Assert.Equal(columnId, fact.ColumnId);
        Assert.Equal(columnId.ToFactId(), fact.Header.Id);
        Assert.Equal(FactKind.DatabaseColumn, fact.Header.Kind);
        Assert.Equal(objectId, fact.ObjectId);
        Assert.Equal("order_status", fact.Name);
    }

    [Fact]
    public void DatabaseObjectKind_DeclaresTheFullPersistenceValueSet() =>
        Assert.Equal(
            ["Table", "View", "Procedure", "Function", "Unknown"],
            Enum.GetNames<DatabaseObjectKind>());

    [Fact]
    public void DatabaseOperation_DeclaresTheFullPersistenceValueSet() =>
        Assert.Equal(
            ["Read", "Insert", "Update", "Delete", "Execute", "Unknown"],
            Enum.GetNames<DatabaseOperation>());

    [Fact]
    public void ColumnUsage_DeclaresTheFullPersistenceValueSet() =>
        Assert.Equal(
            ["Read", "Write", "Filter", "Join", "Order", "Group", "Aggregate", "Unknown"],
            Enum.GetNames<ColumnUsage>());

    [Theory]
    [InlineData(DatabaseObjectKind.Table, "table")]
    [InlineData(DatabaseObjectKind.View, "view")]
    [InlineData(DatabaseObjectKind.Procedure, "procedure")]
    [InlineData(DatabaseObjectKind.Function, "function")]
    [InlineData(DatabaseObjectKind.Unknown, "unknown")]
    public void DatabaseObjectKind_WireName_IsTheSpecifiedLiteral(DatabaseObjectKind kind, string expected) =>
        Assert.Equal(expected, DatabaseFactWire.Name(kind));

    [Theory]
    [InlineData(DatabaseOperation.Read, "read")]
    [InlineData(DatabaseOperation.Insert, "insert")]
    [InlineData(DatabaseOperation.Update, "update")]
    [InlineData(DatabaseOperation.Delete, "delete")]
    [InlineData(DatabaseOperation.Execute, "execute")]
    [InlineData(DatabaseOperation.Unknown, "unknown")]
    public void DatabaseOperation_WireName_IsTheSpecifiedLiteral(DatabaseOperation operation, string expected) =>
        Assert.Equal(expected, DatabaseFactWire.Name(operation));

    [Theory]
    [InlineData(ColumnUsage.Read, "read")]
    [InlineData(ColumnUsage.Write, "write")]
    [InlineData(ColumnUsage.Filter, "filter")]
    [InlineData(ColumnUsage.Join, "join")]
    [InlineData(ColumnUsage.Order, "order")]
    [InlineData(ColumnUsage.Group, "group")]
    [InlineData(ColumnUsage.Aggregate, "aggregate")]
    [InlineData(ColumnUsage.Unknown, "unknown")]
    public void ColumnUsage_WireName_IsTheSpecifiedLiteral(ColumnUsage usage, string expected) =>
        Assert.Equal(expected, DatabaseFactWire.Name(usage));

    public static TheoryData<string[]> PersistenceWireNames => new()
    {
        Enum.GetValues<DatabaseObjectKind>().Select(DatabaseFactWire.Name).ToArray(),
        Enum.GetValues<DatabaseOperation>().Select(DatabaseFactWire.Name).ToArray(),
        Enum.GetValues<ColumnUsage>().Select(DatabaseFactWire.Name).ToArray(),
    };

    [Theory]
    [MemberData(nameof(PersistenceWireNames))]
    public void PersistenceEnum_EveryMember_MapsToADistinctLowerCaseWireName(string[] wireNames)
    {
        Assert.Distinct(wireNames, StringComparer.Ordinal);
        Assert.All(wireNames, wireName => Assert.Equal(wireName.ToLowerInvariant(), wireName, StringComparer.Ordinal));
        Assert.All(wireNames, wireName => Assert.NotEmpty(wireName));
    }

    [Fact]
    public void DatabaseObjectKind_UndeclaredMember_IsRejectedRatherThanNamed() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseFactWire.Name((DatabaseObjectKind)99));

    [Fact]
    public void DatabaseOperation_UndeclaredMember_IsRejectedRatherThanNamed() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseFactWire.Name((DatabaseOperation)99));

    [Fact]
    public void ColumnUsage_UndeclaredMember_IsRejectedRatherThanNamed() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseFactWire.Name((ColumnUsage)99));

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
