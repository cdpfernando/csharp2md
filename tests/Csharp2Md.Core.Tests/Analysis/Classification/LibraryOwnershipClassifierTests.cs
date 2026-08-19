using Csharp2Md.Core.Analysis.Classification;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Classification;

public sealed class LibraryOwnershipClassifierTests
{
    private static readonly FactProvenance Provenance = new("csharp2md.semantic", "3.0.0");

    [Fact]
    public void LibraryReachedByExactlyOneExecutableRoot_IsPrivateToThatRoot()
    {
        var app = Project("src/App/App.csproj", "Exe", ["src/Library/Library.csproj"]);
        var library = Project("src/Library/Library.csproj", "Library");

        var assignment = Assert.Single(Classify(app, library));

        Assert.Equal(LibraryOwnershipKind.Private, assignment.Kind);
        Assert.Equal([app.Fact.ProjectId], assignment.OwnerProjectIds.ToArray());
    }

    [Fact]
    public void TransitivelyReachedLibrary_IsPrivateToTheExecutableRoot()
    {
        var app = Project("src/App/App.csproj", "Exe", ["src/Middle/Middle.csproj"]);
        var middle = Project("src/Middle/Middle.csproj", "Library", ["src/Leaf/Leaf.csproj"]);
        var leaf = Project("src/Leaf/Leaf.csproj", "Library");

        var assignments = Classify(app, middle, leaf);

        Assert.Equal(LibraryOwnershipKind.Private, assignments.Single(item => item.LibraryProjectId == leaf.Fact.ProjectId).Kind);
        Assert.Equal([app.Fact.ProjectId], assignments.Single(item => item.LibraryProjectId == leaf.Fact.ProjectId).OwnerProjectIds.ToArray());
    }

    [Fact]
    public void LibraryReachedBySeveralExecutableRoots_IsSharedDependency()
    {
        var alpha = Project("src/Alpha/Alpha.csproj", "Exe", ["src/Shared/Shared.csproj"]);
        var zeta = Project("src/Zeta/Zeta.csproj", "Exe", ["src/Shared/Shared.csproj"]);
        var shared = Project("src/Shared/Shared.csproj", "Library");

        var assignment = Assert.Single(Classify(zeta, shared, alpha));

        Assert.Equal(LibraryOwnershipKind.SharedDependency, assignment.Kind);
        Assert.Equal([alpha.Fact.ProjectId, zeta.Fact.ProjectId], assignment.OwnerProjectIds.ToArray());
    }

    [Fact]
    public void LibraryWithNoExecutableConsumer_IsStandalone()
    {
        var library = Project("src/Library/Library.csproj", "Library");

        var assignment = Assert.Single(Classify(library));

        Assert.Equal(LibraryOwnershipKind.Standalone, assignment.Kind);
        Assert.Equal([library.Fact.ProjectId], assignment.OwnerProjectIds.ToArray());
    }

    [Fact]
    public void CyclicLibraryReferences_TerminateAndRemainPrivateToOneRoot()
    {
        var app = Project("src/App/App.csproj", "Exe", ["src/First/First.csproj"]);
        var first = Project("src/First/First.csproj", "Library", ["src/Second/Second.csproj"]);
        var second = Project("src/Second/Second.csproj", "Library", ["src/First/First.csproj"]);

        var assignments = Classify(app, first, second);

        Assert.All(assignments, assignment => Assert.Equal(LibraryOwnershipKind.Private, assignment.Kind));
        Assert.All(assignments, assignment => Assert.Equal([app.Fact.ProjectId], assignment.OwnerProjectIds.ToArray()));
    }

    [Fact]
    public void TestOnlyConsumer_DoesNotOwnTheLibrary()
    {
        var tests = Project(
            "tests/App.Tests/App.Tests.csproj",
            "Library",
            ["src/Library/Library.csproj"],
            ["Microsoft.NET.Test.Sdk"]);
        var library = Project("src/Library/Library.csproj", "Library");

        var assignment = Assert.Single(Classify(tests, library));

        Assert.Equal(LibraryOwnershipKind.Standalone, assignment.Kind);
        Assert.Equal([library.Fact.ProjectId], assignment.OwnerProjectIds.ToArray());
    }

    [Fact]
    public void RuntimeLogicalDestination_DoesNotCreateCompileTimeOwnership()
    {
        var app = Project("src/App/App.csproj", "Exe");
        var library = Project("src/Library/Library.csproj", "Library");
        var runtimeRelation = RuntimeRelation(app.Target, library.Fact.ProjectId);

        var assignment = Assert.Single(Classify([app, library], [runtimeRelation]));

        Assert.Equal(LibraryOwnershipKind.Standalone, assignment.Kind);
        Assert.Equal([library.Fact.ProjectId], assignment.OwnerProjectIds.ToArray());
    }

    [Fact]
    public void Assignments_AreCanonicalByLibraryIdentity()
    {
        var zeta = Project("src/Zeta/Zeta.csproj", "Library");
        var alpha = Project("src/Alpha/Alpha.csproj", "Library");

        var assignments = Classify(zeta, alpha);

        Assert.Equal([alpha.Fact.ProjectId, zeta.Fact.ProjectId], assignments.Select(static item => item.LibraryProjectId).ToArray());
    }

    [Fact]
    public void UnresolvedProjectReference_DoesNotCreateAnOwner()
    {
        var app = Project("src/App/App.csproj", "Exe", ["src/Missing/Missing.csproj"]);
        var library = Project("src/Library/Library.csproj", "Library");

        var assignment = Assert.Single(Classify(app, library));

        Assert.Equal(LibraryOwnershipKind.Standalone, assignment.Kind);
        Assert.Equal([library.Fact.ProjectId], assignment.OwnerProjectIds.ToArray());
    }

    private static ImmutableArray<LibraryOwnershipAssignment> Classify(params ProjectInput[] projects) =>
        Classify(projects, []);

    private static ImmutableArray<LibraryOwnershipAssignment> Classify(
        IEnumerable<ProjectInput> projects,
        IEnumerable<RelationFact> relations)
    {
        var projectArray = projects.ToImmutableArray();
        var relationsByTarget = relations
            .GroupBy(static relation => relation.SourceId.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToImmutableArray(), StringComparer.Ordinal);
        var index = SolutionAnalysisIndex.Build(
            projectArray.Select(static item => item.Fact),
            projectArray.Select(item => new TargetAnalysisIndexInput(
                item.Target,
                [],
                relationsByTarget.GetValueOrDefault(item.Target.TargetId.Value, []))));
        return LibraryOwnershipClassifier.Classify(index);
    }

    private static ProjectInput Project(
        string path,
        string outputType,
        ImmutableArray<string> references = default,
        ImmutableArray<string> packages = default)
    {
        var projectId = ProjectFactId.Create(path);
        var targetId = TargetFactId.Create(projectId, "net10.0");
        var target = new TargetFact(
            Header(targetId.ToFactId(), FactKind.Target),
            targetId,
            projectId,
            "net10.0",
            new TargetEvaluationDetails(
                outputType,
                Path.GetFileNameWithoutExtension(path),
                Path.GetFileNameWithoutExtension(path),
                [],
                references.IsDefault ? [] : references,
                packages.IsDefault ? [] : packages,
                [],
                [],
                "preview",
                "enable",
                []));
        var fact = new ProjectFact(
            Header(projectId.ToFactId(), FactKind.Project),
            projectId,
            Path.GetFileNameWithoutExtension(path),
            path,
            [targetId],
            []);
        return new ProjectInput(fact, target);
    }

    private static RelationFact RuntimeRelation(TargetFact source, ProjectFactId target)
    {
        var id = RelationFactId.Create(source.TargetId.ToFactId(), "http-request", "library-name", 1);
        return new RelationFact(
            Header(id.ToFactId(), FactKind.Relation),
            id,
            source.TargetId.ToFactId(),
            target.ToFactId(),
            RelationPartition.Http,
            "http-request",
            null);
    }

    private static FactHeader Header(FactId id, FactKind kind) =>
        FactHeader.Create(id, kind, FactResolution.Exact, [Provenance]);

    private sealed record ProjectInput(ProjectFact Fact, TargetFact Target);
}
