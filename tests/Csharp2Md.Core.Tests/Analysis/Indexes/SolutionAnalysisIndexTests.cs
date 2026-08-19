using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Indexes;

[Trait("Category", "Integration")]
public sealed class SolutionAnalysisIndexTests
{
    private static readonly FactProvenance Provenance = new("csharp2md.semantic", "3.0.0");

    [Fact]
    public void Build_MaterializesTheSolutionTargetSetOnceAndReusesTheSnapshot()
    {
        var project = Project("src/App/App.csproj");
        var target = Target(project, "net10.0");
        var inputs = new SingleEnumeration<TargetAnalysisIndexInput>([Input(target)]);

        var index = SolutionAnalysisIndex.Build([project], inputs);

        Assert.Equal([target.TargetId], index.GetTargets(project.ProjectId).Select(static item => item.TargetId).ToArray());
        Assert.Equal([target.TargetId], index.GetTargets(project.ProjectId).Select(static item => item.TargetId).ToArray());
        Assert.Equal(1, inputs.EnumerationCount);
    }

    [Fact]
    public void Symbols_WithTheSameShapeRemainSeparatedByTargetIdentity()
    {
        var project = Project("src/App/App.csproj");
        var net9 = Target(project, "net9.0");
        var net10 = Target(project, "net10.0");
        var net9Symbol = Symbol(project, net9, "T:App.Handler", "global::Legacy.Contract");
        var net10Symbol = Symbol(project, net10, "T:App.Handler", "global::Current.Contract");

        var index = SolutionAnalysisIndex.Build(
            [project],
            [Input(net10, [net10Symbol]), Input(net9, [net9Symbol])]);

        Assert.Equal([net9Symbol.SymbolId], index.GetSymbols(net9.TargetId).Select(static symbol => symbol.SymbolId).ToArray());
        Assert.Equal([net10Symbol.SymbolId], index.GetSymbols(net10.TargetId).Select(static symbol => symbol.SymbolId).ToArray());
        Assert.Empty(index.GetSymbolsReferencingType(net9.TargetId, "global::Current.Contract"));
    }

    [Fact]
    public void TypeReferenceLookup_ReturnsOnlyMatchingSymbolsInCanonicalOrder()
    {
        var project = Project("src/App/App.csproj");
        var target = Target(project, "net10.0");
        var late = Symbol(project, target, "T:App.Zeta", "global::Contracts.IMessage");
        var early = Symbol(project, target, "T:App.Alpha", "global::Contracts.IMessage");

        var index = SolutionAnalysisIndex.Build([project], [Input(target, [late, early])]);

        Assert.Equal(
            [early.SymbolId, late.SymbolId],
            index.GetSymbolsReferencingType(target.TargetId, "global::Contracts.IMessage")
                .Select(static symbol => symbol.SymbolId)
                .ToArray());
    }

    [Fact]
    public void ProjectReferenceLookup_ResolvesKnownRelativePathsAndRetainsUnknownReferences()
    {
        var app = Project("src/App/App.csproj");
        var shared = Project("src/Shared/Shared.csproj");
        var target = Target(app, "net10.0", ["../Shared/Shared.csproj", "../Missing/Missing.csproj"]);

        var index = SolutionAnalysisIndex.Build([shared, app], [Input(target)]);

        var references = index.GetProjectReferences(target.TargetId);
        Assert.Equal(["../Missing/Missing.csproj", "../Shared/Shared.csproj"], references.Select(static item => item.Reference).ToArray());
        Assert.Null(references[0].TargetProjectId);
        Assert.Equal(shared.ProjectId, references[1].TargetProjectId);
    }

    [Fact]
    public void Relations_AreTargetScopedAndCanonicallyOrdered()
    {
        var project = Project("src/App/App.csproj");
        var net9 = Target(project, "net9.0");
        var net10 = Target(project, "net10.0");
        var late = Relation(net10, "zeta");
        var early = Relation(net10, "alpha");
        var otherTarget = Relation(net9, "other");

        var index = SolutionAnalysisIndex.Build(
            [project],
            [Input(net10, relations: [late, early]), Input(net9, relations: [otherTarget])]);

        Assert.Equal(
            [early.RelationId, late.RelationId],
            index.GetRelations(net10.TargetId).Select(static relation => relation.RelationId).ToArray());
        Assert.Equal([otherTarget.RelationId], index.GetRelations(net9.TargetId).Select(static relation => relation.RelationId).ToArray());
    }

    [Fact]
    public void ProjectAndTargetEnumeration_IsCanonicalRegardlessOfInputOrder()
    {
        var zeta = Project("src/Zeta/Zeta.csproj");
        var alpha = Project("src/Alpha/Alpha.csproj");
        var net9 = Target(alpha, "net9.0");
        var net10 = Target(alpha, "net10.0");

        var index = SolutionAnalysisIndex.Build(
            [zeta, alpha],
            [Input(net9), Input(net10)]);

        Assert.Equal([alpha.ProjectId, zeta.ProjectId], index.Projects.Select(static project => project.ProjectId).ToArray());
        Assert.Equal([net10.TargetId, net9.TargetId], index.GetTargets(alpha.ProjectId).Select(static target => target.TargetId).ToArray());
    }

    [Fact]
    public void RelationPresence_ReusesTargetSummariesAcrossAProject()
    {
        var project = Project("src/App/App.csproj");
        var net9 = Target(project, "net9.0");
        var net10 = Target(project, "net10.0");

        var index = SolutionAnalysisIndex.Build(
            [project],
            [Input(net9), Input(net10, relations: [Relation(net10, "http-endpoint", RelationPartition.Http)])]);

        Assert.True(index.HasRelation(project.ProjectId, RelationPartition.Http, "http-endpoint"));
        Assert.False(index.HasRelation(project.ProjectId, RelationPartition.Events, "http-endpoint"));
    }

    private static ProjectFact Project(string path)
    {
        var id = ProjectFactId.Create(path);
        return new ProjectFact(
            Header(id.ToFactId(), FactKind.Project),
            id,
            Path.GetFileNameWithoutExtension(path),
            path,
            [],
            []);
    }

    private static TargetFact Target(
        ProjectFact project,
        string targetFramework,
        ImmutableArray<string> projectReferences = default)
    {
        var id = TargetFactId.Create(project.ProjectId, targetFramework);
        return new TargetFact(
            Header(id.ToFactId(), FactKind.Target),
            id,
            project.ProjectId,
            targetFramework,
            new TargetEvaluationDetails(
                "Library", "App", "App", [],
                projectReferences.IsDefault ? [] : projectReferences,
                [], [], [], "preview", "enable", []));
    }

    private static SymbolFact Symbol(
        ProjectFact project,
        TargetFact target,
        string documentationId,
        params string[] typeReferences)
    {
        var documentId = DocumentFactId.Create(project.ProjectId, "Handler.cs");
        var id = SymbolFactId.CreateResolved(target.TargetId, documentationId);
        return new SymbolFact(
            Header(id.ToFactId(), FactKind.Symbol),
            id,
            documentId,
            "class",
            false,
            [],
            [],
            typeReferences.Order(StringComparer.Ordinal).ToImmutableArray(),
            Semantics: null,
            Name: "Handler",
            FullyQualifiedName: "global::Handler",
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: "class Handler",
            Arity: 0,
            ParameterTypes: []);
    }

    private static RelationFact Relation(
        TargetFact target,
        string claim,
        RelationPartition partition = RelationPartition.CompileTime)
    {
        var id = RelationFactId.Create(target.TargetId.ToFactId(), "type-reference", claim, 1);
        return new RelationFact(
            Header(id.ToFactId(), FactKind.Relation),
            id,
            target.TargetId.ToFactId(),
            null,
            partition,
            claim == "http-endpoint" ? claim : "type-reference",
            "target is intentionally unresolved");
    }

    private static TargetAnalysisIndexInput Input(
        TargetFact target,
        ImmutableArray<SymbolFact> symbols = default,
        ImmutableArray<RelationFact> relations = default) =>
        new(
            target,
            symbols.IsDefault ? [] : symbols,
            relations.IsDefault ? [] : relations);

    private static FactHeader Header(FactId id, FactKind kind) =>
        FactHeader.Create(id, kind, FactResolution.Exact, [Provenance]);

    private sealed class SingleEnumeration<T>(IEnumerable<T> values) : IEnumerable<T>
    {
        private readonly IEnumerable<T> _values = values;

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount > 1)
            {
                throw new InvalidOperationException("The solution target set was searched more than once.");
            }

            return _values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
