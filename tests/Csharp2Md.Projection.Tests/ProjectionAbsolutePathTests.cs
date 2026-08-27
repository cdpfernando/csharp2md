using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Validation;

namespace Csharp2Md.Projection.Tests;

public sealed class ProjectionAbsolutePathTests
{
    [Fact]
    [Trait("Requirement", "RP-49")]
    public void PackageProjector_PublishedProjections_ContainNoAbsolutePath()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"),
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"),
            store,
            dataObject);
        var reader = new EmptySourceReader();

        var fragments = new PackageProjector().Project(view, reader);

        Assert.NotEmpty(fragments);
        ProjectionValidator.Validate(view, fragments);
        Assert.All(fragments, static fragment => AssertNoAbsolutePath(fragment.CanonicalKey, fragment.Payload));
    }

    [Fact]
    [Trait("Requirement", "RP-49")]
    public void Commit_PublishedProjections_ContainNoAbsolutePath()
    {
        var store = new InMemoryTransactionalStore(new PackageProjector());
        var session = store.Open("s-test", new EmptySourceReader());
        session.Stage(
            DomainMapperSnapshot(
                CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
                CatalogProjectionFactory.CreateComponent("Orders.Api")));
        var publication = session.Commit();

        var projections = publication.ArtifactsInPublicationOrder
            .Where(static fragment => IsProjection(fragment.CanonicalKey))
            .ToArray();
        Assert.NotEmpty(projections);
        Assert.All(
            projections,
            static fragment => AssertNoAbsolutePath(
                fragment.CanonicalKey,
                fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload));
    }

    private static FactualSnapshot DomainMapperSnapshot(params Csharp2Md.Domain.Facts.IFact[] facts) =>
        new(facts.ToImmutableArray(), [], [], [], [], []);

    private static bool IsProjection(string key) =>
        key.StartsWith("catalogs/", StringComparison.Ordinal)
        || key.StartsWith("postings/", StringComparison.Ordinal)
        || key.StartsWith("markdown/", StringComparison.Ordinal)
        || key.StartsWith("source/", StringComparison.Ordinal)
        || key is "retrieval.md" or "AGENTS.md";

    private static void AssertNoAbsolutePath(string key, ImmutableArray<byte> payload)
    {
        Assert.False(Path.IsPathRooted(key), key);
        Assert.DoesNotContain(":\\", key, StringComparison.Ordinal);
        Assert.False(key.StartsWith("/", StringComparison.Ordinal), key);
        var text = Encoding.UTF8.GetString(payload.AsSpan());
        Assert.DoesNotContain(":\\", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/home/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/opt/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Users/", text, StringComparison.Ordinal);
    }
}
