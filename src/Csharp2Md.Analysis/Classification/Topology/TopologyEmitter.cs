using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Topology;

internal static class TopologyEmitter
{
    internal static ClassifierIdentity Identity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.component-topology", 1);

    private static readonly FacetBinding EmptyFacets =
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    public static ClassifierPassResult Emit(TopologyModel model, ClassifierContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);

        var factCount = 0;
        var relationCount = 0;
        var unresolvedCount = 0;
        var components = new Dictionary<string, Component>(StringComparer.Ordinal);
        foreach (var group in model.Groups)
        {
            var owners = OwnersFor(group, context);
            var component = Component.Create(context.SolutionId, group.ComponentName, owners.Select(symbol => symbol.Reference));
            context.Accumulator.AddFact(component);
            factCount++;
            components[group.ComponentName] = component;
            foreach (var symbol in owners)
            {
                context.Accumulator.AddRelation(
                    ConfirmedRelation.Create(
                        RelationKind.BelongsTo,
                        symbol.Reference,
                        component.Reference,
                        EmptyFacets,
                        EvidenceChain.Create(context.ObservationsByOwner(symbol.Reference).Select(static observation => observation.Identity)),
                        Identity,
                        context.AnalysisVariants,
                        EvidenceMethod.Semantic,
                        sourceFact: symbol,
                        targetFact: component));
                relationCount++;
            }
        }

        var deployments = new Dictionary<string, DeploymentUnit>(StringComparer.Ordinal);
        foreach (var node in model.Deployments)
        {
            var unit = DeploymentUnit.Create(context.SolutionId, node.Name);
            context.Accumulator.AddFact(unit);
            factCount++;
            deployments[node.Name] = unit;
        }

        foreach (var edge in model.Inclusions)
        {
            if (!components.TryGetValue(edge.ComponentName, out var component)
                || !deployments.TryGetValue(edge.DeploymentName, out var unit))
            {
                continue;
            }

            context.Accumulator.AddRelation(
                ConfirmedRelation.Create(
                    RelationKind.IncludedIn,
                    component.Reference,
                    unit.Reference,
                    EmptyFacets,
                    edge.Evidence,
                    Identity,
                    context.AnalysisVariants,
                    EvidenceMethod.Configured,
                    sourceFact: component,
                    targetFact: unit));
            relationCount++;
        }

        foreach (var node in model.Unreached)
        {
            if (!components.TryGetValue(node.ComponentName, out var component))
            {
                continue;
            }

            context.Accumulator.AddUnresolved(
                UnresolvedRecord.Create(
                    RelationKind.IncludedIn,
                    component.Reference,
                    UnresolvedCause.InsufficientEvidence,
                    node.Evidence));
            unresolvedCount++;
        }

        EmitCoverageDiagnostic(context, model, components);
        return new ClassifierPassResult(factCount, relationCount, 0, unresolvedCount);
    }

    private static void EmitCoverageDiagnostic(
        ClassifierContext context,
        TopologyModel model,
        Dictionary<string, Component> components)
    {
        var identityOrKey = components.Count > 0
            ? components.Values.OrderBy(static component => component.Reference.Id.Value, StringComparer.Ordinal).First().Reference.Id.Value
            : context.FactsByType<Solution>().SingleOrDefault()?.Reference.Id.Value;
        var unreachedIds = string.Join(
            ", ",
            model.Unreached
                .Select(node => components.TryGetValue(node.ComponentName, out var component) ? component.Name : null)
                .Where(static name => name is not null)
                .Cast<string>()
                .OrderBy(static name => name, StringComparer.Ordinal));
        var message =
            $"Projects grouped: {model.Coverage.ProjectsGrouped}; " +
            $"applications found: {model.Coverage.ApplicationsFound}; " +
            $"components with no deployment unit: {model.Coverage.ComponentsWithoutDeployment}; " +
            $"unreached component ids: {unreachedIds}";
        context.Accumulator.AddDiagnostic(new DiagnosticRecord("component-coverage", message, identityOrKey));
    }

    private static Symbol[] OwnersFor(ComponentGroup group, ClassifierContext context)
    {
        var projectIds = group.Projects.Select(static id => id.Value).ToHashSet(StringComparer.Ordinal);
        return context.FactsByType<Symbol>()
            .Where(symbol => projectIds.Contains(symbol.OwningProject.Value)
                && context.ObservationsByOwner(symbol.Reference).Count > 0)
            .OrderBy(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
