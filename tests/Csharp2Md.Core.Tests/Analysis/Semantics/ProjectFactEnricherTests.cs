using System.Text;
using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Analysis.Semantics;

[Trait("Category", "Integration")]
public sealed class ProjectFactEnricherTests
{
    [Fact]
    public void HealthyEvaluation_PopulatesEveryRequiredProjectAndTargetField()
    {
        var result = ProjectFactEnricher.Enrich(SyntacticProject(), Evaluation(Target("net10.0")));

        Assert.Equal(FactResolution.Exact, result.Project.Header.Resolution);
        var project = Assert.IsType<ProjectEvaluationDetails>(result.Project.Evaluation);
        Assert.Equal("Microsoft.NET.Sdk", project.DeclaredSdk);
        Assert.Equal(["Imported.props"], project.EvaluatedImports.ToArray());
        Assert.Equal(["net10.0"], project.TargetFrameworks.ToArray());
        var target = Assert.IsType<TargetEvaluationDetails>(Assert.Single(result.Targets).Evaluation);
        Assert.Equal("Exe", target.OutputType);
        Assert.Equal("Acme.Net10", target.AssemblyName);
        Assert.Equal("Acme.Root", target.RootNamespace);
        Assert.Equal(["Program.cs"], target.CompileItems.ToArray());
        Assert.Equal(["Shared.csproj"], target.ProjectReferences.ToArray());
        Assert.Equal(["Example.Package"], target.PackageReferences.ToArray());
        Assert.Equal(["Example.Reference"], target.References.ToArray());
        Assert.Equal(["ALPHA", "BETA"], target.Constants.ToArray());
        Assert.Equal("preview", target.LanguageVersion);
        Assert.Equal("enable", target.NullableMode);
        Assert.Equal(["Generator.dll"], target.CompiledExtensions.ToArray());
    }

    [Fact]
    public void MultiTargetEvaluation_RetainsDistinctTargetFactsWithoutCollapsingValues()
    {
        var result = ProjectFactEnricher.Enrich(
            SyntacticProject(),
            Evaluation(Target("net9.0"), Target("net10.0")));

        Assert.Equal(2, result.Targets.Length);
        Assert.Equal(["net10.0", "net9.0"], result.Project.Evaluation!.TargetFrameworks.ToArray());
        Assert.Equal("Acme.Net10", result.Targets.Single(target => target.TargetFramework == "net10.0").Evaluation!.AssemblyName);
        Assert.Equal("Acme.Net9", result.Targets.Single(target => target.TargetFramework == "net9.0").Evaluation!.AssemblyName);
        Assert.NotEqual(result.Targets[0].TargetId, result.Targets[1].TargetId);
    }

    [Fact]
    public void FailedEvaluation_RetainsSyntacticProjectAndRecordsSecurityMetadata()
    {
        var diagnostic = EvaluationDiagnostic(ProjectId.ToFactId(), "C2M-EVAL-001");
        var evaluation = new ProjectEvaluationResult(
            ProjectId,
            "Missing.Sdk/1.0",
            [],
            [],
            [],
            [],
            [],
            [diagnostic],
            TimedOut: false);

        var result = ProjectFactEnricher.Enrich(SyntacticProject(), evaluation);

        Assert.Equal(FactResolution.Syntactic, result.Project.Header.Resolution);
        Assert.Equal("semantic", result.Project.Evaluation!.RequestedAnalysis);
        Assert.Equal(FactResolution.Syntactic, result.Project.Evaluation.EffectiveResolution);
        Assert.False(result.Project.Evaluation.RestorePerformed);
        Assert.Equal("none", result.Project.Evaluation.Isolation);
        Assert.Empty(result.Targets);
        Assert.Contains(diagnostic.Id, result.Project.Header.DiagnosticIds);
    }

    [Fact]
    public void MixedTargetEvaluation_IsPartialAndScopesFailureToAffectedTarget()
    {
        var failure = EvaluationDiagnostic(TargetFactId.Create(ProjectId, "net9.0").ToFactId(), "C2M-EVAL-002");
        var failed = Target("net9.0", succeeded: false, diagnostics: [failure]);

        var result = ProjectFactEnricher.Enrich(
            SyntacticProject(),
            Evaluation(Target("net10.0"), failed));

        Assert.Equal(FactResolution.Partial, result.Project.Header.Resolution);
        Assert.Equal(FactResolution.Exact, result.Targets.Single(target => target.TargetFramework == "net10.0").Header.Resolution);
        var failedFact = result.Targets.Single(target => target.TargetFramework == "net9.0");
        Assert.Equal(FactResolution.Syntactic, failedFact.Header.Resolution);
        Assert.Equal([failure.Id], failedFact.Header.DiagnosticIds.ToArray());
        Assert.Empty(failedFact.Evaluation!.CompileItems);
    }

    [Fact]
    public void EvaluatedCollections_AreCanonicalAndDeduplicatedWithinTheirTarget()
    {
        var target = Target("net10.0") with
        {
            Properties = Properties("net10.0").SetItem("DefineConstants", "BETA;ALPHA;BETA"),
            Items = Items().SetItem("Compile", [Item("Z.cs"), Item("A.cs"), Item("Z.cs")]),
        };

        var result = ProjectFactEnricher.Enrich(SyntacticProject(), Evaluation(target));

        var details = Assert.Single(result.Targets).Evaluation!;
        Assert.Equal(["A.cs", "Z.cs"], details.CompileItems.ToArray());
        Assert.Equal(["ALPHA", "BETA"], details.Constants.ToArray());
    }

    [Fact]
    public void Diagnostics_AreDeduplicatedAndReferencedByAffectedFacts()
    {
        var diagnostic = EvaluationDiagnostic(TargetFactId.Create(ProjectId, "net10.0").ToFactId(), "C2M-EVAL-002");
        var failed = Target("net10.0", succeeded: false, diagnostics: [diagnostic, diagnostic]);

        var result = ProjectFactEnricher.Enrich(SyntacticProject(), Evaluation(failed));

        Assert.Single(result.Diagnostics);
        Assert.Equal([diagnostic.Id], result.Project.Header.DiagnosticIds.ToArray());
        Assert.Equal([diagnostic.Id], Assert.Single(result.Targets).Header.DiagnosticIds.ToArray());
    }

    [Fact]
    public void EnrichedFacts_SerializeAllEvaluationFieldsThroughSchemaVersionTwoContract()
    {
        var result = ProjectFactEnricher.Enrich(SyntacticProject(), Evaluation(Target("net10.0")));
        var validation = FactValidator.Validate(FactValidationInput.Create(
            [result.Project, .. result.Targets],
            result.Diagnostics));
        var validated = Assert.IsType<ValidatedFactFragment>(validation.Fragment);

        var json = Encoding.UTF8.GetString(FactualJsonSerializer.Serialize(FactualJsonMapper.Map(validated)));

        Assert.Contains("\"declared_sdk\": \"Microsoft.NET.Sdk\"", json, StringComparison.Ordinal);
        Assert.Contains("\"requested_analysis\": \"semantic\"", json, StringComparison.Ordinal);
        Assert.Contains("\"restore_performed\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"assembly_name\": \"Acme.Net10\"", json, StringComparison.Ordinal);
        Assert.Contains("\"compiled_extensions\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MismatchedProjectIdentity_IsRejectedBeforeProducingFacts()
    {
        var evaluation = Evaluation(Target("net10.0")) with
        {
            ProjectId = ProjectFactId.Create("Other.csproj"),
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            ProjectFactEnricher.Enrich(SyntacticProject(), evaluation));

        Assert.Equal("Evaluation and syntactic project identities must match. (Parameter 'evaluation')", exception.Message);
    }

    private static ProjectFactId ProjectId { get; } = ProjectFactId.Create("Probe.csproj");

    private static ProjectFact SyntacticProject() => new(
        FactHeader.Create(
            ProjectId.ToFactId(),
            FactKind.Project,
            FactResolution.Syntactic,
            [new FactProvenance("csharp2md.syntax", "3.0.0")]),
        ProjectId,
        "Probe",
        "Probe.csproj",
        [],
        []);

    private static ProjectEvaluationResult Evaluation(params EvaluatedTarget[] targets) => new(
        ProjectId,
        "Microsoft.NET.Sdk",
        targets.Select(static target => target.TargetFramework).ToImmutableArray(),
        ["Imported.props"],
        ["Analyzer.dll", "Generator.dll"],
        ["Generator.dll"],
        targets.ToImmutableArray(),
        [],
        TimedOut: false);

    private static EvaluatedTarget Target(
        string targetFramework,
        bool succeeded = true,
        ImmutableArray<AnalysisDiagnostic> diagnostics = default) => new(
        TargetFactId.Create(ProjectId, targetFramework),
        targetFramework,
        succeeded,
        succeeded ? Properties(targetFramework) : ImmutableDictionary<string, string>.Empty,
        succeeded ? Items() : ImmutableDictionary<string, ImmutableArray<EvaluatedItem>>.Empty,
        diagnostics.IsDefault ? [] : diagnostics);

    private static ImmutableDictionary<string, string> Properties(string targetFramework) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TargetFramework"] = targetFramework,
            ["OutputType"] = "Exe",
            ["AssemblyName"] = targetFramework == "net10.0" ? "Acme.Net10" : "Acme.Net9",
            ["RootNamespace"] = "Acme.Root",
            ["DefineConstants"] = "ALPHA;BETA",
            ["LangVersion"] = "preview",
            ["Nullable"] = "enable",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static ImmutableDictionary<string, ImmutableArray<EvaluatedItem>> Items() =>
        new Dictionary<string, ImmutableArray<EvaluatedItem>>(StringComparer.Ordinal)
        {
            ["Compile"] = [Item("Program.cs")],
            ["ProjectReference"] = [Item("Shared.csproj")],
            ["PackageReference"] = [Item("Example.Package")],
            ["Reference"] = [Item("Example.Reference")],
            ["Analyzer"] = [Item("Generator.dll")],
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static EvaluatedItem Item(string identity) =>
        new(identity, ImmutableDictionary<string, string>.Empty);

    private static AnalysisDiagnostic EvaluationDiagnostic(FactId scopeId, string code) =>
        AnalysisDiagnostic.Create(
            code,
            DiagnosticSeverity.Warning,
            DiagnosticStage.Evaluation,
            scopeId,
            "Controlled evaluation degradation.");
}
