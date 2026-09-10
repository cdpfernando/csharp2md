using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Fixtures;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Pipeline;

/// <summary>
/// GCPC-002, GCPC-003, GCPC-005: the entry-point and linked-call coverage metrics computed by
/// <see cref="ValidationAndCoverageStage"/>, over the certification corpus.
/// </summary>
public sealed class ValidationAndCoverageStageTests
{
    [Fact]
    [Trait("Requirement", "GCPC-003")]
    public async Task ExecuteAsync_CertificationCorpus_EntryPointDenominatorMatchesIndependentRecount()
    {
        var snapshot = await RunThroughValidationAndCoverageAsync(CertificationCorpusPaths.SolutionPath);
        var metric = snapshot.Coverage!.EntryPointCoverage;

        var independentDenominator = IndependentEntryPointDenominator(snapshot);

        Assert.True(independentDenominator > 0);
        Assert.Equal(CoverageMetricState.Evaluated, metric.State);
        Assert.Equal(independentDenominator, metric.Denominator);
    }

    [Fact]
    [Trait("Requirement", "GCPC-002")]
    public async Task ExecuteAsync_CertificationCorpus_EntryPointNumeratorMatchesPublishedEntryPointFacts()
    {
        var snapshot = await RunThroughValidationAndCoverageAsync(CertificationCorpusPaths.SolutionPath);

        var independentNumerator = snapshot.Facts.OfType<EntryPoint>().Count();

        Assert.True(independentNumerator > 0);
        Assert.Equal(independentNumerator, snapshot.Coverage!.EntryPointCoverage.Numerator);
    }

    [Fact]
    [Trait("Requirement", "GCPC-002")]
    public async Task ExecuteAsync_CertificationCorpus_EntryPointAccounting_NeverExceedsDenominator()
    {
        var snapshot = await RunThroughValidationAndCoverageAsync(CertificationCorpusPaths.SolutionPath);
        var metric = snapshot.Coverage!.EntryPointCoverage;

        Assert.True(metric.Numerator + metric.Exclusions + metric.Unknowns <= metric.Denominator);
    }

    [Fact]
    [Trait("Requirement", "GCPC-003")]
    public async Task ExecuteAsync_CertificationCorpus_LinkedCallDenominatorMatchesIndependentRecount()
    {
        var snapshot = await RunThroughValidationAndCoverageAsync(CertificationCorpusPaths.SolutionPath);
        var metric = snapshot.Coverage!.LinkedCallCoverage;

        // Independently re-derived: every recognized invocation occurrence is exactly one Invocation or
        // ObjectCreation observation (GCPC-011), read directly off the accumulated snapshot rather than
        // through any classifier's internal state.
        var independentDenominator = snapshot.Observations
            .Count(o => o.Identity.Kind is ObservationKind.Invocation or ObservationKind.ObjectCreation);

        Assert.True(independentDenominator > 0);
        Assert.Equal(CoverageMetricState.Evaluated, metric.State);
        Assert.Equal(independentDenominator, metric.Denominator);
    }

    [Fact]
    [Trait("Requirement", "GCPC-002")]
    [Trait("Requirement", "GCPC-014")]
    public async Task ExecuteAsync_CertificationCorpus_LinkedCallNumeratorMatchesDistinctConfirmedOccurrences()
    {
        var snapshot = await RunThroughValidationAndCoverageAsync(CertificationCorpusPaths.SolutionPath);
        var metric = snapshot.Coverage!.LinkedCallCoverage;

        var independentNumerator = snapshot.ConfirmedRelations
            .Where(r => r.Kind is RelationKind.Invokes)
            .SelectMany(r => r.DerivedFrom.DerivedFrom)
            .Distinct()
            .Count();

        Assert.True(independentNumerator > 0);
        Assert.Equal(independentNumerator, metric.Numerator);
    }

    [Fact]
    [Trait("Requirement", "GCPC-002")]
    public async Task ExecuteAsync_CertificationCorpus_LinkedCallAccounting_NeverExceedsDenominator()
    {
        var snapshot = await RunThroughValidationAndCoverageAsync(CertificationCorpusPaths.SolutionPath);
        var metric = snapshot.Coverage!.LinkedCallCoverage;

        Assert.True(metric.Numerator + metric.Exclusions + metric.Unknowns <= metric.Denominator);
    }

    [Fact]
    [Trait("Requirement", "GCPC-005")]
    public void CoverageMetric_PublishesNoRecallOrPrecisionField()
    {
        var forbidden = new[] { "recall", "precision", "accuracy" };

        var properties = typeof(CoverageMetric).GetProperties().Select(p => p.Name.ToLowerInvariant());

        Assert.DoesNotContain(properties, name => forbidden.Any(name.Contains));
    }

    [Fact]
    [Trait("Requirement", "GCPC-005")]
    public void CoverageReport_PublishesNoRecallOrPrecisionField()
    {
        var forbidden = new[] { "recall", "precision", "accuracy" };

        var properties = typeof(CoverageReport).GetProperties().Select(p => p.Name.ToLowerInvariant());

        Assert.DoesNotContain(properties, name => forbidden.Any(name.Contains));
    }

    /// <summary>
    /// Independent re-derivation of entry_point_coverage's denominator: every reachable, non-constructor
    /// callable declared on a type that mechanically looks like an ASP.NET controller (derives from
    /// <c>ControllerBase</c>, or has a member with a route declaration) or a messaging handler (derives
    /// from <c>IIntegrationEventHandler</c>, or has a <c>HandleAsync</c> member) -- written independently
    /// of <see cref="ValidationAndCoverageStage"/>'s own (private) implementation.
    /// </summary>
    private static int IndependentEntryPointDenominator(FactualSnapshot snapshot)
    {
        var symbols = snapshot.Facts.OfType<Symbol>().ToArray();
        var types = symbols.Where(s => ReadField(s.Signature.Value, "kind") == "namedtype").ToArray();
        var methods = symbols
            .Where(s => ReadField(s.Signature.Value, "kind") == "method"
                && s.Facets.Facets.Contains(SymbolFacet.Callable)
                && ReadField(s.Signature.Value, "metadata") is not (".ctor" or ".cctor"))
            .ToArray();
        var observationsByOwner = snapshot.Observations.ToLookup(o => o.Identity.Owner.Id.Value, StringComparer.Ordinal);

        bool IsDeclaredOn(Symbol method, Symbol type)
        {
            var container = ReadField(method.Signature.Value, "container");
            var typeName = ReadField(type.Signature.Value, "type");
            if (container == typeName)
            {
                return true;
            }

            var typeContainer = ReadField(type.Signature.Value, "container");
            var typeMetadata = ReadField(type.Signature.Value, "metadata");
            return typeContainer is not null && typeMetadata is not null
                && container == typeContainer + "." + typeMetadata;
        }

        bool HasTargetType(Observation o, string needle) =>
            o.Identity.Payload.Entries.Any(e => e.Key == "target-type" && e.Value.Value.Contains(needle, StringComparison.Ordinal));

        var controllerTypes = new HashSet<string>(StringComparer.Ordinal);
        var handlerTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            var bases = observationsByOwner[type.Reference.Id.Value].Where(o => o.Identity.Kind is ObservationKind.BaseType).ToArray();
            if (bases.Length == 0)
            {
                continue;
            }

            if (bases.Any(b => HasTargetType(b, "Microsoft.AspNetCore.Mvc.ControllerBase"))
                || methods.Any(m => IsDeclaredOn(m, type)
                    && observationsByOwner[m.Reference.Id.Value].Any(o => o.Identity.Kind is ObservationKind.RouteDeclaration)))
            {
                controllerTypes.Add(type.Reference.Id.Value);
            }

            if (bases.Any(b => HasTargetType(b, "IIntegrationEventHandler"))
                || methods.Any(m => IsDeclaredOn(m, type) && ReadField(m.Signature.Value, "metadata") == "HandleAsync"))
            {
                handlerTypes.Add(type.Reference.Id.Value);
            }
        }

        var denominator = 0;
        foreach (var method in methods)
        {
            if (!method.Facets.Facets.Contains(SymbolFacet.ExternallyReachable))
            {
                continue;
            }

            var declaringType = types.FirstOrDefault(t => IsDeclaredOn(method, t));
            if (declaringType is null)
            {
                continue;
            }

            var isController = controllerTypes.Contains(declaringType.Reference.Id.Value);
            var isHandler = handlerTypes.Contains(declaringType.Reference.Id.Value)
                && ReadField(method.Signature.Value, "metadata") == "HandleAsync";
            if (isController || isHandler)
            {
                denominator++;
            }
        }

        return denominator;
    }

    private static string? ReadField(string identity, string key)
    {
        var marker = ";" + key + "=";
        var start = identity.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = identity.IndexOf(';', start);
        var encoded = end < 0 ? identity[start..] : identity[start..end];
        return encoded.Length == 0 || encoded == "-" ? null : Uri.UnescapeDataString(encoded);
    }

    private static async Task<FactualSnapshot> RunThroughValidationAndCoverageAsync(string solutionPath)
    {
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        foreach (var stage in PipelineStages.CreateDefault().Take(5))
        {
            await stage.ExecuteAsync(context, CancellationToken.None);
        }

        return context.Accumulator.ToSnapshot();
    }
}
