using System.Reflection;
using System.Runtime.CompilerServices;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Relations;

public sealed class RelationContractsTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "OrderService.cs");

    // Design.md's Data Models section lists these five as required members of the raw claim.
    [Theory]
    [InlineData(nameof(RawRelation.Kind))]
    [InlineData(nameof(RawRelation.OwnerId))]
    [InlineData(nameof(RawRelation.Evidence))]
    [InlineData(nameof(RawRelation.ShapeConfidence))]
    [InlineData(nameof(RawRelation.Partition))]
    [InlineData(nameof(RawRelation.Details))]
    public void RawRelation_RequiredMember_IsEnforcedByTheCompiler(string memberName)
    {
        var member = typeof(RawRelation).GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(member);
        Assert.NotNull(member.GetCustomAttribute<RequiredMemberAttribute>());
    }

    [Fact]
    public void RawRelation_CarriesEveryFieldFromTheDesign()
    {
        var evidence = EvidenceAt(4, 1, 4, 30);
        var details = ImmutableArray.Create(new RelationDetail("target_text", "paymentsClient.Authorize"));
        var targetId = SymbolFactId.CreateSyntactic(ProjectId, "PaymentsClient.cs", "method", "method Authorize").ToFactId();

        var claim = new RawRelation
        {
            Kind = "calls",
            OwnerId = OwnerId,
            Evidence = evidence,
            ShapeConfidence = FactResolution.Syntactic,
            Partition = RelationPartition.Structural,
            Details = details,
            TargetText = "paymentsClient.Authorize",
            ReceiverText = "paymentsClient",
            ReceiverTypeText = "PaymentsClient",
            MemberName = "Authorize",
            ArgumentCount = 2,
            ArgumentTypes = ImmutableArray.Create<string?>("string", null),
            Namespace = "Acme.Orders",
            ProjectId = "src/App/App.csproj",
            Imports = ImmutableArray.Create("System", "Acme.Payments"),
            TargetId = targetId,
            ProducerMethod = ResolutionMethod.Configured,
            UnresolvedReason = "receiver-not-proven",
        };

        Assert.Equal("calls", claim.Kind);
        Assert.Equal(OwnerId, claim.OwnerId);
        Assert.Equal(evidence, claim.Evidence);
        Assert.Equal(FactResolution.Syntactic, claim.ShapeConfidence);
        Assert.Equal(RelationPartition.Structural, claim.Partition);
        Assert.Equal(details, claim.Details);
        Assert.Equal("paymentsClient.Authorize", claim.TargetText);
        Assert.Equal("paymentsClient", claim.ReceiverText);
        Assert.Equal("PaymentsClient", claim.ReceiverTypeText);
        Assert.Equal("Authorize", claim.MemberName);
        Assert.Equal(2, claim.ArgumentCount);
        Assert.Equal<string?>(["string", null], claim.ArgumentTypes);
        Assert.Equal("Acme.Orders", claim.Namespace);
        Assert.Equal("src/App/App.csproj", claim.ProjectId);
        Assert.Equal<string>(["System", "Acme.Payments"], claim.Imports);
        Assert.Equal(targetId, claim.TargetId);
        Assert.Equal(ResolutionMethod.Configured, claim.ProducerMethod);
        Assert.Equal("receiver-not-proven", claim.UnresolvedReason);
    }

    // The evidence guard: a claim without evidence cannot exist.
    [Fact]
    public void RawRelation_WithoutEvidence_IsRejectedAtConstruction()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RawRelation
        {
            Kind = "calls",
            OwnerId = OwnerId,
            Evidence = default,
            ShapeConfidence = FactResolution.Syntactic,
            Partition = RelationPartition.Structural,
            Details = [],
        });

        Assert.Equal("evidence", exception.ParamName);
    }

    [Fact]
    public void RawRelation_WithOnlyRequiredMembers_ResolvesNothing()
    {
        var claim = Claim();

        Assert.Null(claim.TargetText);
        Assert.Null(claim.ReceiverText);
        Assert.Null(claim.ReceiverTypeText);
        Assert.Null(claim.MemberName);
        Assert.Null(claim.ArgumentCount);
        Assert.Empty(claim.ArgumentTypes);
        Assert.Null(claim.Namespace);
        Assert.Null(claim.ProjectId);
        Assert.Empty(claim.Imports);
        Assert.Null(claim.TargetId);
        Assert.Null(claim.ProducerMethod);
        Assert.Null(claim.UnresolvedReason);
    }

    private static FactId OwnerId =>
        SymbolFactId.CreateSyntactic(ProjectId, "OrderService.cs", "class", "class OrderService").ToFactId();

    private static RawRelation Claim() => new()
    {
        Kind = "calls",
        OwnerId = OwnerId,
        Evidence = EvidenceAt(3, 1, 3, 20),
        ShapeConfidence = FactResolution.Syntactic,
        Partition = RelationPartition.Structural,
        Details = [new RelationDetail("target_text", "paymentsClient.Authorize")],
    };

    private static Evidence EvidenceAt(int startLine, int startColumn, int endLine, int endColumn) =>
        new(DocumentId, "src/App/OrderService.cs", startLine, startColumn, endLine, endColumn);
}
