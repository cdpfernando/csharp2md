using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Extraction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class CausalRelationExtractorTests
{
    [Fact]
    [Trait("Requirement", "DEP-02")]
    public void Extract_ProjectReference_EmitsEvidenceBackedProjectReference()
    {
        var result = Extract("class Sample { }");

        var relation = Assert.Single(result.Relations.Where(relation => relation.Category == "project-reference"));
        Assert.NotEmpty(relation.EvidenceCanonicalKeys);
        Assert.Contains(result.Evidence, evidence => evidence.CanonicalKey == relation.EvidenceCanonicalKeys[0]);
        Assert.All(result.Occurrences, occurrence => Assert.Equal("net10.0", occurrence.Variant.TargetFramework));
    }

    [Fact]
    [Trait("Requirement", "DEP-02")]
    public void Extract_InternalInvocation_EmitsCallableToCallableRelation()
    {
        var result = Extract("class Sample { void Source() => Target(); void Target() { } }");

        var relation = Assert.Single(result.Relations.Where(relation => relation.Category == "internal-invocation"));
        Assert.Contains(result.Entities, entity => entity.CanonicalKey == relation.SourceCanonicalKey && entity.Kind == EntityKind.Callable);
        Assert.Contains(result.Entities, entity => entity.CanonicalKey == relation.TargetCanonicalKey && entity.Kind == EntityKind.Callable);
    }

    [Fact]
    [Trait("Requirement", "CRT-08")]
    public void Extract_RepeatedCalls_EmitsSeparateFactualOccurrences()
    {
        var result = Extract("class Sample { void Source() { Target(); Target(); } void Target() { } }");

        var relations = result.Relations.Where(relation => relation.Category == "internal-invocation").ToArray();
        Assert.Equal(2, relations.Length);
        Assert.Equal(2, relations.Select(relation => relation.CanonicalKey).Distinct(StringComparer.Ordinal).Count());
        Assert.All(relations, relation => Assert.NotEmpty(relation.EvidenceCanonicalKeys));
    }

    [Fact]
    [Trait("Requirement", "DEP-02")]
    public void Extract_StructuralTypeUse_EmitsResolvedTypeRelation()
    {
        var result = Extract("class Payload { } class Sample { void Source(Payload payload) { } }");

        var relations = result.Relations.Where(relation => relation.Category == "structural-type-use").ToArray();
        Assert.Contains(relations, relation => result.Entities.Any(entity =>
            entity.CanonicalKey == relation.TargetCanonicalKey
            && entity.Kind == EntityKind.Symbol
            && entity.DisplayName == "Payload"));
    }

    [Fact]
    [Trait("Requirement", "DEP-02")]
    public void Extract_HttpLiteral_EmitsHttpRelationToObservedDestination()
    {
        var result = Extract("class Client { public void PostAsJsonAsync(string path) { } } class Sample { void Source() => new Client().PostAsJsonAsync(\"payments/authorize\"); }");

        var relation = Assert.Single(result.Relations.Where(relation => relation.Category == "http"));
        var target = Assert.Single(result.Entities.Where(entity => entity.CanonicalKey == relation.TargetCanonicalKey));
        Assert.Equal("http:payments/authorize", target.DisplayName);
    }

    [Fact]
    [Trait("Requirement", "PKG-09")]
    public void Extract_HttpWithoutLiteralDestination_RecordsCandidateInsteadOfConfirmedRelation()
    {
        var result = Extract("class Client { public void PostAsJsonAsync(string path) { } } class Sample { void Source(string path) => new Client().PostAsJsonAsync(path); }");

        Assert.DoesNotContain(result.Relations, relation => relation.Category == "http");
        var gap = Assert.Single(result.Gaps);
        Assert.Equal(GapKind.Candidate, gap.Kind);
        Assert.Equal("http-destination-unresolved", gap.Cause);
    }

    [Fact]
    [Trait("Requirement", "DEP-02")]
    public void Extract_GrpcClientInvocation_EmitsGrpcRelation()
    {
        var result = Extract("namespace Grpc.Core { public abstract class ClientBase { } } class Client : Grpc.Core.ClientBase { public void Authorize() { } } class Sample { void Source() => new Client().Authorize(); }");

        var relation = Assert.Single(result.Relations.Where(relation => relation.Category == "grpc"));
        var target = Assert.Single(result.Entities.Where(entity => entity.CanonicalKey == relation.TargetCanonicalKey));
        Assert.StartsWith("grpc:", target.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "DEP-02")]
    public void Extract_MessagingPublish_EmitsMessagingRelation()
    {
        var result = Extract("class Event { } class Bus { public void PublishAsync<T>(T value) { } } class Sample { void Source() => new Bus().PublishAsync(new Event()); }");

        var relation = Assert.Single(result.Relations.Where(relation => relation.Category == "messaging"));
        Assert.NotEmpty(relation.EvidenceCanonicalKeys);
    }

    [Fact]
    [Trait("Requirement", "DEP-02")]
    public void Extract_MessagingPublish_EmitsSeparateContractRelation()
    {
        var result = Extract("class Event { } class Bus { public void PublishAsync<T>(T value) { } } class Sample { void Source() => new Bus().PublishAsync(new Event()); }");

        var relation = Assert.Single(result.Relations.Where(relation => relation.Category == "contract"));
        var target = Assert.Single(result.Entities.Where(entity => entity.CanonicalKey == relation.TargetCanonicalKey));
        Assert.Equal(EntityKind.Contract, target.Kind);
    }

    [Fact]
    [Trait("Requirement", "PKG-09")]
    public void Extract_UnresolvedInvocation_RecordsUnknownGapInsteadOfConfirmedRelation()
    {
        var result = Extract("class Sample { void Source() => Missing(); }");

        Assert.DoesNotContain(result.Relations, relation => relation.Category == "internal-invocation");
        var gap = Assert.Single(result.Gaps);
        Assert.Equal(GapKind.Unknown, gap.Kind);
        Assert.Equal("unresolved-invocation", gap.Cause);
    }

    [Fact]
    [Trait("Requirement", "EDG-01")]
    public void Extract_EveryConfirmedRelation_ResolvesEvidenceChain()
    {
        var result = Extract("class Sample { void Source() => Target(); void Target() { } }");

        Assert.All(result.Relations, relation =>
        {
            Assert.NotEmpty(relation.EvidenceCanonicalKeys);
            Assert.All(relation.EvidenceCanonicalKeys, key => Assert.Contains(result.Evidence, evidence => evidence.CanonicalKey == key));
        });
    }

    [Fact]
    [Trait("Requirement", "EDG-04")]
    public void Extract_UsesTheInputVariantForEveryOccurrenceAndEvidence()
    {
        var result = Extract("class Sample { void Source() => Target(); void Target() { } }");

        Assert.All(result.Occurrences, occurrence => Assert.Equal("net10.0", occurrence.Variant.TargetFramework));
        Assert.All(result.Evidence, evidence => Assert.Equal("net10.0", evidence.Variant.TargetFramework));
    }

    [Fact]
    [Trait("Requirement", "PKG-09")]
    public void Extract_SameInput_ProducesDeterministicallyOrderedResults()
    {
        const string source = "class Sample { void Source() { Target(); Target(); } void Target() { } }";

        var first = Extract(source);
        var second = Extract(source);

        Assert.Equal(first.Relations.Select(relation => relation.CanonicalKey), second.Relations.Select(relation => relation.CanonicalKey));
        Assert.Equal(first.Gaps.Select(gap => gap.CanonicalKey), second.Gaps.Select(gap => gap.CanonicalKey));
    }

    [Fact]
    [Trait("Requirement", "EDG-01")]
    public void Extract_Gaps_ResolveSourceAndEvidence()
    {
        var result = Extract("class Sample { void Source() => Missing(); }");

        var gap = Assert.Single(result.Gaps);
        Assert.Contains(result.Entities, entity => entity.CanonicalKey == gap.AffectedEntityCanonicalKeys[0]);
        Assert.Contains(result.Evidence, evidence => evidence.CanonicalKey == gap.EvidenceCanonicalKeys[0]);
    }

    [Fact]
    [Trait("Requirement", "DEP-01")]
    public void Extract_ProjectReference_EvidenceKeyNamesBothTheCitingRootAndTheTarget()
    {
        // Two different roots each reference the same target. Before the fix, the evidence payload named
        // only the target, so both roots hashed to the same evidence key and Assemble's DistinctBy kept
        // only one root's document - silently reattributing the other root's edge to it.
        var solution = CanonicalIdentity.CreateSolution("causal", "src/Causal.slnx");
        var target = CanonicalIdentity.CreateProject(solution, "src/Shared/Shared.csproj");
        var first = ExtractForRoot(solution, "src/Api/Api.csproj", target);
        var second = ExtractForRoot(solution, "src/Worker/Worker.csproj", target);

        var firstEvidence = Assert.Single(first.Relations.Where(relation => relation.Category == "project-reference")).EvidenceCanonicalKeys;
        var secondEvidence = Assert.Single(second.Relations.Where(relation => relation.Category == "project-reference")).EvidenceCanonicalKeys;

        Assert.NotEqual(firstEvidence[0], secondEvidence[0]);
        Assert.Equal(
            CanonicalIdentity.CreateDocumentKey(solution, "src/Api/Api.csproj"),
            Assert.Single(first.Evidence).DocumentCanonicalKey);
        Assert.Equal(
            CanonicalIdentity.CreateDocumentKey(solution, "src/Worker/Worker.csproj"),
            Assert.Single(second.Evidence).DocumentCanonicalKey);
    }

    [Fact]
    [Trait("Requirement", "DEP-01")]
    public void Extract_ProjectReference_TargetEntityOccursInItselfNotInTheCitingRoot()
    {
        var solution = CanonicalIdentity.CreateSolution("causal", "src/Causal.slnx");
        var target = CanonicalIdentity.CreateProject(solution, "src/Shared/Shared.csproj");
        var result = ExtractForRoot(solution, "src/Api/Api.csproj", target);

        var targetEntity = Assert.Single(result.Entities.Where(entity => entity.Kind == EntityKind.Project && entity.QualifiedName == "src/Shared/Shared.csproj"));
        var occurrence = Assert.Single(result.Occurrences.Where(occurrence => occurrence.EntityCanonicalKey == targetEntity.CanonicalKey));
        Assert.Equal(target.CanonicalKey, occurrence.Project.CanonicalKey);
    }

    private static CausalRelationExtractionResult ExtractForRoot(SolutionIdentity solution, string rootPath, ProjectIdentity referenced)
    {
        var project = CanonicalIdentity.CreateProject(solution, rootPath);
        var tree = CSharpSyntaxTree.ParseText("class Sample { }", path: rootPath.Replace(".csproj", ".cs"));
        var compilation = CSharpCompilation.Create(
            "Root",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CausalRelationExtractor.Extract(new CausalRelationExtractionInput(
            solution,
            project,
            CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci"),
            compilation,
            [referenced]));
    }

    private static CausalRelationExtractionResult Extract(string source)
    {
        var solution = CanonicalIdentity.CreateSolution("causal", "src/Causal.slnx");
        var project = CanonicalIdentity.CreateProject(solution, "src/App/App.csproj");
        var referenced = CanonicalIdentity.CreateProject(solution, "src/Contracts/Contracts.csproj");
        var tree = CSharpSyntaxTree.ParseText(source, path: "src/App/Sample.cs");
        var compilation = CSharpCompilation.Create(
            "App",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CausalRelationExtractor.Extract(new CausalRelationExtractionInput(
            solution,
            project,
            CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci"),
            compilation,
            [referenced]));
    }
}
