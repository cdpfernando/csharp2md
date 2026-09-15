using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Retrieval;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Retrieval;

/// <summary>
/// T47 (GCPC-052..GCPC-054): every scenario `retrieval.md` documents is executed and measured, a scenario
/// with no instance in the input is reported not exercised rather than passed, a documented path that
/// does not resolve fails the run naming the offender, and both artifact sources -- the fragments an
/// `analyze` run just staged, and the same package re-read from disk -- report identically for the same
/// content.
/// </summary>
[Collection(FilesystemStoreCollection.Name)]
public sealed class RetrievalScenarioRunnerTests
{
    private const string SolutionKey = @"C:\src\Acme Orders.sln";

    [Fact]
    [Trait("Requirement", "GCPC-052")]
    [Trait("Requirement", "GCPC-053")]
    public void Run_FullyPopulatedPackage_EveryDocumentedScenarioIsExercisedAndReachesItsEndpointWithinBudget()
    {
        var artifacts = Commit(new InMemoryTransactionalStore(new PackageProjector()), FullSnapshot());

        var report = RetrievalScenarioRunner.Run(new StagedFragmentArtifactSource(artifacts));

        Assert.Equal(11, report.Scenarios.Length);
        Assert.True(report.Passed);
        Assert.All(report.Scenarios, scenario =>
        {
            Assert.True(scenario.Exercised, scenario.Name);
            Assert.True(scenario.Reached, scenario.Name + ": " + scenario.FailureReason);
            Assert.Null(scenario.FailureReason);
            Assert.True(scenario.FilesRead > 0, scenario.Name);
            Assert.True(scenario.Bytes > 0, scenario.Name);
            Assert.True(scenario.RelevantFacts > 0, scenario.Name);
            Assert.True(scenario.WithinBudget, scenario.Name);
        });
        Assert.Contains(report.Scenarios, static scenario => scenario.Name == "locate-an-identity");
        foreach (var kind in new[]
                 {
                     "executes", "implements-operation", "invokes", "uses-contract",
                     "accesses-data", "operates-on", "targets",
                 })
        {
            Assert.Contains(report.Scenarios, scenario => scenario.Name == "relation:" + kind);
        }

        Assert.Contains(report.Scenarios, static scenario => scenario.Name == "disposition:candidate link");
        Assert.Contains(report.Scenarios, static scenario => scenario.Name == "disposition:unresolved record");
        Assert.Contains(report.Scenarios, static scenario => scenario.Name == "disposition:open frontier");
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    [Trait("Requirement", "GCPC-053")]
    public void Run_MinimalPackage_ScenarioWithNoInstanceIsReportedNotExercisedNeverPassed()
    {
        var symbol = Callable("Run");
        var component = Component.Create(SolutionId, "Orders.Api", []);
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var executes = ConfirmedRelation.Create(
            RelationKind.Executes, entry.Reference, symbol.Reference, EmptyFacets(), Evidence(entry.Reference),
            Classifier("executes"), Variant(), EvidenceMethod.Semantic, targetFact: symbol);
        var artifacts = Commit(
            new InMemoryTransactionalStore(new PackageProjector()),
            new FactualSnapshot([component, symbol, entry], [], [executes], [], [], []));

        var report = RetrievalScenarioRunner.Run(new StagedFragmentArtifactSource(artifacts));

        var invokesScenario = Assert.Single(report.Scenarios, static scenario => scenario.Name == "relation:invokes");
        Assert.False(invokesScenario.Exercised);
        Assert.False(invokesScenario.Reached);
        Assert.Null(invokesScenario.FailureReason);
        Assert.Equal(0, invokesScenario.FilesRead);

        var candidateScenario = Assert.Single(
            report.Scenarios, static scenario => scenario.Name == "disposition:candidate link");
        Assert.False(candidateScenario.Exercised);
        Assert.False(candidateScenario.Reached);

        var executesScenario = Assert.Single(report.Scenarios, static scenario => scenario.Name == "relation:executes");
        Assert.True(executesScenario.Exercised);
        Assert.True(executesScenario.Reached);

        Assert.True(report.Passed);
    }

    [Fact]
    [Trait("Requirement", "GCPC-054")]
    public void Run_BothArtifactSources_ProduceTheSameReportForTheSamePackage()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, new PackageProjector());
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FullSnapshot());
        var publication = session.Commit();
        var packageDirectory = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);

        var fromStaged = RetrievalScenarioRunner.Run(
            new StagedFragmentArtifactSource(publication.ArtifactsInPublicationOrder));
        var fromDisk = RetrievalScenarioRunner.Run(new PackageDirectoryArtifactSource(packageDirectory));

        Assert.Equal(fromStaged.Scenarios.Length, fromDisk.Scenarios.Length);
        Assert.Equal(
            fromStaged.Scenarios.OrderBy(static s => s.Name, StringComparer.Ordinal).ToArray(),
            fromDisk.Scenarios.OrderBy(static s => s.Name, StringComparer.Ordinal).ToArray());
        Assert.Equal(fromStaged.Passed, fromDisk.Passed);
    }

    [Fact]
    [Trait("Requirement", "GCPC-054")]
    public void Run_ADocumentedPathThatDoesNotResolve_FailsTheRunNamingTheOffender()
    {
        var artifacts = Commit(new InMemoryTransactionalStore(new PackageProjector()), FullSnapshot());
        var broken = new BrokenArtifactSource(
            new StagedFragmentArtifactSource(artifacts), "relations/confirmed/executes.json");

        var report = RetrievalScenarioRunner.Run(broken);

        var executesScenario = Assert.Single(report.Scenarios, static scenario => scenario.Name == "relation:executes");
        Assert.True(executesScenario.Exercised);
        Assert.False(executesScenario.Reached);
        Assert.Contains("relations/confirmed/executes.json", executesScenario.FailureReason, StringComparison.Ordinal);
        Assert.False(report.Passed);
    }

    [Fact]
    [Trait("Requirement", "GCPC-054")]
    public void Run_NoRetrievalGuidePublished_ThrowsNamingTheMissingGuide()
    {
        var exception = Assert.Throws<PublicationRejectedException>(
            static () => RetrievalScenarioRunner.Run(new StagedFragmentArtifactSource([])));

        Assert.Equal("retrieval-guide-missing", exception.Gate);
        Assert.Equal("retrieval.md", exception.Detail);
    }

    [Fact]
    [Trait("Requirement", "GCPC-052")]
    public void ToMeasurementRecords_RoundTripsThroughTheMeasurementsEnvelope()
    {
        var artifacts = Commit(new InMemoryTransactionalStore(new PackageProjector()), FullSnapshot());
        var report = RetrievalScenarioRunner.Run(new StagedFragmentArtifactSource(artifacts));

        var envelope = new MeasurementsEnvelope(report.ToMeasurementRecords());
        var roundTripped = CanonicalJson.Read<MeasurementsEnvelope>(CanonicalJson.Write(envelope).AsSpan());

        Assert.Equal(report.Scenarios.Length, roundTripped.Records.Length);
        var executesRecord = Assert.Single(
            roundTripped.Records, static record => record.Name == "retrieval-scenario:relation:executes");
        var executesScenario = Assert.Single(report.Scenarios, static scenario => scenario.Name == "relation:executes");
        Assert.Equal(executesScenario.Exercised, executesRecord.Exercised);
        Assert.Equal(executesScenario.Reached, executesRecord.Reached);
        Assert.Equal(executesScenario.Bytes, executesRecord.Bytes);
        Assert.Equal(executesScenario.Tokens, executesRecord.Tokens);
        Assert.Equal(executesScenario.FilesRead, executesRecord.FilesRead);
        Assert.Equal(executesScenario.RelevantFacts, executesRecord.RelevantFacts);
    }

    [Fact]
    [Trait("Requirement", "GCPC-054")]
    public void PackageDirectoryArtifactSource_MissingManifest_ThrowsBeforeAnyScenarioRuns()
    {
        using var output = TempOutputRoot.Create();
        Directory.CreateDirectory(output.DirectoryPath);

        Assert.Throws<PublicationRejectedException>(() => new PackageDirectoryArtifactSource(output.DirectoryPath));
    }

    private static ImmutableArray<StagedFragment> Commit(InMemoryTransactionalStore store, FactualSnapshot snapshot)
    {
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(snapshot);
        return session.Commit().ArtifactsInPublicationOrder;
    }

    private static readonly WorkspaceIdentity Workspace = WorkspaceIdentity.Create("acme");
    private static readonly SolutionId SolutionId = SolutionId.Create(Workspace, "src/Acme.sln");
    private static readonly ProjectId ProjectId = ProjectId.Create(SolutionId, "src/Acme.Orders/Acme.Orders.csproj");

    private static Symbol Callable(string name) =>
        Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Orders.Host", name, 0, "global::System.Void"),
            ProjectId,
            SymbolFacetSet.Create([SymbolFacet.Callable]));

    /// <summary>
    /// One instance of every catalog family, every one of the seven confirmed relation kinds, and all
    /// three unproven proof states -- so every scenario T46's retrieval guide documents has something real
    /// to walk.
    /// </summary>
    private static FactualSnapshot FullSnapshot()
    {
        var solution = Solution.Create(SolutionId);
        var project = Project.Create(ProjectId);
        var component = Component.Create(SolutionId, "Orders.Api", []);
        var unit = DeploymentUnit.Create(SolutionId, "Orders.Container");
        var externalSystem = ExternalSystem.Create(
            SolutionId, StructuralLiteral.Create(LiteralRole.ClientName, "Payments.External", "name"));

        var runSymbol = Callable("Run");
        var calledSymbol = Callable("Called");
        var entry = EntryPoint.Create(runSymbol.Reference, component.Reference);
        var operation = BoundaryOperation.Create(
            runSymbol.Reference,
            component.Reference,
            BoundaryDirection.Inbound,
            protocol: BoundaryProtocol.Http,
            httpMethod: "GET",
            route: StructuralLiteral.Create(LiteralRole.Route, "/orders", "route"),
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "GET /orders", "protocolOperationKey"));
        var contract = Contract.Create(StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof"));
        var store = DataStore.Create(
            DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.SchemaName, "OrdersDb", "name"));
        var dataObject = DataObject.Create(
            store.Reference,
            DataObjectForm.Table,
            StructuralLiteral.Create(LiteralRole.SchemaName, "dbo", "schemaName"),
            StructuralLiteral.Create(LiteralRole.TableName, "orders", "tableName"),
            MappingStateKind.ExplicitConfirmation);
        var dataOperation = DataOperation.Create(dataObject.Reference, DataOperationKind.Read, MappingStateKind.ExplicitConfirmation);

        var executes = ConfirmedRelation.Create(
            RelationKind.Executes, entry.Reference, runSymbol.Reference, EmptyFacets(), Evidence(entry.Reference),
            Classifier("executes"), Variant(), EvidenceMethod.Semantic, targetFact: runSymbol);
        var invokes = ConfirmedRelation.Create(
            RelationKind.Invokes, runSymbol.Reference, calledSymbol.Reference, EmptyFacets(), Evidence(runSymbol.Reference),
            Classifier("invokes"), Variant(), EvidenceMethod.Semantic, sourceFact: runSymbol, targetFact: calledSymbol);
        var implementsOperation = ConfirmedRelation.Create(
            RelationKind.ImplementsOperation, runSymbol.Reference, operation.Reference, EmptyFacets(), Evidence(runSymbol.Reference),
            Classifier("implements-operation"), Variant(), EvidenceMethod.Semantic, sourceFact: runSymbol);
        var targets = ConfirmedRelation.Create(
            RelationKind.Targets, operation.Reference, externalSystem.Reference, EmptyFacets(), Evidence(operation.Reference),
            Classifier("targets"), Variant(), EvidenceMethod.Configured, targetFact: externalSystem);
        var usesContract = ConfirmedRelation.Create(
            RelationKind.UsesContract, operation.Reference, contract.Reference, PayloadRoleFacets("request"), Evidence(operation.Reference),
            Classifier("uses-contract"), Variant(), EvidenceMethod.Semantic);
        var accessesData = ConfirmedRelation.Create(
            RelationKind.AccessesData, runSymbol.Reference, dataOperation.Reference, EmptyFacets(), Evidence(runSymbol.Reference),
            Classifier("accesses-data"), Variant(), EvidenceMethod.Semantic, sourceFact: runSymbol, targetFact: dataOperation);
        var operatesOn = ConfirmedRelation.Create(
            RelationKind.OperatesOn, dataOperation.Reference, dataObject.Reference, EmptyFacets(), Evidence(dataOperation.Reference),
            Classifier("operates-on"), Variant(), EvidenceMethod.Semantic, sourceFact: dataOperation, targetFact: dataObject);

        var candidateOwner = component.Reference;
        var candidate = CandidateLink.Create(RelationKind.Contains, candidateOwner, candidateOwner, Evidence(candidateOwner));
        var unresolved = UnresolvedRecord.Create(RelationKind.Invokes, candidateOwner, UnresolvedCause.NoCandidateFound, Evidence(candidateOwner));
        var frontier = OpenFrontier.Create(
            new ObservationIdentity(candidateOwner, ObservationKind.Invocation, NormalizedPayload.Create([]), 1),
            FrontierCause.FurtherContinuationObserved);

        return new FactualSnapshot(
            [solution, project, component, unit, externalSystem, runSymbol, calledSymbol, entry, operation, contract, store, dataObject, dataOperation],
            [],
            [executes, invokes, implementsOperation, targets, usesContract, accessesData, operatesOn],
            [candidate],
            [unresolved],
            [frontier]);
    }

    private static FacetBinding EmptyFacets() => FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static FacetBinding PayloadRoleFacets(string payloadRole)
    {
        var axes = TaxonomyTables.Default.FacetAxes.Add(
            new FacetAxisDescriptor("payload-role", TaxonomyTables.Default.PayloadRoles));
        return FacetBinding.Create(axes, ["payload-role"], [new FacetBindingEntry("payload-role", payloadRole)]);
    }

    private static ClassifierIdentity Classifier(string name) => ClassifierIdentity.Create("csharp2md.test." + name, 1);

    private static ImmutableArray<AnalysisVariantId> Variant() => [AnalysisVariantId.Create("net10.0", "Release", [], "ci")];

    private static EvidenceChain Evidence(FactReference owner, int occurrenceOrdinal = 1) =>
        EvidenceChain.Create(
        [
            new ObservationIdentity(owner, ObservationKind.Invocation, NormalizedPayload.Create([]), occurrenceOrdinal),
        ]);

    /// <summary>Delegates every read except one deliberately broken key, to prove GCPC-054's failure path.</summary>
    private sealed class BrokenArtifactSource : IArtifactSource
    {
        private readonly IArtifactSource _inner;
        private readonly string _brokenKey;

        public BrokenArtifactSource(IArtifactSource inner, string brokenKey)
        {
            _inner = inner;
            _brokenKey = brokenKey;
        }

        public bool TryRead(string artifactKey, out ImmutableArray<byte> bytes)
        {
            if (string.Equals(artifactKey, _brokenKey, StringComparison.Ordinal))
            {
                bytes = default;
                return false;
            }

            return _inner.TryRead(artifactKey, out bytes);
        }
    }
}
