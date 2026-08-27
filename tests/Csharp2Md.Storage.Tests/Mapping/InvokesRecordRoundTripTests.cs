using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

[Collection(FilesystemStoreCollection.Name)]
public sealed class InvokesRecordRoundTripTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "CLLF-07")]
    public void StageCommitRead_InvokesCandidateLink_PreservesKindSourceAndProposedTarget()
    {
        var source = CallerSymbol();
        var target = ConcreteOverrideSymbol();
        var link = CandidateLink.Create(
            RelationKind.Invokes,
            source.Reference,
            target.Reference,
            ValidEvidence());
        var snapshot = new FactualSnapshot([source, target], [], [], [link], [], []);

        using var output = TempOutputRoot.Create();
        var restored = CommitAndRead(output.DirectoryPath, snapshot);

        var actual = Assert.Single(restored.Candidates.ToArray());
        Assert.Equal(link, actual);
        Assert.Equal(RelationKind.Invokes, actual.Kind);
        Assert.Equal(source.Reference, actual.Source);
        Assert.Equal(target.Reference, actual.ProposedTarget);
        Assert.Equal(Resolution.Candidate, actual.Resolution);
        Assert.Equal("invokes", ReadWireKind(output.DirectoryPath, "relations/candidates.json"));
    }

    [Fact]
    [Trait("Requirement", "CLLF-08")]
    public void StageCommitRead_InvokesUnresolvedRecord_PreservesKindSourceAndCause()
    {
        var source = CallerSymbol();
        var record = UnresolvedRecord.Create(
            RelationKind.Invokes,
            source.Reference,
            UnresolvedCause.NoCandidateFound,
            ValidEvidence());
        var snapshot = new FactualSnapshot([source], [], [], [], [record], []);

        using var output = TempOutputRoot.Create();
        var restored = CommitAndRead(output.DirectoryPath, snapshot);

        var actual = Assert.Single(restored.Unresolved.ToArray());
        Assert.Equal(record, actual);
        Assert.Equal(RelationKind.Invokes, actual.Kind);
        Assert.Equal(source.Reference, actual.Source);
        Assert.Equal(UnresolvedCause.NoCandidateFound, actual.Cause);
        Assert.Equal(Resolution.Unresolved, actual.Resolution);
        Assert.Equal("invokes", ReadWireKind(output.DirectoryPath, "relations/unresolved.json"));
    }

    [Fact]
    [Trait("Requirement", "CLLF-11")]
    public void StageCommitRead_InvokesOpenFrontier_PreservesOccurrenceAndCause()
    {
        var occurrence = ValidOccurrence();
        var frontier = OpenFrontier.Create(occurrence, FrontierCause.FurtherContinuationObserved);
        var snapshot = new FactualSnapshot([], [], [], [], [], [frontier]);

        using var output = TempOutputRoot.Create();
        var restored = CommitAndRead(output.DirectoryPath, snapshot);

        var actual = Assert.Single(restored.Frontiers.ToArray());
        Assert.Equal(frontier, actual);
        Assert.Equal(occurrence, actual.Occurrence);
        Assert.Equal(ObservationKind.Invocation, actual.Occurrence.Kind);
        Assert.Equal(FrontierCause.FurtherContinuationObserved, actual.Cause);
        Assert.Equal(Frontier.Open, actual.Frontier);
        Assert.Equal("FurtherContinuationObserved", ReadWireFrontierCause(output.DirectoryPath));
    }

    private static FactualSnapshot CommitAndRead(string outputRoot, FactualSnapshot snapshot)
    {
        var store = new FilesystemTransactionalStore(outputRoot);
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(snapshot);
        session.Commit();
        return FactualPackageReader.Read(FilesystemTestPaths.ChildDirectory(outputRoot, SolutionKey)).Snapshot;
    }

    private static string ReadWireKind(string outputRoot, string relativePath)
    {
        var package = FilesystemTestPaths.ChildDirectory(outputRoot, SolutionKey);
        var bytes = File.ReadAllBytes(Path.Combine(package, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (relativePath == "relations/candidates.json")
        {
            var dto = Assert.Single(CanonicalJson.Read<ImmutableArray<CandidateLinkDto>>(bytes));
            Assert.Equal(RelationKind.Invokes, WireRelationMapping.FromDto(dto).Kind);
            return dto.Kind;
        }

        var unresolved = Assert.Single(CanonicalJson.Read<ImmutableArray<UnresolvedRecordDto>>(bytes));
        var restored = WireRelationMapping.FromDto(unresolved);
        Assert.Equal(RelationKind.Invokes, restored.Kind);
        Assert.Equal(UnresolvedCause.NoCandidateFound, restored.Cause);
        return unresolved.Kind;
    }

    private static string ReadWireFrontierCause(string outputRoot)
    {
        var package = FilesystemTestPaths.ChildDirectory(outputRoot, SolutionKey);
        var bytes = File.ReadAllBytes(Path.Combine(package, "relations", "frontiers.json"));
        var dto = Assert.Single(CanonicalJson.Read<ImmutableArray<OpenFrontierDto>>(bytes));
        Assert.Equal(FrontierCause.FurtherContinuationObserved, WireRelationMapping.FromDto(dto).Cause);
        return dto.Cause;
    }

    private static Symbol CallerSymbol() =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Orders.OrderService",
                "PlaceOrderAsync",
                0,
                "global::System.Threading.Tasks.Task"),
            AcmeProject,
            SymbolFacetSet.Create([SymbolFacet.Callable]));

    private static Symbol ConcreteOverrideSymbol() =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Payments.InMemoryEventBus",
                "PublishAsync",
                0,
                "global::System.Threading.Tasks.Task"),
            AcmeProject,
            SymbolFacetSet.Create([SymbolFacet.Callable]));

    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create([ValidOccurrence()]);

    private static ObservationIdentity ValidOccurrence() =>
        new(Solution.Create(AcmeSolution).Reference, ObservationKind.Invocation, NormalizedPayload.Create([]), 1);

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");
}
