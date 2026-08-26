using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class ComponentPass : IClassifierPass
{
    private static readonly ObservationKind[] CandidateKinds =
    [
        ObservationKind.BaseType,
        ObservationKind.AttributeUsage,
        ObservationKind.RouteDeclaration,
        ObservationKind.MessageOperation,
    ];

    public string Name => "Components";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var projectsById = context.FactsByType<Project>()
            .ToDictionary(static project => project.Id.Value, StringComparer.Ordinal);
        var ownersByProject = new Dictionary<string, HashSet<FactReference>>(StringComparer.Ordinal);

        foreach (var kind in CandidateKinds)
        {
            foreach (var observation in context.ObservationsByKind(kind))
            {
                if (!symbolsById.TryGetValue(observation.Identity.Owner.Id.Value, out var symbol)
                    || !projectsById.ContainsKey(symbol.OwningProject.Value))
                {
                    continue;
                }

                if (!ownersByProject.TryGetValue(symbol.OwningProject.Value, out var owners))
                {
                    owners = [];
                    ownersByProject[symbol.OwningProject.Value] = owners;
                }

                owners.Add(symbol.Reference);
            }
        }

        var factCount = 0;
        foreach (var project in projectsById.Values.OrderBy(static project => project.Id.Value, StringComparer.Ordinal))
        {
            if (!ownersByProject.TryGetValue(project.Id.Value, out var owners))
            {
                continue;
            }

            var path = TryLogicalPath(project);
            if (path is null)
            {
                continue;
            }

            context.Accumulator.AddFact(Component.Create(context.SolutionId, path, owners));
            factCount++;
        }

        return new ClassifierPassResult(factCount, 0, 0, 0);
    }

    private static string? TryLogicalPath(Project project)
    {
        const string marker = ";path=";
        var id = project.Id.Value;
        var start = id.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = id.IndexOf(';', start);
        var encoded = end < 0 ? id[start..] : id[start..end];
        return string.IsNullOrWhiteSpace(encoded) ? null : Uri.UnescapeDataString(encoded);
    }
}
