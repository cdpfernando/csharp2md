using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.DataAccess.Sql;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Relations.Resolution;

public sealed class DatabaseRelationStrategyTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Repository.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Repository.cs", "class", "class:OrderRepository").ToFactId();
    private static readonly FactId TargetId =
        DatabaseObjectFactId.Create(DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "orders").ToFactId();
    private static readonly HashSet<FactId> NoKnownFacts = [];
    private static readonly DatabaseRelationStrategy Strategy = new();

    [Fact]
    public void TryResolve_ToTableConfiguredTarget_YieldsConfigured()
    {
        var claim = DataClaim(TargetId, mapping: DatabaseMappingResolver.ConfiguredMapping);

        var outcome = Strategy.TryResolve(Context(claim));

        Assert.True(outcome.Handled);
        Assert.Equal(TargetId, outcome.TargetId);
        Assert.Equal(ResolutionMethod.Configured, outcome.Method);
    }

    [Fact]
    public void TryResolve_ConventionNamedTarget_YieldsConventionAndDiagnosesC2MRELR005()
    {
        var claim = DataClaim(targetId: null, mapping: DatabaseMappingResolver.ConventionMapping);

        var outcome = Strategy.TryResolve(Context(claim));

        Assert.True(outcome.Handled);
        Assert.Null(outcome.TargetId);
        Assert.Equal(ResolutionMethod.Convention, outcome.Method);
        Assert.Equal("C2M-RELR-005", outcome.Diagnostic!.Code);
    }

    [Fact]
    public void TryResolve_NoDatabaseTargetIsEverMarkedExact()
    {
        var configured = Strategy.TryResolve(Context(DataClaim(TargetId, mapping: DatabaseMappingResolver.ConfiguredMapping)));
        var convention = Strategy.TryResolve(Context(DataClaim(null, mapping: DatabaseMappingResolver.ConventionMapping)));

        Assert.NotEqual(ResolutionMethod.Exact, configured.Method);
        Assert.NotEqual(ResolutionMethod.Exact, convention.Method);
    }

    [Fact]
    public void TryResolve_InterpolatedSqlTarget_YieldsDynamicWithNullTargetKeepsObservedTextAndDiagnosesC2MRELR004()
    {
        var claim = DataClaim(targetId: null, mapping: null) with
        {
            UnresolvedReason = SqlTextAnalyzer.DynamicSqlReason,
            TargetText = SqlTextAnalyzer.DynamicTargetText,
        };

        var outcome = Strategy.TryResolve(Context(claim));

        Assert.True(outcome.Handled);
        Assert.Null(outcome.TargetId);
        Assert.Equal(ResolutionMethod.Dynamic, outcome.Method);
        Assert.Equal("C2M-RELR-004", outcome.Diagnostic!.Code);
    }

    [Fact]
    public void TryResolve_TheClaimsDatabaseOperationAndRelationKindAreUnchangedByTheStrategy()
    {
        var claim = DataClaim(TargetId, mapping: DatabaseMappingResolver.ConfiguredMapping) with { Kind = DatabaseMappingResolver.ReadsKind };

        Strategy.TryResolve(Context(claim));

        Assert.Equal(DatabaseMappingResolver.ReadsKind, claim.Kind);
        Assert.Contains(claim.Details, detail => detail is { Key: "operation" });
    }

    [Fact]
    public void TryResolve_ARelationOutsideTheDataPartition_IsDeclinedWithoutInspectingDetails()
    {
        var claim = DataClaim(TargetId, mapping: DatabaseMappingResolver.ConfiguredMapping) with
        {
            Partition = RelationPartition.Structural,
        };

        var outcome = Strategy.TryResolve(Context(claim));

        Assert.False(outcome.Handled);
    }

    private static RelationResolutionContext Context(RawRelation claim) =>
        new(claim, SymbolIndexBuilder.Build([], [], [], []), NoKnownFacts);

    private static RawRelation DataClaim(FactId? targetId, string? mapping)
    {
        var details = ImmutableArray.CreateBuilder<RelationDetail>();
        details.Add(new RelationDetail("operation", "read"));
        details.Add(new RelationDetail("target_text", "orders"));
        if (mapping is not null)
        {
            details.Add(new RelationDetail("mapping", mapping));
        }

        return new RawRelation
        {
            Kind = DatabaseMappingResolver.MapsToKind,
            OwnerId = OwnerId,
            Evidence = new Evidence(DocumentId, "Repository.cs", 1, 1, 1, 10),
            ShapeConfidence = targetId is not null ? FactResolution.Exact : FactResolution.Heuristic,
            Partition = RelationPartition.Data,
            Details = details.ToImmutable(),
            TargetText = "orders",
            TargetId = targetId,
            ProducerMethod = targetId is not null ? ResolutionMethod.Configured : null,
            UnresolvedReason = targetId is null && mapping == DatabaseMappingResolver.ConventionMapping
                ? DatabaseMappingResolver.ConventionMappingReason
                : null,
        };
    }
}
