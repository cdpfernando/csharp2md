using System.Reflection;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class SnapshotAccumulatorTests
{
    [Fact]
    [Trait("Requirement", "ROSE-21")]
    public void AddFact_EqualFactsWithOneIdentity_KeepsOneAndDoesNotCorrupt()
    {
        var solution = Solution.Create(AcmeSolution);
        var accumulator = new SnapshotAccumulator();

        accumulator.AddFact(solution);
        accumulator.AddFact(Solution.Create(AcmeSolution));

        Assert.False(accumulator.StructuralCorruption);
        Assert.Equal(solution, Assert.Single(accumulator.ToSnapshot().Facts.ToArray()));
    }

    [Fact]
    [Trait("Requirement", "ROSE-21")]
    public void AddFact_UnequalFactsWithOneIdentity_SetsStructuralCorruption()
    {
        var identity = Solution.Create(AcmeSolution).Reference;
        var accumulator = new SnapshotAccumulator();

        accumulator.AddFact(new CollidingFact(identity, "left"));
        accumulator.AddFact(new CollidingFact(identity, "right"));

        Assert.True(accumulator.StructuralCorruption);
        Assert.Equal("left", Assert.IsType<CollidingFact>(Assert.Single(accumulator.ToSnapshot().Facts.ToArray())).Marker);
    }

    [Fact]
    [Trait("Requirement", "ROSE-44")]
    public void AddObservation_SpanOnlyDifference_CollapsesToOneIdentity()
    {
        var first = CreateObservation(new SourceSpan(1, 1, 1, 8), new BindingDiagnostic("unbound", "unbound"));
        var second = CreateObservation(new SourceSpan(40, 2, 40, 20), new BindingDiagnostic("unbound", "unbound"));
        var accumulator = new SnapshotAccumulator();

        accumulator.AddObservation(first);
        accumulator.AddObservation(second);

        Assert.Equal(first.Identity, second.Identity);
        Assert.NotEqual(first.Locator.Span, second.Locator.Span);
        Assert.Equal(first, Assert.Single(accumulator.ToSnapshot().Observations.ToArray()));
    }

    [Fact]
    [Trait("Requirement", "ROSE-21")]
    [Trait("Requirement", "ROSE-44")]
    public void AddObservation_BoundWinsOverUnboundForTheSameIdentity()
    {
        var unbound = CreateObservation(new SourceSpan(1, 1, 1, 8), new BindingDiagnostic("unbound", "unbound"));
        var bound = CreateObservation(new SourceSpan(2, 1, 2, 8), new BindingDiagnostic("bound", "bound"));
        var unboundFirst = new SnapshotAccumulator();
        var boundFirst = new SnapshotAccumulator();

        unboundFirst.AddObservation(unbound);
        unboundFirst.AddObservation(bound);
        boundFirst.AddObservation(bound);
        boundFirst.AddObservation(unbound);

        Assert.Equal("bound", Assert.Single(unboundFirst.ToSnapshot().Observations.ToArray()).Diagnostic.Code);
        Assert.Equal("bound", Assert.Single(boundFirst.ToSnapshot().Observations.ToArray()).Diagnostic.Code);
        Assert.Equal(bound, Assert.Single(unboundFirst.ToSnapshot().Observations.ToArray()));
        Assert.Equal(bound, Assert.Single(boundFirst.ToSnapshot().Observations.ToArray()));
    }

    [Fact]
    [Trait("Requirement", "ROSE-21")]
    public void ToSnapshot_IncludesFactsObservationsRelationsDiagnosticsAndSuspectedSecrets()
    {
        var solution = Solution.Create(AcmeSolution);
        var observation = CreateObservation(new SourceSpan(1, 1, 1, 8), new BindingDiagnostic("bound", "bound"));
        var relation = CreateContainsRelation();
        var diagnostic = new DiagnosticRecord("missing-project", "Acme.DoesNotExist is absent.", "Acme.DoesNotExist/Acme.DoesNotExist.csproj");
        var secret = SuspectedSecretEvidence.Create(
            DocumentId.Create("src/Acme.Orders/Program.cs"),
            new SourceSpan(3, 1, 3, 12),
            DocumentHash.Create(new string('a', 64)),
            RedactedExcerpt.Create("Password=***"));
        var accumulator = new SnapshotAccumulator();

        accumulator.AddFact(solution);
        accumulator.AddObservation(observation);
        accumulator.AddRelation(relation);
        accumulator.AddDiagnostic(diagnostic);
        accumulator.AddSuspectedSecret(secret);

        var snapshot = accumulator.ToSnapshot();

        Assert.Equal(solution, Assert.Single(snapshot.Facts.ToArray()));
        Assert.Equal(observation, Assert.Single(snapshot.Observations.ToArray()));
        Assert.Equal(relation, Assert.Single(snapshot.ConfirmedRelations.ToArray()));
        Assert.Equal(diagnostic, Assert.Single(snapshot.Diagnostics.ToArray()));
        Assert.Equal(secret, Assert.Single(snapshot.SuspectedSecrets.ToArray()));
        Assert.Empty(snapshot.Candidates);
        Assert.Empty(snapshot.Unresolved);
        Assert.Empty(snapshot.Frontiers);
    }

    [Fact]
    [Trait("Requirement", "ROSE-21")]
    public void PipelineContext_ExposesAnEmptyAccumulator()
    {
        var context = new PipelineContext(new SwallowingSession(), "alpha.sln");

        Assert.False(context.Accumulator.StructuralCorruption);
        var snapshot = context.Accumulator.ToSnapshot();
        Assert.Empty(snapshot.Facts);
        Assert.Empty(snapshot.Observations);
        Assert.Empty(snapshot.ConfirmedRelations);
        Assert.Empty(snapshot.Diagnostics);
        Assert.Empty(snapshot.SuspectedSecrets);
    }

    [Fact]
    [Trait("Requirement", "ROSE-21")]
    public void SnapshotAccumulator_DoesNotExposeRoslynTypes()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var roslyn = typeof(SnapshotAccumulator)
            .GetMethods(flags)
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
            .Concat(typeof(SnapshotAccumulator).GetProperties(flags).Select(property => property.PropertyType))
            .Where(type => type.Namespace is not null && type.Namespace.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(roslyn.Length == 0, $"SnapshotAccumulator exposes Roslyn type(s): {string.Join(", ", roslyn)}.");
    }

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static Observation CreateObservation(SourceSpan span, BindingDiagnostic diagnostic) =>
        Observation.Create(
            Solution.Create(AcmeSolution).Reference,
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Orders/Program.cs", span),
            EvidenceMethod.Semantic,
            diagnostic,
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));

    private static ConfirmedRelation CreateContainsRelation()
    {
        var solution = Solution.Create(AcmeSolution);
        var project = Project.Create(ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj"));
        return ConfirmedRelation.Create(
            RelationKind.Contains,
            solution.Reference,
            project.Reference,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([CreateObservation(new SourceSpan(1, 1, 1, 8), new BindingDiagnostic("bound", "bound")).Identity]),
            ClassifierIdentity.Create("csharp2md.inventory.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Debug", [], "local")],
            EvidenceMethod.Syntactic);
    }

    private sealed record CollidingFact(FactReference Reference, string Marker) : IFact
    {
        public FactFamily Family => FactFamily.Structural;
    }
}
