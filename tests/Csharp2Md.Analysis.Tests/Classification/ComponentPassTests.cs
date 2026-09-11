using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Classification.Topology;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ComponentPassTests
{
    [Fact]
    [Trait("Requirement", "CDC-55")]
    public void Name_IsComponents()
    {
        Assert.Equal("Components", new ComponentPass().Name);
        Assert.Equal("csharp2md.classifier.component-topology", TopologyEmitter.Identity.Id);
        Assert.Equal(1, TopologyEmitter.Identity.Version);
    }

    [Fact]
    [Trait("Requirement", "CDC-55")]
    public void Execute_IsBuilderThenEmitterWithNoClassificationLogicOfItsOwn()
    {
        var source = File.ReadAllText(ComponentPassPath());

        Assert.Contains("TopologyModelBuilder.Build(context)", source, StringComparison.Ordinal);
        Assert.Contains("TopologyEmitter.Emit(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CandidateKinds", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryLogicalPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Component.Create", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DeploymentUnit.Create", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CDC-18")]
    public void Execute_EmptyLedger_ReturnsZeroWithoutThrowing()
    {
        var pipeline = Arrange();
        var context = new ClassifierContext(pipeline);

        var result = new ComponentPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Equal(0, result.RelationCount);
        Assert.Equal(0, result.CandidateCount);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<DeploymentUnit>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
    }

    [Fact]
    [Trait("Requirement", "CDC-18")]
    public void Execute_NoProjectMetadata_EmitsNoTopology()
    {
        var pipeline = Arrange();
        var app = TopologyModelBuilderTests.AddProject(pipeline, "App/App.csproj");
        var host = AddSymbol(pipeline, app, "Host");
        AddObservation(pipeline, host, ObservationKind.Invocation, 1);
        var context = new ClassifierContext(pipeline);

        var result = new ComponentPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Equal(0, result.RelationCount);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<DeploymentUnit>());
    }

    [Fact]
    [Trait("Requirement", "CDC-09")]
    [Trait("Requirement", "CDC-19")]
    public void Execute_Application_EmitsDeployableComponentAndDeploymentUnit()
    {
        var pipeline = Arrange();
        var app = TopologyModelBuilderTests.AddProject(pipeline, "App/App.csproj");
        TopologyModelBuilderTests.AddOutputKind(pipeline, app, "application");
        var host = AddSymbol(pipeline, app, "Host");
        AddObservation(pipeline, host, ObservationKind.Invocation, 1);
        var context = new ClassifierContext(pipeline);

        var result = new ComponentPass().Execute(context, CancellationToken.None);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        var component = Assert.Single(snapshot.Facts.OfType<Component>());
        var unit = Assert.Single(snapshot.Facts.OfType<DeploymentUnit>());
        Assert.Equal("App/App.csproj", component.Name);
        Assert.Equal("App/App.csproj", unit.Name);
        Assert.Equal(2, result.FactCount);
        Assert.Equal(2, result.RelationCount);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Equal(host.Reference, Assert.Single(component.Owners.ToArray()));
        Assert.Contains(
            snapshot.ConfirmedRelations,
            relation => relation.Kind is RelationKind.BelongsTo
                && relation.Source.Equals(host.Reference)
                && relation.Target.Equals(component.Reference));
        Assert.Contains(
            snapshot.ConfirmedRelations,
            relation => relation.Kind is RelationKind.IncludedIn
                && relation.Source.Equals(component.Reference)
                && relation.Target.Equals(unit.Reference));
    }

    [Fact]
    [Trait("Requirement", "CDC-10")]
    public void Execute_PrivateUseLibrary_GroupsIntoTheApplicationComponent()
    {
        var pipeline = Arrange();
        var app = TopologyModelBuilderTests.AddProject(pipeline, "App/App.csproj");
        var lib = TopologyModelBuilderTests.AddProject(pipeline, "Lib/Lib.csproj");
        TopologyModelBuilderTests.AddOutputKind(pipeline, app, "application");
        TopologyModelBuilderTests.AddOutputKind(pipeline, lib, "library");
        TopologyModelBuilderTests.AddReference(pipeline, app, "Lib/Lib.csproj", ordinal: 2);
        var host = AddSymbol(pipeline, app, "Host");
        var dto = AddSymbol(pipeline, lib, "Dto");
        AddObservation(pipeline, host, ObservationKind.Invocation, 1);
        AddObservation(pipeline, dto, ObservationKind.Invocation, 1);
        var context = new ClassifierContext(pipeline);

        new ComponentPass().Execute(context, CancellationToken.None);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        var component = Assert.Single(snapshot.Facts.OfType<Component>());
        Assert.Equal("App/App.csproj", component.Name);
        Assert.DoesNotContain(
            snapshot.Facts.OfType<Component>(),
            candidate => candidate.Name == "Lib/Lib.csproj");
        Assert.Contains(host.Reference, component.Owners.ToArray());
        Assert.Contains(dto.Reference, component.Owners.ToArray());
        Assert.Single(snapshot.Facts.OfType<DeploymentUnit>());
    }

    [Fact]
    [Trait("Requirement", "CDC-11")]
    [Trait("Requirement", "CDC-21")]
    public void Execute_SharedLibrary_EmitsOwnComponentAndOneIncludedInPerApplication()
    {
        var pipeline = Arrange();
        var orders = TopologyModelBuilderTests.AddProject(pipeline, "Orders/Orders.csproj");
        var worker = TopologyModelBuilderTests.AddProject(pipeline, "Worker/Worker.csproj");
        var contracts = TopologyModelBuilderTests.AddProject(pipeline, "Contracts/Contracts.csproj");
        TopologyModelBuilderTests.AddOutputKind(pipeline, orders, "application");
        TopologyModelBuilderTests.AddOutputKind(pipeline, worker, "application");
        TopologyModelBuilderTests.AddOutputKind(pipeline, contracts, "library");
        TopologyModelBuilderTests.AddReference(pipeline, orders, "Contracts/Contracts.csproj", ordinal: 2);
        TopologyModelBuilderTests.AddReference(pipeline, worker, "Contracts/Contracts.csproj", ordinal: 2);
        AddObservation(pipeline, AddSymbol(pipeline, contracts, "Dto"), ObservationKind.Invocation, 1);
        var context = new ClassifierContext(pipeline);

        new ComponentPass().Execute(context, CancellationToken.None);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        Assert.Equal(
            new[] { "Contracts/Contracts.csproj", "Orders/Orders.csproj", "Worker/Worker.csproj" },
            snapshot.Facts.OfType<Component>().Select(component => component.Name).ToArray());
        Assert.Equal(
            new[] { "Orders/Orders.csproj", "Worker/Worker.csproj" },
            snapshot.Facts.OfType<DeploymentUnit>().Select(unit => unit.Name).ToArray());
        var shared = Assert.Single(
            snapshot.Facts.OfType<Component>(),
            component => component.Name == "Contracts/Contracts.csproj");
        Assert.Equal(
            2,
            snapshot.ConfirmedRelations.Count(relation =>
                relation.Kind is RelationKind.IncludedIn && relation.Source.Equals(shared.Reference)));
    }

    [Fact]
    [Trait("Requirement", "CDC-12")]
    [Trait("Requirement", "CDC-22")]
    public void Execute_UnreachedLibrary_EmitsUnresolvedIncludedIn()
    {
        var pipeline = Arrange();
        var app = TopologyModelBuilderTests.AddProject(pipeline, "App/App.csproj");
        var isolated = TopologyModelBuilderTests.AddProject(pipeline, "Isolated/Isolated.csproj");
        TopologyModelBuilderTests.AddOutputKind(pipeline, app, "application");
        TopologyModelBuilderTests.AddOutputKind(pipeline, isolated, "library");
        var context = new ClassifierContext(pipeline);

        var result = new ComponentPass().Execute(context, CancellationToken.None);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        Assert.Equal(
            new[] { "App/App.csproj", "Isolated/Isolated.csproj" },
            snapshot.Facts.OfType<Component>().Select(component => component.Name).ToArray());
        Assert.Equal("App/App.csproj", Assert.Single(snapshot.Facts.OfType<DeploymentUnit>()).Name);
        var isolatedComponent = Assert.Single(
            snapshot.Facts.OfType<Component>(),
            component => component.Name == "Isolated/Isolated.csproj");
        var unresolved = Assert.Single(snapshot.Unresolved);
        Assert.Equal(RelationKind.IncludedIn, unresolved.Kind);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
        Assert.Equal(isolatedComponent.Reference, unresolved.Source);
        Assert.Equal(1, result.UnresolvedCount);
        Assert.DoesNotContain(
            snapshot.ConfirmedRelations,
            relation => relation.Kind is RelationKind.IncludedIn && relation.Source.Equals(isolatedComponent.Reference));
    }

    [Fact]
    [Trait("Requirement", "CDC-23")]
    [Trait("Requirement", "CDC-24")]
    public void Execute_NoApplication_EmitsNoDeploymentUnit()
    {
        var pipeline = Arrange();
        var one = TopologyModelBuilderTests.AddProject(pipeline, "One/One.csproj");
        var two = TopologyModelBuilderTests.AddProject(pipeline, "Two/Two.csproj");
        TopologyModelBuilderTests.AddOutputKind(pipeline, one, "library");
        TopologyModelBuilderTests.AddOutputKind(pipeline, two, "library");
        var context = new ClassifierContext(pipeline);

        new ComponentPass().Execute(context, CancellationToken.None);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        Assert.Equal(
            new[] { "One/One.csproj", "Two/Two.csproj" },
            snapshot.Facts.OfType<Component>().Select(component => component.Name).ToArray());
        Assert.Empty(snapshot.Facts.OfType<DeploymentUnit>());
        Assert.Equal(2, snapshot.Unresolved.Length);
        Assert.Empty(snapshot.ConfirmedRelations.Where(relation => relation.Kind is RelationKind.IncludedIn));
    }

    [Fact]
    [Trait("Requirement", "CDC-14")]
    public void Execute_Owners_AreAnySymbolWithAnObservationNotFourCandidateKinds()
    {
        var pipeline = Arrange();
        var app = TopologyModelBuilderTests.AddProject(pipeline, "App/App.csproj");
        TopologyModelBuilderTests.AddOutputKind(pipeline, app, "application");
        var invoked = AddSymbol(pipeline, app, "Invoked");
        var unused = AddSymbol(pipeline, app, "Unused");
        AddObservation(pipeline, invoked, ObservationKind.Invocation, 1);
        var context = new ClassifierContext(pipeline);

        new ComponentPass().Execute(context, CancellationToken.None);

        var component = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>());
        Assert.Equal(invoked.Reference, Assert.Single(component.Owners.ToArray()));
        Assert.DoesNotContain(unused.Reference, component.Owners.ToArray());
    }

    [Fact]
    [Trait("Requirement", "CDC-09")]
    [Trait("Requirement", "CDC-11")]
    [Trait("Requirement", "CDC-19")]
    [Trait("Requirement", "CDC-21")]
    public async Task AnalyzeAsync_AcmeOrders_EmitsThreeComponentsAndTwoDeployments()
    {
        var (_, publication) = await AnalyzeAsync(AcmeOrdersSolutionPath());
        var architecture = ReadShard<ArchitectureFactsShard>(publication, "facts/architecture.json");
        var included = ReadRelations(publication, "relations/confirmed/included-in.json");

        Assert.Equal(
            new[]
            {
                "Acme.Orders/Acme.Orders.csproj",
                "Acme.Orders.Worker/Acme.Orders.Worker.csproj",
                "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj",
            }.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            architecture.Components.Select(component => component.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal(
            new[]
            {
                "Acme.Orders/Acme.Orders.csproj",
                "Acme.Orders.Worker/Acme.Orders.Worker.csproj",
            }.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            architecture.DeploymentUnits.Select(unit => unit.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(
            architecture.Components,
            component => component.Name == "Acme.Broken/Acme.Broken.csproj");

        var shared = Assert.Single(
            architecture.Components,
            component => component.Name == "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
        var sharedInclusions = included
            .Where(relation => relation.Kind == "included-in" && relation.Source.Id == shared.Identity.Id)
            .ToArray();
        Assert.Equal(2, sharedInclusions.Length);
        Assert.Equal(
            architecture.DeploymentUnits.Select(unit => unit.Identity.Id).OrderBy(id => id, StringComparer.Ordinal),
            sharedInclusions.Select(relation => relation.Target.Id).OrderBy(id => id, StringComparer.Ordinal));

        AssertOwnDeploymentOnly(architecture, included, "Acme.Orders/Acme.Orders.csproj");
        AssertOwnDeploymentOnly(architecture, included, "Acme.Orders.Worker/Acme.Orders.Worker.csproj");
    }

    [Fact]
    [Trait("Requirement", "CDC-10")]
    [Trait("Requirement", "CDC-15")]
    public async Task AnalyzeAsync_AcmePayments_EmitsOneComponentAndSharedContractsBelongsToIt()
    {
        var (_, publication) = await AnalyzeAsync(AcmePaymentsSolutionPath());
        var architecture = ReadShard<ArchitectureFactsShard>(publication, "facts/architecture.json");
        var structural = ReadShard<StructuralFactsShard>(publication, "facts/structural.json");
        var belongs = ReadRelations(publication, "relations/confirmed/belongs-to.json");

        var component = Assert.Single(architecture.Components);
        Assert.Equal("Acme.Payments/Acme.Payments.csproj", component.Name);
        Assert.Equal("Acme.Payments/Acme.Payments.csproj", Assert.Single(architecture.DeploymentUnits).Name);
        Assert.DoesNotContain(
            architecture.Components,
            candidate => candidate.Name == "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");

        var contractsProject = Assert.Single(
            structural.Projects,
            project => Uri.UnescapeDataString(project.ProjectId)
                .Contains("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", StringComparison.Ordinal));
        var contractsSymbolIds = structural.Symbols
            .Where(symbol => string.Equals(symbol.OwningProject, contractsProject.ProjectId, StringComparison.Ordinal))
            .Select(symbol => symbol.Identity.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(
            belongs,
            relation => relation.Kind == "belongs-to"
                && relation.Target.Id == component.Identity.Id
                && contractsSymbolIds.Contains(relation.Source.Id));
    }

    private static void AssertOwnDeploymentOnly(
        ArchitectureFactsShard architecture,
        ImmutableArray<ConfirmedRelationDto> included,
        string name)
    {
        var component = Assert.Single(architecture.Components, candidate => candidate.Name == name);
        var unit = Assert.Single(architecture.DeploymentUnits, candidate => candidate.Name == name);
        var targets = included
            .Where(relation => relation.Kind == "included-in" && relation.Source.Id == component.Identity.Id)
            .Select(relation => relation.Target.Id)
            .ToArray();
        Assert.Equal(new[] { unit.Identity.Id }, targets);
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeAsync(
        string solutionPath)
    {
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static T ReadShard<T>(CommittedPublication publication, string canonicalKey) =>
        ShardedFactsReader.Read<T>(publication.ArtifactsInPublicationOrder, canonicalKey);

    /// <summary>
    /// T52 made the derived ~32 KiB ceiling the live default, so this flat record-array family may now
    /// legitimately be sharded into "&lt;stem&gt;.&lt;bucket&gt;.json" artifacts instead of staying one
    /// file at its base key -- this merges every shard back into one array, matching what
    /// <c>FactualPackageReader.ReadShardedArray</c> does for a real reader.
    /// </summary>
    private static ImmutableArray<ConfirmedRelationDto> ReadRelations(
        CommittedPublication publication,
        string canonicalKey)
    {
        var stem = canonicalKey.EndsWith(".json", StringComparison.Ordinal)
            ? canonicalKey[..^".json".Length]
            : canonicalKey;
        var shardKeys = publication.ArtifactsInPublicationOrder
            .Select(static artifact => artifact.CanonicalKey)
            .Where(key => key == canonicalKey
                || (key.StartsWith(stem + ".", StringComparison.Ordinal) && key.EndsWith(".json", StringComparison.Ordinal)))
            .OrderBy(static key => key, StringComparer.Ordinal);

        var records = ImmutableArray.CreateBuilder<ConfirmedRelationDto>();
        foreach (var key in shardKeys)
        {
            var fragment = publication.ArtifactsInPublicationOrder.Single(artifact => artifact.CanonicalKey == key);
            records.AddRange(CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(fragment.Payload.AsSpan()));
        }

        return records.ToImmutable();
    }

    private static string AcmeOrdersSolutionPath() => FixtureSolution("Acme.Orders", "Acme.Orders.slnx");

    private static string AcmePaymentsSolutionPath() => FixtureSolution("Acme.Payments", "Acme.Payments.slnx");

    private static string FixtureSolution(string folder, string file)
    {
        var path = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution", folder, file);
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static PipelineContext Arrange()
    {
        var pipeline = TopologyModelBuilderTests.Arrange();
        pipeline.AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")];
        return pipeline;
    }

    private static Symbol AddSymbol(PipelineContext pipeline, Project project, string metadata)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("namedtype", "global::App", metadata, 0, "global::App." + metadata),
            project.Id,
            SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static Observation AddObservation(
        PipelineContext pipeline,
        Symbol owner,
        ObservationKind kind,
        int ordinal)
    {
        var observation = Observation.Create(
            owner.Reference,
            kind,
            NormalizedPayload.Create([]),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "App/App.csproj", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
        pipeline.Accumulator.AddObservation(observation);
        return observation;
    }

    private static string ComponentPassPath() =>
        Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Classification",
            "Passes",
            "ComponentPass.cs");
}
