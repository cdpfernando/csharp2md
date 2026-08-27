using System.Text.Json.Nodes;
using Csharp2Md.Analysis;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Composition;

public sealed class CrossSolutionCorrelationTests
{
    [Fact]
    [Trait("Requirement", "MSC-16")]
    [Trait("Requirement", "MSC-21")]
    [Trait("Requirement", "MSC-22")]
    public async Task Analyze_OrdersAndShipping_PublishesTheMessagingPairHttpCandidateAndSharedContract()
    {
        var output = Path.Combine(Path.GetTempPath(), "csharp2md-cross-solution-" + Guid.NewGuid().ToString("N"));
        try
        {
            var run = await CompositionBatch.AnalyzeAsync(
                output,
                CompositionBatch.OrdersSolutionPath(),
                CompositionBatch.ShippingSolutionPath());

            Assert.All(run.Result.Solutions, outcome => Assert.Equal(PublicationStatus.Committed, outcome.Status));
            var manifest = CanonicalJson.Read<BatchManifestEnvelope>(run.Files["batch-manifest.json"]);
            Assert.Equal(2, manifest.Solutions.Length);
            Assert.True(manifest.Complete);

            var relation = Required(Assert.Single(
                CompositionBatch.ReadArray(run.Files, "composition/cross-solution-relations.json")));
            Assert.Equal("targets", CompositionBatch.Text(relation, "kind"));
            Assert.Contains("OrderPlaced", CompositionBatch.Text(relation, "matched_key"), StringComparison.Ordinal);
            Assert.DoesNotContain("PaymentProcessed", relation.ToJsonString(), StringComparison.Ordinal);

            var candidate = Required(Assert.Single(
                CompositionBatch.ReadArray(run.Files, "composition/correlation-candidates.json")));
            Assert.Equal("POST shipments", CompositionBatch.Text(candidate, "matched_key"));
            Assert.Equal("ShippingService", CompositionBatch.Text(candidate, "destination_scope"));
            Assert.DoesNotContain("PaymentProcessed", candidate.ToJsonString(), StringComparison.Ordinal);

            var ordersPackage = PackageNamed(run, "Acme.Orders.slnx");
            var shippingPackage = PackageNamed(run, "Acme.Shipping.slnx");

            var sourceOperation = CompositionBatch.ElementAt(
                ordersPackage,
                CompositionBatch.Text(relation, "source_artifact_key"),
                relation["source_ordinal"]!.GetValue<int>());
            Assert.Equal(CompositionBatch.Text(relation, "source_fact_id"), CompositionBatch.Text(sourceOperation, "identity", "id"));
            Assert.Contains("PlaceOrderAsync", CompositionBatch.Text(sourceOperation, "symbol", "id"), StringComparison.Ordinal);
            Assert.Equal("outbound", CompositionBatch.Text(sourceOperation, "direction"));
            Assert.Equal("messaging", CompositionBatch.Text(sourceOperation, "protocol"));

            var targetOperation = CompositionBatch.ElementAt(
                shippingPackage,
                CompositionBatch.Text(relation, "target_artifact_key"),
                relation["target_ordinal"]!.GetValue<int>());
            Assert.Equal(CompositionBatch.Text(relation, "target_fact_id"), CompositionBatch.Text(targetOperation, "identity", "id"));
            Assert.Contains("HandleAsync", CompositionBatch.Text(targetOperation, "symbol", "id"), StringComparison.Ordinal);
            Assert.Contains("OrderPlacedEventHandler", CompositionBatch.Text(targetOperation, "symbol", "id"), StringComparison.Ordinal);
            Assert.Equal("inbound", CompositionBatch.Text(targetOperation, "direction"));
            Assert.Equal("messaging", CompositionBatch.Text(targetOperation, "protocol"));

            var outboundHttp = Assert.Single(
                CanonicalJson.Read<ArchitectureFactsShard>(ordersPackage["facts/architecture.json"]).BoundaryOperations,
                operation => operation.Identity.Id == CompositionBatch.Text(candidate, "source_fact_id"));
            Assert.Contains("RequestShippingAsync", outboundHttp.Symbol.Id, StringComparison.Ordinal);
            Assert.Equal("outbound", outboundHttp.Direction);
            Assert.Equal("http", outboundHttp.Protocol);
            Assert.Equal("POST", outboundHttp.HttpMethod);
            Assert.Equal("shipments", outboundHttp.Route?.Value);

            var inboundHttp = Assert.Single(
                CanonicalJson.Read<ArchitectureFactsShard>(shippingPackage["facts/architecture.json"]).BoundaryOperations,
                operation => operation.Identity.Id == CompositionBatch.Text(candidate, "target_fact_id"));
            Assert.Contains("CreateShipment", inboundHttp.Symbol.Id, StringComparison.Ordinal);
            Assert.Equal("inbound", inboundHttp.Direction);
            Assert.Equal("http", inboundHttp.Protocol);
            Assert.Equal("POST shipments", inboundHttp.ProtocolOperationKey?.Value);

            var sharedEntry = Required(Assert.Single(
                CompositionBatch.ReadArray(run.Files, "composition/shared-contracts.json")));
            Assert.Contains("OrderPlaced", CompositionBatch.Text(sharedEntry, "fact_id"), StringComparison.Ordinal);
            Assert.Contains("Acme.Shared.Contracts", CompositionBatch.Text(sharedEntry, "fact_id"), StringComparison.Ordinal);
            Assert.DoesNotContain("PaymentProcessed", sharedEntry.ToJsonString(), StringComparison.Ordinal);
            var owners = Assert.IsType<JsonArray>(sharedEntry["owners"]);
            Assert.Equal(2, owners.Count);
            Assert.Contains(owners, owner => OwnerMatches(owner, manifest, "Acme.Orders.slnx"));
            Assert.Contains(owners, owner => OwnerMatches(owner, manifest, "Acme.Shipping.slnx"));
            Assert.All(owners, owner =>
            {
                var node = Required(owner);
                var cited = CompositionBatch.ElementAt(
                    PackageFor(node, manifest, ordersPackage, shippingPackage),
                    CompositionBatch.Text(node, "artifact_key"),
                    node["ordinal"]!.GetValue<int>());
                Assert.Contains("Acme.Shared.Contracts", CompositionBatch.Text(cited, "proof", "value"), StringComparison.Ordinal);
                Assert.Contains("OrderPlaced", CompositionBatch.Text(cited, "proof", "value"), StringComparison.Ordinal);
            });

            Assert.DoesNotContain(
                run.Files.Keys,
                key => key.StartsWith("composition/", StringComparison.Ordinal)
                    && EncodingContains(run.Files[key], "PaymentProcessed"));
        }
        finally
        {
            TryDelete(output);
        }
    }

    private static IReadOnlyDictionary<string, byte[]> PackageNamed(CompositionBatch.BatchRun run, string solutionFileName)
    {
        var manifest = CanonicalJson.Read<BatchManifestEnvelope>(run.Files["batch-manifest.json"]);
        var entry = Assert.Single(manifest.Solutions, solution => solution.SolutionFileName == solutionFileName);
        var package = CompositionBatch.PackageFiles(run.Files, entry.PackageDirectory);
        Assert.NotEmpty(package);
        return package;
    }

    private static bool OwnerMatches(JsonNode? owner, BatchManifestEnvelope manifest, string solutionFileName)
    {
        var identity = CompositionBatch.Text(Required(owner), "solution_identity");
        return manifest.Solutions.Any(solution =>
            solution.SolutionFileName == solutionFileName
            && string.Equals(solution.Identity, identity, StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, byte[]> PackageFor(
        JsonNode owner,
        BatchManifestEnvelope manifest,
        IReadOnlyDictionary<string, byte[]> ordersPackage,
        IReadOnlyDictionary<string, byte[]> shippingPackage)
    {
        var identity = CompositionBatch.Text(owner, "solution_identity");
        var fileName = Assert.Single(manifest.Solutions, solution => solution.Identity == identity).SolutionFileName;
        return fileName == "Acme.Orders.slnx" ? ordersPackage : shippingPackage;
    }

    private static JsonNode Required(JsonNode? node)
    {
        Assert.NotNull(node);
        return node;
    }

    private static bool EncodingContains(byte[] bytes, string value) =>
        System.Text.Encoding.UTF8.GetString(bytes).Contains(value, StringComparison.Ordinal);

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
