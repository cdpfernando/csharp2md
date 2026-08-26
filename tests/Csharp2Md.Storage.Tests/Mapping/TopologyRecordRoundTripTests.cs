using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

[Collection(FilesystemStoreCollection.Name)]
public sealed class TopologyRecordRoundTripTests
{
    private const string SolutionKey = @"C:\src\Acme Orders.sln";

    [Fact]
    [Trait("Requirement", "CDC-53")]
    public void StageCommitRead_DeploymentUnitAndConfigurationBinding_PreserveIdentitiesAndFields()
    {
        var unit = OrdersUnit();
        var component = OrdersComponent();
        var binding = PaymentBinding(component);
        var snapshot = new FactualSnapshot(
            [unit, component, binding],
            [],
            [],
            [],
            [],
            []);

        using var output = TempOutputRoot.Create();
        var restored = CommitAndRead(output.DirectoryPath, snapshot);

        var restoredUnit = Assert.Single(restored.Facts.OfType<DeploymentUnit>());
        var restoredBinding = Assert.Single(restored.Facts.OfType<ConfigurationBinding>());
        Assert.Equal(unit, restoredUnit);
        Assert.Equal(binding, restoredBinding);
        Assert.Equal(unit.Reference, restoredUnit.Reference);
        Assert.Equal("Acme.Orders/Acme.Orders.csproj", restoredUnit.Name);
        Assert.Equal(binding.Reference, restoredBinding.Reference);
        Assert.Equal(component.Reference, restoredBinding.BoundFact);
        Assert.Equal("Services:PaymentService", restoredBinding.ConfigurationKey.Value);
        Assert.Equal(LiteralRole.ConfigurationKey, restoredBinding.ConfigurationKey.Role);

        var package = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var shardBytes = File.ReadAllBytes(Path.Combine(package, "facts", "configuration.json"));
        var shard = CanonicalJson.Read<ConfigurationFactsShard>(shardBytes);
        var shardBinding = Assert.Single(shard.ConfigurationBindings);
        Assert.Equal(binding.Reference.Id.Value, shardBinding.Identity.Id);
        Assert.Equal("Services:PaymentService", shard.ConfigurationBindings[0].ConfigurationKey.Value);

        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(package, "manifest.json")));
        var configurationEntry = Assert.Single(
            manifest.Artifacts,
            entry => entry.CanonicalKey == "facts/configuration");
        Assert.Equal(1, configurationEntry.Count);
        Assert.Equal("facts/configuration.json", configurationEntry.Path);
    }

    [Fact]
    [Trait("Requirement", "CDC-53")]
    public void StageCommitRead_BelongsToIncludedInConfiguredBy_PreserveKindSourceTargetAndEvidenceMethod()
    {
        var symbol = HostSymbol();
        var component = Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", [symbol.Reference]);
        var unit = OrdersUnit();
        var binding = PaymentBinding(component);
        var evidence = ValidEvidence();
        var variants = Variants();
        var emptyFacets = EmptyFacets();

        var belongs = ConfirmedRelation.Create(
            RelationKind.BelongsTo,
            symbol.Reference,
            component.Reference,
            emptyFacets,
            evidence,
            ClassifierIdentity.Create("csharp2md.classifier.component-topology", 1),
            variants,
            EvidenceMethod.Semantic,
            sourceFact: symbol,
            targetFact: component);
        var included = ConfirmedRelation.Create(
            RelationKind.IncludedIn,
            component.Reference,
            unit.Reference,
            emptyFacets,
            evidence,
            ClassifierIdentity.Create("csharp2md.classifier.component-topology", 1),
            variants,
            EvidenceMethod.Configured,
            sourceFact: component,
            targetFact: unit);
        var configured = ConfirmedRelation.Create(
            RelationKind.ConfiguredBy,
            component.Reference,
            binding.Reference,
            emptyFacets,
            evidence,
            ClassifierIdentity.Create("csharp2md.classifier.configuration", 1),
            variants,
            EvidenceMethod.Configured,
            sourceFact: component,
            targetFact: binding);

        using var output = TempOutputRoot.Create();
        var restored = CommitAndRead(
            output.DirectoryPath,
            new FactualSnapshot(
                [symbol, component, unit, binding],
                [],
                [belongs, included, configured],
                [],
                [],
                []));

        var restoredBelongs = Assert.Single(restored.ConfirmedRelations, relation => relation.Kind is RelationKind.BelongsTo);
        var restoredIncluded = Assert.Single(restored.ConfirmedRelations, relation => relation.Kind is RelationKind.IncludedIn);
        var restoredConfigured = Assert.Single(restored.ConfirmedRelations, relation => relation.Kind is RelationKind.ConfiguredBy);
        Assert.Equal(belongs, restoredBelongs);
        Assert.Equal(included, restoredIncluded);
        Assert.Equal(configured, restoredConfigured);
        Assert.Equal(symbol.Reference, restoredBelongs.Source);
        Assert.Equal(component.Reference, restoredBelongs.Target);
        Assert.Equal(component.Reference, restoredIncluded.Source);
        Assert.Equal(unit.Reference, restoredIncluded.Target);
        Assert.Equal(component.Reference, restoredConfigured.Source);
        Assert.Equal(binding.Reference, restoredConfigured.Target);

        var package = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        Assert.Equal("Semantic", ReadWireEvidenceMethod(package, "relations/confirmed/belongs-to.json"));
        Assert.Equal("Configured", ReadWireEvidenceMethod(package, "relations/confirmed/included-in.json"));
        Assert.Equal("Configured", ReadWireEvidenceMethod(package, "relations/confirmed/configured-by.json"));

        var shard = CanonicalJson.Read<ConfigurationFactsShard>(
            File.ReadAllBytes(Path.Combine(package, "facts", "configuration.json")));
        Assert.Single(shard.ConfigurationBindings);
        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(package, "manifest.json")));
        Assert.Equal(
            1,
            Assert.Single(manifest.Artifacts, entry => entry.CanonicalKey == "facts/configuration").Count);
    }

    private static FactualSnapshot CommitAndRead(string outputRoot, FactualSnapshot snapshot)
    {
        var store = new FilesystemTransactionalStore(outputRoot);
        var session = store.Open(SolutionKey);
        session.Stage(snapshot);
        session.Commit();
        return FactualPackageReader.Read(FilesystemTestPaths.ChildDirectory(outputRoot, SolutionKey)).Snapshot;
    }

    private static string ReadWireEvidenceMethod(string package, string relativePath)
    {
        var bytes = File.ReadAllBytes(Path.Combine(package, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var dto = Assert.Single(CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(bytes));
        return dto.EvidenceMethod;
    }

    private static DeploymentUnit OrdersUnit() =>
        DeploymentUnit.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static Component OrdersComponent() =>
        Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", [HostSymbol().Reference]);

    private static ConfigurationBinding PaymentBinding(Component component) =>
        ConfigurationBinding.Create(
            component.Reference,
            StructuralLiteral.Create(LiteralRole.ConfigurationKey, "Services:PaymentService", "configurationKey"));

    private static Symbol HostSymbol() =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Orders.Program",
                "Main",
                0,
                "global::System.Void"),
            AcmeProject,
            SymbolFacetSet.Create([SymbolFacet.Callable]));

    private static FacetBinding EmptyFacets() =>
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static ImmutableArray<AnalysisVariantId> Variants() =>
        [AnalysisVariantId.Create("net10.0", "Debug", [], "local")];

    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create(
        [
            new ObservationIdentity(Solution.Create(AcmeSolution).Reference, ObservationKind.Configuration, NormalizedPayload.Create([]), 1),
        ]);

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.Orders.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Orders/Acme.Orders.csproj");
}
