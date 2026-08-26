using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Configuration;

internal static class ConfigurationEmitter
{
    internal static ClassifierIdentity Identity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.configuration", 1);

    private static readonly FacetBinding EmptyFacets =
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    public static ClassifierPassResult Emit(ConfigurationModel model, ClassifierContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);

        var bindings = new Dictionary<string, ConfigurationBinding>(StringComparer.Ordinal);
        var factCount = 0;
        foreach (var key in model.Keys
            .OrderBy(static key => key.OwningComponent.Id.Value, StringComparer.Ordinal)
            .ThenBy(static key => key.KeyPath, StringComparer.Ordinal))
        {
            var binding = BindingOf(key.OwningComponent, key.KeyPath);
            context.Accumulator.AddFact(binding);
            if (bindings.TryAdd(binding.Reference.Id.Value, binding))
            {
                factCount++;
            }
        }

        var relationCount = EmitConfiguredBy(model, context, bindings);
        relationCount += EmitTargets(model, context);
        var unresolvedCount = EmitUnbound(model, context);
        return new ClassifierPassResult(factCount, relationCount, 0, unresolvedCount);
    }

    private static int EmitConfiguredBy(
        ConfigurationModel model,
        ClassifierContext context,
        Dictionary<string, ConfigurationBinding> bindings)
    {
        if (context.AnalysisVariants.IsDefaultOrEmpty)
        {
            return 0;
        }

        var factsById = context.Facts
            .Concat(bindings.Values)
            .GroupBy(static fact => fact.Reference.Id.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var count = 0;
        foreach (var edge in model.Edges)
        {
            var owner = BoundComponent(edge, model.Keys);
            if (owner.Equals(default(FactReference)))
            {
                continue;
            }

            var binding = BindingOf(owner, edge.KeyPath);
            if (!bindings.TryGetValue(binding.Reference.Id.Value, out var target))
            {
                continue;
            }

            factsById.TryGetValue(edge.Source.Id.Value, out var sourceFact);
            context.Accumulator.AddRelation(
                ConfirmedRelation.Create(
                    RelationKind.ConfiguredBy,
                    edge.Source,
                    target.Reference,
                    EmptyFacets,
                    edge.Evidence,
                    Identity,
                    context.AnalysisVariants,
                    EvidenceMethod.Configured,
                    sourceFact: sourceFact,
                    targetFact: target));
            count++;
        }

        return count;
    }

    private static int EmitTargets(ConfigurationModel model, ClassifierContext context)
    {
        if (context.AnalysisVariants.IsDefaultOrEmpty)
        {
            return 0;
        }

        var factsById = context.Facts
            .GroupBy(static fact => fact.Reference.Id.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var count = 0;
        foreach (var decision in model.Targets)
        {
            switch (decision.Outcome)
            {
                case TargetOutcome.Promote:
                    if (!factsById.TryGetValue(decision.Candidate.ProposedTarget.Id.Value, out var targetFact))
                    {
                        break;
                    }

                    factsById.TryGetValue(decision.Candidate.Source.Id.Value, out var sourceFact);
                    context.Accumulator.AddRelation(
                        ConfirmedRelation.Create(
                            RelationKind.Targets,
                            decision.Candidate.Source,
                            decision.Candidate.ProposedTarget,
                            EmptyFacets,
                            decision.Evidence,
                            Identity,
                            context.AnalysisVariants,
                            EvidenceMethod.Configured,
                            sourceFact: sourceFact,
                            targetFact: targetFact));
                    context.Accumulator.RemoveCandidate(decision.Candidate);
                    count++;
                    break;
                case TargetOutcome.Frontier:
                    if (!decision.Candidate.DerivedFrom.DerivedFrom.IsDefaultOrEmpty)
                    {
                        context.Accumulator.AddOpenFrontier(
                            OpenFrontier.Create(
                                decision.Candidate.DerivedFrom.DerivedFrom[0],
                                FrontierCause.FurtherContinuationObserved));
                    }

                    break;
            }
        }

        return count;
    }

    private static int EmitUnbound(ConfigurationModel model, ClassifierContext context)
    {
        var count = 0;
        foreach (var read in model.UnboundReads)
        {
            context.Accumulator.AddUnresolved(
                UnresolvedRecord.Create(
                    RelationKind.ConfiguredBy,
                    read.Symbol,
                    UnresolvedCause.InsufficientEvidence,
                    EvidenceChain.Create([read.Evidence])));
            count++;
        }

        return count;
    }

    private static FactReference BoundComponent(ConfiguredEdge edge, ImmutableArray<DeclaredKey> keys)
    {
        if (string.Equals(edge.Source.FactType, nameof(Component), StringComparison.Ordinal))
        {
            return edge.Source;
        }

        foreach (var key in keys)
        {
            if (string.Equals(key.KeyPath, edge.KeyPath, StringComparison.Ordinal))
            {
                return key.OwningComponent;
            }
        }

        return default;
    }

    private static ConfigurationBinding BindingOf(FactReference component, string keyPath) =>
        ConfigurationBinding.Create(
            component,
            StructuralLiteral.Create(LiteralRole.ConfigurationKey, keyPath, "configurationKey"));
}
