using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Postings;

/// <summary>
/// GCPC-089, GCPC-091, GCPC-092: a messaging contract's producer and consumer, recoverable from
/// postings in one hop from the contract identity (T57). <c>ContractPass</c> binds every messaging
/// operation with payload role <c>"request"</c> regardless of direction (EBC-22/EBC-26 --
/// <see cref="ContractPassTests"/> asserts this for both the outbound and the inbound side), so unlike
/// the HTTP-shaped RP-25 fixture (<see cref="ContractDataAccessPostingTests"/>, which deliberately
/// names its operations opposite to their payload role), direction is the only signal available for a
/// messaging contract. These fixtures build <c>BoundaryOperation</c> facts with
/// <c>Protocol = Messaging</c> directly, matching <c>ContractPass</c>'s real output exactly.
///
/// T4's <c>Certification.Messaging/ContractShapes.cs</c> fixture is not used here: its
/// <c>OrderCreated</c> publisher and handler are both declared inside <c>Certification.Messaging</c>,
/// and <c>ContractPass.IsSharedAcrossProjects</c> (EBC-25, tested by
/// <c>ContractPassTests.Execute_EventTypeDeclaredInSameProjectAsPublisherAndHandler_DoesNotCreateContract</c>)
/// means that pair can never become a <c>Contract</c> fact at all -- confirmed by a real
/// <c>analyze</c> run of the certification corpus publishing no <c>facts/contract.json</c> whatsoever
/// (see T54's deviation note in <c>tasks.md</c>). The GCPC-087/GCPC-092 discrete-record gap this
/// deferred (a nameable, unhandled published message -- <c>OrderShipped</c> -- reaching none of
/// contract binding, candidate or unresolved record) is closed by the Verifier's Fix 2:
/// <c>RelationPass.EmitUnresolved</c> now publishes a real <c>UnresolvedRecord</c> for it, proven end
/// to end against the real corpus by
/// <c>Csharp2Md.Analysis.Tests.Fixtures.CertificationCorpusContractTests.AnalyzeAsync_CertificationCorpus_OrderShippedReachesExactlyOneOfTheFourOutcomes</c>.
/// The hand-built <see cref="UnresolvedRecord"/> below still stands: it proves
/// <see cref="PostingProjector"/>'s own response to an unresolved contract identity in isolation from
/// whichever classifier produced it.
/// </summary>
public sealed class MessagingContractPostingTests
{
    [Fact]
    [Trait("Requirement", "GCPC-091")]
    public void Project_MessagingContractWithBothDirectionsBoundAsRequest_ReachesProducerAndConsumerInOneHop()
    {
        var (view, contract, publish, handle) = MessagingContractFixture();

        var fragments = PostingProjector.Project(view);
        var producers = Assert.Single(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractProducersKey),
            group => group.FactId == contract.Reference.Id.Value);
        var consumers = Assert.Single(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractConsumersKey),
            group => group.FactId == contract.Reference.Id.Value);

        var produced = PostingProjectionFactory.RelationAt(view, Assert.Single(producers.Entries));
        Assert.Equal(publish.Reference.Id.Value, produced.Source.Id);
        var consumed = PostingProjectionFactory.RelationAt(view, Assert.Single(consumers.Entries));
        Assert.Equal(handle.Reference.Id.Value, consumed.Source.Id);
        Assert.DoesNotContain(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractProducersKey)
                .SelectMany(static group => group.Entries),
            entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id == handle.Reference.Id.Value);
        Assert.DoesNotContain(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractConsumersKey)
                .SelectMany(static group => group.Entries),
            entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id == publish.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-091")]
    public void Project_MessagingProducersAndConsumers_AreCitationsOnly()
    {
        var (view, _, _, _) = MessagingContractFixture();
        var fragments = PostingProjector.Project(view);

        PostingProjectionFactory.AssertEntriesAreCitationsOnly(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractProducersKey));
        PostingProjectionFactory.AssertEntriesAreCitationsOnly(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractConsumersKey));
    }

    [Fact]
    [Trait("Requirement", "GCPC-092")]
    public void Project_AnUnresolvedContractIdentity_SurfacesThroughUnknownsNeverAsAContractPosting()
    {
        // Simulates what a classifier proving no contract identity must publish (GCPC-092): an
        // UnresolvedRecord, never a Contract/ContractBinding. PostingProjector's own job -- proven
        // here -- is to surface it through postings/unknowns.json and never fabricate a producer or
        // consumer entry for it.
        var operation = MessagingOperation("PublishOrderShipped", "Certification.Messaging", BoundaryDirection.Outbound, "OrderShipped");
        var unresolved = UnresolvedRecord.Create(
            RelationKind.UsesContract,
            operation.Reference,
            UnresolvedCause.NoCandidateFound,
            PostingProjectionFactory.Evidence(operation.Reference, 1));
        var view = CatalogProjectionFactory.ViewOf([operation], unresolved: [unresolved]);

        var fragments = PostingProjector.Project(view);

        var unknowns = PostingProjectionFactory.ReadPosting(fragments, PostingProjector.UnknownsKey);
        Assert.Contains(unknowns, group => group.FactId == operation.Reference.Id.Value);
        // PostingProjector.Add skips writing a posting family fragment at all when its group set is
        // empty (Project(...) never fabricates an empty producers/consumers artifact for this fixture),
        // so absence of the fragment itself is the proof no contract posting was ever published.
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey == PostingProjector.ContractProducersKey);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey == PostingProjector.ContractConsumersKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-092")]
    public void Project_ACandidateContractIdentity_NeverPublishesAsAContractPosting()
    {
        // A CandidateLink (kind uses-contract) has no matching ContractBinding, so AddContractRole's
        // binding lookup finds nothing and correctly excludes it from both groups -- a candidate never
        // becomes a contract posting.
        var operation = MessagingOperation("PublishOrderShipped", "Certification.Messaging", BoundaryDirection.Outbound, "OrderShipped");
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderShipped");
        var candidate = CandidateLink.Create(
            RelationKind.UsesContract,
            operation.Reference,
            contract.Reference,
            PostingProjectionFactory.Evidence(operation.Reference, 1));
        var view = CatalogProjectionFactory.ViewOf([operation, contract], candidates: [candidate]);

        var fragments = PostingProjector.Project(view);

        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey == PostingProjector.ContractProducersKey);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey == PostingProjector.ContractConsumersKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-089")]
    public void Project_PostingProjectorOutput_NeverCarriesProseThatCouldClaimMessagingIsAbsent()
    {
        // PostingProjector never emits diagnostic or descriptive prose -- only structured
        // (fact-id, citation) postings -- so it cannot itself be where a "no messaging" claim leaks
        // from when only the contract is absent (GCPC-089). Every published group is a bare FactId
        // plus citation-only entries, proven for every posting family this fixture produces.
        var (view, _, _, _) = MessagingContractFixture();
        var fragments = PostingProjector.Project(view);

        Assert.NotEmpty(fragments);
        foreach (var fragment in fragments)
        {
            var payload = fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;
            var groups = CanonicalJson.Read<ImmutableArray<PostingGroupDto>>(payload.AsSpan());
            foreach (var group in groups)
            {
                Assert.False(string.IsNullOrEmpty(group.FactId));
                Assert.All(group.Entries, PostingProjectionFactory.AssertCitationOnly);
            }
        }
    }

    private static (
        PublishedPackageView View,
        Contract Contract,
        BoundaryOperation Publish,
        BoundaryOperation Handle) MessagingContractFixture()
    {
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var publish = MessagingOperation("PublishOrderPlaced", "Certification.Messaging", BoundaryDirection.Outbound, "OrderPlaced");
        var handle = MessagingOperation("HandleOrderPlaced", "Certification.Messaging", BoundaryDirection.Inbound, "OrderPlaced");
        var publishSymbol = CatalogProjectionFactory.Callable("PublishOrderPlaced");
        var handleSymbol = CatalogProjectionFactory.Callable("HandleOrderPlaced");
        var view = CatalogProjectionFactory.ViewOf(
            [
                contract,
                publish,
                handle,
                publishSymbol,
                handleSymbol,
                ContractBinding.Create(publish.Reference, "request", publishSymbol.Reference, contract.Reference),
                ContractBinding.Create(handle.Reference, "request", handleSymbol.Reference, contract.Reference),
            ],
            [
                PostingProjectionFactory.UsesContract(publish, contract, "request", 1),
                PostingProjectionFactory.UsesContract(handle, contract, "request", 2),
            ]);
        return (view, contract, publish, handle);
    }

    private static BoundaryOperation MessagingOperation(
        string symbolName,
        string componentName,
        BoundaryDirection direction,
        string eventTypeKey) =>
        BoundaryOperation.Create(
            CatalogProjectionFactory.Callable(symbolName).Reference,
            CatalogProjectionFactory.CreateComponent(componentName).Reference,
            direction,
            BoundaryProtocol.Messaging,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, eventTypeKey, "protocolOperationKey"));
}
