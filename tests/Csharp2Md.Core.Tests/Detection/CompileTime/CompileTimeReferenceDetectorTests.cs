using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Detection.CompileTime;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Detection.CompileTime;

public sealed class CompileTimeReferenceDetectorTests
{
    private const string ProjectReference = "project-reference";
    private const string PackageReference = "package-reference";

    private static readonly ProjectFactId OrdersProjectId = ProjectFactId.Create("src/Acme.Orders/Acme.Orders.csproj");
    private static readonly ProjectFactId ContractsProjectId = ProjectFactId.Create("src/Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
    private static readonly TargetFactId OrdersTargetId = TargetFactId.Create(OrdersProjectId, "net10.0");
    private static readonly TargetFactId ContractsTargetId = TargetFactId.Create(ContractsProjectId, "net10.0");

    // FACT-48: a project reference that resolves inside the indexed solution.
    [Fact]
    public void Detect_ProjectReferenceToAnIndexedProject_ResolvesTheTarget()
    {
        var facts = Detect(
            projectReferences: ["../Acme.Shared.Contracts/Acme.Shared.Contracts.csproj"],
            packageReferences: [],
            includeContracts: true);

        var fact = Assert.Single(facts, item => item.RelationKind == ProjectReference);
        Assert.Equal(ContractsProjectId.ToFactId(), fact.TargetId);
        Assert.Null(fact.UnresolvedReason);
        Assert.Equal(RelationPartition.CompileTime, fact.Partition);
        Assert.False(fact.IsRuntime);
    }

    // FACT-46/47: a project reference outside the indexed solution stays honestly unresolved.
    [Fact]
    public void Detect_ProjectReferenceOutsideTheSolution_StaysUnresolved()
    {
        var facts = Detect(
            projectReferences: ["../Acme.Unindexed/Acme.Unindexed.csproj"],
            packageReferences: [],
            includeContracts: false);

        var fact = Assert.Single(facts, item => item.RelationKind == ProjectReference);
        Assert.Null(fact.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));
    }

    // FACT-48: package references are always external and stay unresolved, with the id preserved.
    [Fact]
    public void Detect_PackageReference_IsAlwaysExternalAndUnresolved()
    {
        var facts = Detect(projectReferences: [], packageReferences: ["Newtonsoft.Json"], includeContracts: false);

        var fact = Assert.Single(facts, item => item.RelationKind == PackageReference);
        Assert.Null(fact.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));
        Assert.Contains(fact.Details, detail => detail.Key == "reference" && detail.Value == "Newtonsoft.Json");
    }

    [Fact]
    public void Detect_MultiplePackageReferences_AreAllEmittedDistinctly()
    {
        var facts = Detect(
            projectReferences: [],
            packageReferences: ["Newtonsoft.Json", "Serilog"],
            includeContracts: false);

        var references = facts.Where(fact => fact.RelationKind == PackageReference)
            .SelectMany(static fact => fact.Details)
            .Where(static detail => detail.Key == "reference")
            .Select(static detail => detail.Value)
            .ToArray();
        Assert.Equal(["Newtonsoft.Json", "Serilog"], references.Order());
    }

    // FACT-46/48: unlike v2's PackageId-name matching, a package id is never resolved to a project
    // target even when it coincides with another indexed project's name - packages are external by
    // construction, and evaluated references carry no such matching rule.
    [Fact]
    public void Detect_PackageReferenceNameCoincidingWithAnIndexedProjectName_StaysExternal()
    {
        var facts = Detect(
            projectReferences: [],
            packageReferences: ["Acme.Shared.Contracts"],
            includeContracts: true);

        var fact = Assert.Single(facts, item => item.RelationKind == PackageReference);
        Assert.Null(fact.TargetId);
        Assert.NotEqual(ContractsProjectId.ToFactId(), fact.TargetId);
    }

    [Fact]
    public void Detect_ProjectWithNoReferences_EmitsNothing()
    {
        Assert.Empty(Detect(projectReferences: [], packageReferences: [], includeContracts: false));
    }

    // Every compile-time fact belongs to the compile-time partition, never a runtime one (FACT-16/48).
    [Fact]
    public void Detect_EveryEmittedFact_IsNeverClassifiedAsRuntime()
    {
        var facts = Detect(
            projectReferences: ["../Acme.Shared.Contracts/Acme.Shared.Contracts.csproj"],
            packageReferences: ["Newtonsoft.Json"],
            includeContracts: true);

        Assert.NotEmpty(facts);
        Assert.All(facts, fact => Assert.False(fact.IsRuntime));
        Assert.All(facts, fact => Assert.Equal(RelationPartition.CompileTime, fact.Partition));
    }

    [Fact]
    public void Detect_EmittedFacts_CarryDetectorProvenance()
    {
        var fact = Assert.Single(Detect(
            projectReferences: [],
            packageReferences: ["Newtonsoft.Json"],
            includeContracts: false));

        Assert.Contains(fact.Header.Provenance, static provenance =>
            provenance.DetectorId?.Value == "id1:detector;name=io.csharp2md.compile-time"
            && provenance.DetectorVersion == "1.0.0");
    }

    // Multi-target projects keep one fact per target rather than collapsing them (mirrors T25).
    [Fact]
    public void Detect_MultiTargetProject_KeepsOneFactPerTarget()
    {
        var net10 = TargetFactId.Create(OrdersProjectId, "net10.0");
        var net9 = TargetFactId.Create(OrdersProjectId, "net9.0");
        var orders = OrdersProject([net10, net9]);
        var index = SolutionAnalysisIndex.Build(
            [orders],
            [
                new TargetAnalysisIndexInput(Target(net10, OrdersProjectId, ["Newtonsoft.Json"], []), [], []),
                new TargetAnalysisIndexInput(Target(net9, OrdersProjectId, ["Newtonsoft.Json"], []), [], []),
            ]);
        var context = new Csharp2Md.Core.Detection.Contracts.ProjectDetectionContext(
            orders,
            [Target(net10, OrdersProjectId, ["Newtonsoft.Json"], []), Target(net9, OrdersProjectId, ["Newtonsoft.Json"], [])],
            [],
            index);

        var facts = new DetectorHost(projectDetectors: [new CompileTimeReferenceDetector()])
            .DetectProject(context)
            .Facts
            .OfType<RelationFact>()
            .Where(fact => fact.RelationKind == PackageReference)
            .ToArray();

        Assert.Equal(2, facts.Length);
        Assert.Equal(2, facts.Select(static fact => fact.RelationId.Value).Distinct().Count());
    }

    // FACT-16/FACT-48: the fact validator rejects a compile-time-only kind misclassified as runtime.
    [Fact]
    public void Validator_RejectsAProjectReferenceKindClassifiedAsRuntime()
    {
        var document = DocumentFactId.Create(OrdersProjectId, "Program.cs");
        var misclassified = new RelationFact(
            FactHeader.Create(
                RelationFactId.Create(OrdersProjectId.ToFactId(), ProjectReference, "reference=x", 1).ToFactId(),
                FactKind.Relation,
                FactResolution.Exact,
                provenance: [new FactProvenance("csharp2md", "3.0.0", DetectorId.Create("io.csharp2md.compile-time"), "1.0.0")],
                evidence: [new Evidence(document, "Program.cs", 1, 1, 1, 1)]),
            RelationFactId.Create(OrdersProjectId.ToFactId(), ProjectReference, "reference=x", 1),
            OrdersProjectId.ToFactId(),
            null,
            RelationPartition.Http,
            ProjectReference,
            "invalid on purpose",
            []);

        var input = FactValidationInput.Create(
            [misclassified],
            documents: [DocumentExtent.Create(document, "Program.cs", [80])]);

        var result = FactValidator.Validate(input);

        Assert.Null(result.Fragment);
        Assert.Contains(result.ValidationDiagnostics, error => error.Code == "C2M-FV-006");
    }

    private static RelationFact[] Detect(
        IReadOnlyList<string> projectReferences,
        IReadOnlyList<string> packageReferences,
        bool includeContracts)
    {
        var orders = OrdersProject([OrdersTargetId]);
        var target = Target(OrdersTargetId, OrdersProjectId, packageReferences, projectReferences);

        var projects = new List<ProjectFact> { orders };
        var targetInputs = new List<TargetAnalysisIndexInput> { new(target, [], []) };
        if (includeContracts)
        {
            var contracts = new ProjectFact(
                Header(ContractsProjectId.ToFactId(), FactKind.Project),
                ContractsProjectId,
                "Acme.Shared.Contracts",
                "src/Acme.Shared.Contracts/Acme.Shared.Contracts.csproj",
                [ContractsTargetId],
                []);
            projects.Add(contracts);
            targetInputs.Add(new TargetAnalysisIndexInput(
                Target(ContractsTargetId, ContractsProjectId, [], []), [], []));
        }

        var index = SolutionAnalysisIndex.Build(projects, targetInputs);
        var context = new Csharp2Md.Core.Detection.Contracts.ProjectDetectionContext(orders, [target], [], index);

        return new DetectorHost(projectDetectors: [new CompileTimeReferenceDetector()])
            .DetectProject(context)
            .Facts
            .OfType<RelationFact>()
            .ToArray();
    }

    private static ProjectFact OrdersProject(ImmutableArray<TargetFactId> targetIds) => new(
        Header(OrdersProjectId.ToFactId(), FactKind.Project),
        OrdersProjectId,
        "Acme.Orders",
        "src/Acme.Orders/Acme.Orders.csproj",
        targetIds,
        []);

    private static TargetFact Target(
        TargetFactId targetId,
        ProjectFactId projectId,
        IReadOnlyList<string> packageReferences,
        IReadOnlyList<string> projectReferences) => new(
        Header(targetId.ToFactId(), FactKind.Target),
        targetId,
        projectId,
        "net10.0",
        new TargetEvaluationDetails(
            "Exe",
            "App",
            "App",
            [],
            [.. projectReferences],
            [.. packageReferences],
            [],
            [],
            "latest",
            "enable",
            []));

    private static FactHeader Header(FactId id, FactKind kind) =>
        FactHeader.Create(id, kind, FactResolution.Exact);
}
