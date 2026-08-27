using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Postings;

public sealed class ContractDataAccessPostingTests
{
    [Fact]
    [Trait("Requirement", "RP-25")]
    public void Project_ContractConsumers_UseBindingPayloadRoleNotOperationName()
    {
        var (view, contract, requestOperation, _) = ContractFixture();

        var fragments = PostingProjector.Project(view);
        var consumers = Assert.Single(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractConsumersKey),
            group => group.FactId == contract.Reference.Id.Value);
        var producers = PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractProducersKey);

        var consumerSources = consumers.Entries
            .Select(entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id)
            .ToArray();
        Assert.Contains(requestOperation.Reference.Id.Value, consumerSources);
        Assert.DoesNotContain(
            producers.SelectMany(static group => group.Entries),
            entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id == requestOperation.Reference.Id.Value);
        Assert.Contains("Produce", requestOperation.Symbol.Id.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    public void Project_ContractProducers_UseBindingPayloadRoleNotOperationName()
    {
        var (view, contract, _, responseOperation) = ContractFixture();

        var fragments = PostingProjector.Project(view);
        var producers = Assert.Single(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractProducersKey),
            group => group.FactId == contract.Reference.Id.Value);
        var consumers = PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractConsumersKey);

        var producerSources = producers.Entries
            .Select(entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id)
            .ToArray();
        Assert.Equal(responseOperation.Reference.Id.Value, Assert.Single(producerSources));
        Assert.DoesNotContain(
            consumers.SelectMany(static group => group.Entries),
            entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id == responseOperation.Reference.Id.Value);
        Assert.Contains("Consume", responseOperation.Symbol.Id.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    public void Project_ContractRoles_FollowBindingEvenWhenRelationFacetDiffers()
    {
        var (view, contract, _, responseOperation) = ContractFixture();
        var binding = Assert.Single(
            view.Document.ContractBindings,
            candidate => candidate.Operation.Id == responseOperation.Reference.Id.Value);
        var uses = view.Document.ConfirmedRelations["uses-contract"]
            .Single(relation => relation.Source.Id == responseOperation.Reference.Id.Value);

        Assert.Equal("response", binding.PayloadRole);
        Assert.Contains(uses.Facets, facet => facet.AxisName == "payload-role" && facet.WireValue == "request");

        var producers = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.ContractProducersKey),
            group => group.FactId == contract.Reference.Id.Value);
        var produced = PostingProjectionFactory.RelationAt(view, Assert.Single(producers.Entries));
        Assert.Equal(responseOperation.Reference.Id.Value, produced.Source.Id);
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    [Trait("Requirement", "RP-27")]
    public void Project_ContractPostings_CitedOrdinalResolvesToClaimedUsesContract()
    {
        var (view, contract, requestOperation, responseOperation) = ContractFixture();

        var fragments = PostingProjector.Project(view);
        var consumers = Assert.Single(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractConsumersKey),
            group => group.FactId == contract.Reference.Id.Value);
        var producers = Assert.Single(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.ContractProducersKey),
            group => group.FactId == contract.Reference.Id.Value);

        var consumed = PostingProjectionFactory.RelationAt(view, Assert.Single(consumers.Entries));
        Assert.Equal("uses-contract", consumed.Kind);
        Assert.Equal(requestOperation.Reference.Id.Value, consumed.Source.Id);
        Assert.Equal(contract.Reference.Id.Value, consumed.Target.Id);
        var produced = PostingProjectionFactory.RelationAt(view, Assert.Single(producers.Entries));
        Assert.Equal("uses-contract", produced.Kind);
        Assert.Equal(responseOperation.Reference.Id.Value, produced.Source.Id);
        Assert.Equal(contract.Reference.Id.Value, produced.Target.Id);
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    public void Project_DataReaders_UseOperationKindNotCallableName()
    {
        var (view, dataObject, readOperation, _, readerNamedWrite, _) = DataFixture();

        var readers = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.DataReadersKey),
            group => group.FactId == dataObject.Reference.Id.Value);
        var writers = PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.DataWritersKey);

        Assert.Equal("read", Assert.Single(view.Document.DataOperations, dto => dto.Identity.Id == readOperation.Reference.Id.Value).Operation);
        Assert.Contains("Write", readerNamedWrite.Signature.Value, StringComparison.Ordinal);
        var cited = PostingProjectionFactory.RelationAt(view, Assert.Single(readers.Entries));
        Assert.Equal(readOperation.Reference.Id.Value, cited.Source.Id);
        Assert.Equal(dataObject.Reference.Id.Value, cited.Target.Id);
        Assert.DoesNotContain(
            writers.SelectMany(static group => group.Entries),
            entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id == readOperation.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    public void Project_DataWriters_UseOperationKindNotCallableName()
    {
        var (view, dataObject, _, writeOperation, _, writerNamedRead) = DataFixture();

        var writers = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.DataWritersKey),
            group => group.FactId == dataObject.Reference.Id.Value);
        var readers = PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.DataReadersKey);

        Assert.Equal("insert", Assert.Single(view.Document.DataOperations, dto => dto.Identity.Id == writeOperation.Reference.Id.Value).Operation);
        Assert.Contains("Read", writerNamedRead.Signature.Value, StringComparison.Ordinal);
        var cited = PostingProjectionFactory.RelationAt(view, Assert.Single(writers.Entries));
        Assert.Equal(writeOperation.Reference.Id.Value, cited.Source.Id);
        Assert.Equal(dataObject.Reference.Id.Value, cited.Target.Id);
        Assert.DoesNotContain(
            readers.SelectMany(static group => group.Entries),
            entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id == writeOperation.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    [Trait("Requirement", "RP-27")]
    public void Project_DataAccessPostings_CitedOrdinalResolvesToClaimedOperatesOn()
    {
        var (view, dataObject, readOperation, writeOperation, _, _) = DataFixture();

        var fragments = PostingProjector.Project(view);
        foreach (var entry in PostingProjectionFactory.ReadPosting(fragments, PostingProjector.DataReadersKey)
            .Concat(PostingProjectionFactory.ReadPosting(fragments, PostingProjector.DataWritersKey))
            .SelectMany(static group => group.Entries))
        {
            var relation = PostingProjectionFactory.RelationAt(view, entry);
            Assert.Equal("operates-on", relation.Kind);
            Assert.Equal(dataObject.Reference.Id.Value, relation.Target.Id);
            Assert.Contains(
                new[] { readOperation.Reference.Id.Value, writeOperation.Reference.Id.Value },
                id => id == relation.Source.Id);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-26")]
    public void Project_ContractAndDataPostings_EntriesCarryKeyAndOrdinalOnly()
    {
        var (contractView, _, _, _) = ContractFixture();
        var (dataView, _, _, _, _, _) = DataFixture();

        PostingProjectionFactory.AssertEntriesAreCitationsOnly(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(contractView), PostingProjector.ContractConsumersKey));
        PostingProjectionFactory.AssertEntriesAreCitationsOnly(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(contractView), PostingProjector.ContractProducersKey));
        PostingProjectionFactory.AssertEntriesAreCitationsOnly(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(dataView), PostingProjector.DataReadersKey));
        PostingProjectionFactory.AssertEntriesAreCitationsOnly(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(dataView), PostingProjector.DataWritersKey));
    }

    private static (
        PublishedPackageView View,
        Contract Contract,
        BoundaryOperation RequestOperation,
        BoundaryOperation ResponseOperation) ContractFixture()
    {
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var requestOperation = CatalogProjectionFactory.CreateInboundOperation("ProduceOrders", "Orders.Api", "POST /produce");
        var responseOperation = CatalogProjectionFactory.CreateInboundOperation("ConsumeOrders", "Orders.Api", "POST /consume");
        var requestSymbol = CatalogProjectionFactory.Callable("ProduceOrders");
        var responseSymbol = CatalogProjectionFactory.Callable("ConsumeOrders");
        var view = CatalogProjectionFactory.ViewOf(
            [
                contract,
                requestOperation,
                responseOperation,
                requestSymbol,
                responseSymbol,
                ContractBinding.Create(requestOperation.Reference, "request", requestSymbol.Reference, contract.Reference),
                ContractBinding.Create(responseOperation.Reference, "response", responseSymbol.Reference, contract.Reference),
            ],
            [
                PostingProjectionFactory.UsesContract(requestOperation, contract, "request", 1),
                PostingProjectionFactory.UsesContract(responseOperation, contract, "request", 2),
            ]);
        return (view, contract, requestOperation, responseOperation);
    }

    private static (
        PublishedPackageView View,
        DataObject DataObject,
        DataOperation ReadOperation,
        DataOperation WriteOperation,
        Symbol ReaderNamedWrite,
        Symbol WriterNamedRead) DataFixture()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var readOperation = DataOperation.Create(dataObject.Reference, DataOperationKind.Read, MappingStateKind.ExplicitConfirmation);
        var writeOperation = DataOperation.Create(dataObject.Reference, DataOperationKind.Insert, MappingStateKind.ExplicitConfirmation);
        var readerNamedWrite = CatalogProjectionFactory.Callable("WriteOrders");
        var writerNamedRead = CatalogProjectionFactory.Callable("ReadOrders");
        var view = CatalogProjectionFactory.ViewOf(
            [store, dataObject, readOperation, writeOperation, readerNamedWrite, writerNamedRead],
            [
                PostingProjectionFactory.AccessesData(readerNamedWrite, readOperation, 1),
                PostingProjectionFactory.AccessesData(writerNamedRead, writeOperation, 2),
                PostingProjectionFactory.OperatesOn(readOperation, dataObject, 3),
                PostingProjectionFactory.OperatesOn(writeOperation, dataObject, 4),
            ]);
        return (view, dataObject, readOperation, writeOperation, readerNamedWrite, writerNamedRead);
    }
}
