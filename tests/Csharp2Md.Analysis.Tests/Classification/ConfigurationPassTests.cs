using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Configuration;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ConfigurationPassTests
{
    [Fact]
    [Trait("Requirement", "CDC-55")]
    public void Name_IsConfiguration()
    {
        Assert.Equal("Configuration", new ConfigurationPass().Name);
    }

    [Fact]
    [Trait("Requirement", "CDC-55")]
    public void Execute_IsBuilderThenEmitterWithNoClassificationLogicOfItsOwn()
    {
        var source = File.ReadAllText(ConfigurationPassPath());

        Assert.Contains("ConfigurationModelBuilder.Build(context)", source, StringComparison.Ordinal);
        Assert.Contains("ConfigurationEmitter.Emit(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigurationBinding.Create", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmedRelation.Create", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CDC-55")]
    public void Execute_EmptyLedger_ReturnsZeroWithoutThrowing()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln")
        {
            AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")],
        };

        var result = new ConfigurationPass().Execute(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Equal(0, result.RelationCount);
        Assert.Equal(0, result.CandidateCount);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
    }

    [Fact]
    [Trait("Requirement", "CDC-55")]
    public void Execute_HandBuiltLedger_ReportsEmitterCountsAndEmitsBinding()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln")
        {
            AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")],
        };
        pipeline.Accumulator.AddFact(Solution.Create(SolutionId));
        var project = Project.Create(ProjectId.Create(SolutionId, "Acme.Orders/Acme.Orders.csproj"));
        pipeline.Accumulator.AddFact(project);
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Orders.Program", "ConfigureHost", 1, "global::Microsoft.AspNetCore.Builder.WebApplicationBuilder"),
            project.Id,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        var component = Component.Create(SolutionId, "Acme.Orders/Acme.Orders.csproj", [symbol.Reference]);
        pipeline.Accumulator.AddFact(component);
        var document = Document.Create(project.Id, "Acme.Orders/appsettings.json");
        pipeline.Accumulator.AddFact(document);
        pipeline.Accumulator.AddObservation(
            Observation.Create(
                document.Reference,
                ObservationKind.Configuration,
                NormalizedPayload.Create(
                [
                    new PayloadEntry("key", StructuralLiteral.Create(LiteralRole.ConfigurationKey, "Services:PaymentService", "key")),
                    new PayloadEntry("resolution", StructuralLiteral.Create(LiteralRole.ConfigurationKey, "literal", "resolution")),
                    new PayloadEntry("address", StructuralLiteral.Create(LiteralRole.ConfigurationKey, "https://payments.internal.acme.local:8443", "address")),
                ]),
                1,
                new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/appsettings.json", new SourceSpan(1, 1, 1, 8)),
                EvidenceMethod.Configured,
                new BindingDiagnostic("configured", "configured"),
                DocumentHash.Create(new string('a', 64)),
                new ExtractorVersion(1)));

        var result = new ConfigurationPass().Execute(new ClassifierContext(pipeline), CancellationToken.None);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        var binding = Assert.Single(snapshot.Facts.OfType<ConfigurationBinding>());
        Assert.Equal(component.Reference, binding.BoundFact);
        Assert.Equal("Services:PaymentService", binding.ConfigurationKey.Value);
        var configured = snapshot.ConfirmedRelations.Where(relation => relation.Kind is RelationKind.ConfiguredBy).ToArray();
        Assert.Equal(configured.Length, result.RelationCount);
        Assert.Equal(1, result.FactCount);
        Assert.Equal(0, result.CandidateCount);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.All(configured, relation => Assert.Equal(ConfigurationEmitter.Identity, relation.Classifier));
        Assert.Equal("csharp2md.classifier.configuration", ConfigurationEmitter.Identity.Id);
        Assert.Equal(1, ConfigurationEmitter.Identity.Version);
    }

    private static SolutionId SolutionId =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static string ConfigurationPassPath() =>
        Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Classification",
            "Passes",
            "ConfigurationPass.cs");
}
