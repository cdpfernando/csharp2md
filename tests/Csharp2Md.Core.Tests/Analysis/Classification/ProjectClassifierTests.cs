using Csharp2Md.Core.Analysis.Classification;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Classification;

public sealed class ProjectClassifierTests
{
    private static readonly FactProvenance Provenance = new("csharp2md.semantic", "3.0.0");

    [Fact]
    public void ExecutableWithConfirmedHttpEndpoint_IsWebApi()
    {
        var input = Fixture("Exe", relations: [Relation(ProjectClassifier.HttpEndpointRelationKind)]);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("service/web-api", classification);
    }

    [Fact]
    public void ExecutableWithEndpointAndHostedService_PrioritizesWebApi()
    {
        var input = Fixture(
            "Exe",
            typeReferences: ["global::Microsoft.Extensions.Hosting.IHostedService"],
            relations: [Relation(ProjectClassifier.HttpEndpointRelationKind)]);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("service/web-api", classification);
    }

    [Theory]
    [InlineData("global::Microsoft.Extensions.Hosting.IHostedService")]
    [InlineData("global::Microsoft.Extensions.Hosting.BackgroundService")]
    public void ExecutableWithConfirmedHostedService_IsWorker(string hostedServiceType)
    {
        var input = Fixture("Exe", typeReferences: [hostedServiceType]);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("service/worker", classification);
    }

    [Theory]
    [InlineData("Exe")]
    [InlineData("WinExe")]
    public void RemainingExecutable_IsCli(string outputType)
    {
        var input = Fixture(outputType);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("tool/cli", classification);
    }

    [Fact]
    public void ConfirmedMicrosoftTestSdkProject_IsTestSupport()
    {
        var input = Fixture("Library", packageReferences: ["Microsoft.NET.Test.Sdk"]);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("test-support", classification);
    }

    [Fact]
    public void ConfirmedNonTestLibrary_IsLibrary()
    {
        var input = Fixture("Library", packageReferences: ["Example.Package"]);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("library", classification);
    }

    [Fact]
    public void ProjectNameLookalikes_DoNotCreateFrameworkOrTestEvidence()
    {
        var input = Fixture("Library", projectPath: "src/WebApiWorker.Tests/WebApiWorker.Tests.csproj");

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("library", classification);
    }

    [Fact]
    public void NonEndpointHttpRelation_DoesNotPromoteExecutableToWebApi()
    {
        var input = Fixture("Exe", relations: [Relation("http-request")]);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("tool/cli", classification);
    }

    [Fact]
    public void HostedServiceNameLookalike_DoesNotPromoteExecutableToWorker()
    {
        var input = Fixture("Exe", typeReferences: ["global::Contoso.IHostedService"]);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("tool/cli", classification);
    }

    [Fact]
    public void UnconfirmedTarget_ReturnsNoClassification()
    {
        var input = Fixture("Library", resolution: FactResolution.Syntactic);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Null(classification);
    }

    [Fact]
    public void MicrosoftTestSdkEvidence_PrecedesExecutableClassification()
    {
        var input = Fixture("Exe", packageReferences: ["Microsoft.NET.Test.Sdk"]);

        var classification = ProjectClassifier.Classify(input.Project.ProjectId, input.Index);

        Assert.Equal("test-support", classification);
    }

    private static ClassificationFixture Fixture(
        string outputType,
        string projectPath = "src/App/App.csproj",
        ImmutableArray<string> packageReferences = default,
        ImmutableArray<string> typeReferences = default,
        ImmutableArray<RelationFact> relations = default,
        FactResolution resolution = FactResolution.Exact)
    {
        var projectId = ProjectFactId.Create(projectPath);
        var project = new ProjectFact(
            Header(projectId.ToFactId(), FactKind.Project, resolution),
            projectId,
            Path.GetFileNameWithoutExtension(projectPath),
            projectPath,
            [],
            []);
        var targetId = TargetFactId.Create(projectId, "net10.0");
        var target = new TargetFact(
            Header(targetId.ToFactId(), FactKind.Target, resolution),
            targetId,
            projectId,
            "net10.0",
            new TargetEvaluationDetails(
                outputType,
                "App",
                "App",
                [],
                [],
                packageReferences.IsDefault ? [] : packageReferences,
                [],
                [],
                "preview",
                "enable",
                []));
        var symbol = Symbol(projectId, targetId, typeReferences.IsDefault ? [] : typeReferences);
        var index = SolutionAnalysisIndex.Build(
            [project],
            [new TargetAnalysisIndexInput(target, [symbol], relations.IsDefault ? [] : relations)]);
        return new ClassificationFixture(project, index);
    }

    private static SymbolFact Symbol(
        ProjectFactId projectId,
        TargetFactId targetId,
        ImmutableArray<string> typeReferences)
    {
        var id = SymbolFactId.CreateResolved(targetId, "T:App.Program");
        return new SymbolFact(
            Header(id.ToFactId(), FactKind.Symbol),
            id,
            DocumentFactId.Create(projectId, "Program.cs"),
            "class",
            false,
            [],
            [],
            typeReferences,
            Semantics: null,
            Name: "Program",
            FullyQualifiedName: "global::App.Program",
            Namespace: "App",
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: "class Program",
            Arity: 0,
            ParameterTypes: []);
    }

    private static RelationFact Relation(string relationKind)
    {
        var projectId = ProjectFactId.Create("src/App/App.csproj");
        var targetId = TargetFactId.Create(projectId, "net10.0");
        var id = RelationFactId.Create(targetId.ToFactId(), relationKind, relationKind, 1);
        return new RelationFact(
            Header(id.ToFactId(), FactKind.Relation),
            id,
            targetId.ToFactId(),
            null,
            RelationPartition.Http,
            relationKind,
            "endpoint target is not a component identity");
    }

    private static FactHeader Header(
        FactId id,
        FactKind kind,
        FactResolution resolution = FactResolution.Exact) =>
        FactHeader.Create(id, kind, resolution, [Provenance]);

    private sealed record ClassificationFixture(ProjectFact Project, SolutionAnalysisIndex Index);
}
