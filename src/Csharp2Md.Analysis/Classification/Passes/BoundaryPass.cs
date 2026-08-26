using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class BoundaryPass : IClassifierPass
{
    internal const string RouteKey = "route";

    internal static ClassifierIdentity HttpInboundIdentity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.http-inbound", 1);

    public string Name => "Boundaries";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var factCount = ClassifyHttpInbound(context);
        return new ClassifierPassResult(factCount, 0, 0, 0);
    }

    private static int ClassifyHttpInbound(ClassifierContext context)
    {
        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var componentsById = context.FactsByType<Component>()
            .ToDictionary(static component => component.Reference.Id.Value, StringComparer.Ordinal);

        var factCount = 0;
        foreach (var entry in context.FactsByType<EntryPoint>().OrderBy(static e => e.Reference.Id.Value, StringComparer.Ordinal))
        {
            if (!symbolsById.TryGetValue(entry.Symbol.Id.Value, out var symbol)
                || !componentsById.TryGetValue(entry.OwningComponent.Id.Value, out var component))
            {
                continue;
            }

            var routeDeclaration = context.ObservationsByOwner(symbol.Reference)
                .FirstOrDefault(static observation => observation.Identity.Kind is ObservationKind.RouteDeclaration);
            if (routeDeclaration is null)
            {
                continue;
            }

            var template = ReadPayloadValue(routeDeclaration, RouteKey);
            if (template is null)
            {
                // SPEC_DEVIATION: spec EBC-06 empty template → empty-string protocolOperationKey.
                // Reason: StructuralLiteral.Create rejects empty canonical text, matching the
                // design missing-field rule (skip + diagnostic).
                context.Accumulator.AddDiagnostic(
                    new DiagnosticRecord(
                        "missing-route-template",
                        $"RouteDeclaration on '{symbol.Reference.Id.Value}' has no route template.",
                        symbol.Reference.Id.Value));
                continue;
            }

            var protocolOperationKey = StructuralLiteral.Create(LiteralRole.ProtocolName, template, "protocol-operation-key");
            context.Accumulator.AddFact(
                BoundaryOperation.Create(
                    symbol.Reference,
                    component.Reference,
                    BoundaryDirection.Inbound,
                    BoundaryProtocol.Http,
                    protocolOperationKey: protocolOperationKey));
            factCount++;
        }

        return factCount;
    }

    private static string? ReadPayloadValue(Observation observation, string key)
    {
        foreach (var entry in observation.Identity.Payload.Entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(entry.Value.Value))
            {
                return entry.Value.Value;
            }
        }

        return null;
    }
}
