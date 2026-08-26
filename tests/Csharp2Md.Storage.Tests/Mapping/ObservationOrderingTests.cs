using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class ObservationOrderingTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    [Trait("Requirement", "STOR-02")]
    public void ToWire_SameOwnerKindAndOrdinalDifferentPayload_OrdersIdenticallyAcrossInsertionOrder()
    {
        var alpha = CreateInvocation(payloadValue: "alpha");
        var beta = CreateInvocation(payloadValue: "beta");
        Assert.NotEqual(ContentHash(alpha), ContentHash(beta));

        var forward = MappedInvocations(alpha, beta);
        var reverse = MappedInvocations(beta, alpha);

        Assert.Equal(2, forward.Length);
        Assert.Equal(forward[0].ContentSha256, reverse[0].ContentSha256);
        Assert.Equal(forward[1].ContentSha256, reverse[1].ContentSha256);
        Assert.NotEqual(forward[0].ContentSha256, forward[1].ContentSha256);
        Assert.True(
            string.Compare(forward[0].ContentSha256, forward[1].ContentSha256, StringComparison.Ordinal) < 0);
    }

    private static ImmutableArray<ObservationDto> MappedInvocations(Observation first, Observation second)
    {
        var document = DomainMapper.ToWire(
            new FactualSnapshot([], [first, second], [], [], [], []),
            Context);
        return document.Observations["invocation"];
    }

    private static string ContentHash(Observation observation) =>
        WireObservationMapping.ToDto(observation).ContentSha256;

    private static Observation CreateInvocation(string payloadValue)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var owner = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln")).Reference;
        return Observation.Create(
            owner,
            ObservationKind.Invocation,
            NormalizedPayload.Create([
                new PayloadEntry(
                    "name",
                    StructuralLiteral.Create(LiteralRole.ClientName, payloadValue, "name"))]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Payments/Invoice.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("BIND001", "Bound successfully."),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
    }
}
