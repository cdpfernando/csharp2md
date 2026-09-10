using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

/// <summary>GCPC-061: every manifest entry's count and byte size match the artifact it cites, including a
/// projection artifact the plan never knew about.</summary>
public sealed class ManifestRealCardinalityTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void From_EveryManifestEntry_RealCountAndByteSizeMatchTheCitedArtifact()
    {
        var fragments = PublishWithOneProjectionArtifact(out _);
        var manifest = ReadManifest(fragments);
        var byKey = fragments
            .Where(static fragment => fragment.Role == ArtifactRole.Payload)
            .ToDictionary(static fragment => fragment.CanonicalKey, static fragment => fragment.Payload);

        Assert.NotEmpty(manifest.Artifacts);
        foreach (var entry in manifest.Artifacts)
        {
            Assert.True(byKey.TryGetValue(entry.Path, out var bytes), $"Missing cited artifact '{entry.Path}'.");
            Assert.Equal(bytes.Length, entry.ByteSize);

            // The taxonomy registry is one indivisible document, not a homogeneous record set -- summing
            // its many internal tables would not be "its own top-level entry count" in any meaningful
            // sense, so this generic re-derivation does not apply to it.
            if (entry.Path == PackagePublisher.RegistryKey)
            {
                Assert.Equal(1, entry.Count);
                continue;
            }

            Assert.Equal(IndependentTopLevelCount(bytes.AsSpan()), entry.Count);
        }
    }

    [Fact]
    public void From_ProjectionArtifactHoldingRecords_CarriesANonZeroCount()
    {
        var fragments = PublishWithOneProjectionArtifact(out var projectionKey);
        var manifest = ReadManifest(fragments);

        var entry = Assert.Single(manifest.Artifacts, candidate => candidate.Path == projectionKey);
        Assert.Equal(3, entry.Count);
        Assert.True(entry.ByteSize > 0);
    }

    private static ImmutableArray<StagedFragment> PublishWithOneProjectionArtifact(out string projectionKey)
    {
        projectionKey = "catalogs/sample.json";
        var document = DomainMapper.ToWire(FilledSnapshot(), Context);
        var plan = LayoutPlanner.Plan(document, int.MaxValue);
        var projectionPayload = CanonicalJson.Write((JsonNode)new JsonArray(
            new JsonObject { ["id"] = "p-1" },
            new JsonObject { ["id"] = "p-2" },
            new JsonObject { ["id"] = "p-3" }));
        var projections = ImmutableArray.Create(
            new StagedFragment(ArtifactRole.Payload, projectionKey, projectionPayload));

        return PackagePublisher.ToPublicationOrder(document, plan, projections);
    }

    private static ManifestEnvelope ReadManifest(ImmutableArray<StagedFragment> fragments)
    {
        var manifestFragment = Assert.Single(fragments, f => f.CanonicalKey == PackagePublisher.ManifestKey);
        return CanonicalJson.Read<ManifestEnvelope>(manifestFragment.Payload.AsSpan());
    }

    /// <summary>Independently re-derives "real top-level count" from raw bytes, without going through
    /// <c>ManifestBuilder</c>'s own implementation.</summary>
    private static int IndependentTopLevelCount(ReadOnlySpan<byte> bytes)
    {
        var node = JsonNode.Parse(bytes);
        switch (node)
        {
            case JsonArray array:
                return array.Count;
            case JsonObject obj:
                var sum = 0;
                var sawArray = false;
                foreach (var property in obj)
                {
                    if (property.Value is JsonArray family)
                    {
                        sawArray = true;
                        sum += family.Count;
                    }
                }

                return sawArray ? sum : 1;
            default:
                return 1;
        }
    }

    private static FactualSnapshot FilledSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var otherProjectId = ProjectId.Create(solutionId, "src/Acme.Orders/Acme.Orders.csproj");
        var solution = Solution.Create(solutionId);
        var project = Project.Create(projectId);
        var other = Project.Create(otherProjectId);
        var component = Component.Create(solutionId, "Payments.Api", []);
        var binding = ConfigurationBinding.Create(
            project.Reference,
            StructuralLiteral.Create(LiteralRole.ConfigurationKey, "ConnectionStrings:Orders", "configurationKey"));
        var firstRelation = Contains(solution.Reference, project.Reference, occurrenceOrdinal: 1);
        var secondRelation = Contains(solution.Reference, other.Reference, occurrenceOrdinal: 2);
        return new FactualSnapshot(
            [solution, project, other, component, binding],
            [],
            [firstRelation, secondRelation],
            [],
            [],
            []);
    }

    private static ConfirmedRelation Contains(FactReference source, FactReference target, int occurrenceOrdinal) =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            source,
            target,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([
                new ObservationIdentity(
                    source,
                    ObservationKind.Invocation,
                    NormalizedPayload.Create([]),
                    occurrenceOrdinal)]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
}
