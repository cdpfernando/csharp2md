using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class EntryPointPass : IClassifierPass
{
    internal const string TargetTypeKey = "target-type";
    internal const string ControllerBaseTypeName = "Microsoft.AspNetCore.Mvc.ControllerBase";

    internal static ClassifierIdentity Identity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.entrypoint", 1);

    public string Name => "Entry points";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var symbols = context.FactsByType<Symbol>();
        var typesById = symbols
            .Where(static symbol => string.Equals(ReadField(symbol.Signature.Value, "kind"), "namedtype", StringComparison.Ordinal))
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var methods = symbols
            .Where(static symbol =>
                string.Equals(ReadField(symbol.Signature.Value, "kind"), "method", StringComparison.Ordinal)
                && symbol.Facets.Facets.Contains(SymbolFacet.Callable)
                && ReadField(symbol.Signature.Value, "metadata") is not (".ctor" or ".cctor"))
            .ToArray();

        var controllerTypes = new HashSet<string>(StringComparer.Ordinal);
        var handlerTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typesById.Values)
        {
            var bases = context.ObservationsByOwner(type.Reference)
                .Where(static observation => observation.Identity.Kind is ObservationKind.BaseType)
                .ToArray();
            if (bases.Length == 0)
            {
                continue;
            }

            if (bases.Any(static observation => PayloadContains(observation, ControllerBaseTypeName))
                || methods.Any(method => IsDeclaredOn(method, type) && HasRouteDeclaration(context, method)))
            {
                controllerTypes.Add(type.Reference.Id.Value);
            }

            if (bases.Any(static observation => PayloadContains(observation, "IIntegrationEventHandler"))
                || methods.Any(method => IsDeclaredOn(method, type) && IsHandleAsync(method)))
            {
                handlerTypes.Add(type.Reference.Id.Value);
            }
        }

        var factCount = 0;
        var unresolvedCount = 0;
        foreach (var method in methods.OrderBy(static method => method.Reference.Id.Value, StringComparer.Ordinal))
        {
            // GCPC-020: a declaration that is not externally reachable never becomes an EntryPoint,
            // regardless of its declaring type -- this is what stops a private controller helper
            // (e.g. CatalogController.ChangeUriPlaceholder) from being promoted.
            if (!method.Facets.Facets.Contains(SymbolFacet.ExternallyReachable))
            {
                continue;
            }

            var declaringType = FindDeclaringType(method, typesById.Values);
            if (declaringType is null)
            {
                continue;
            }

            var isControllerAction = controllerTypes.Contains(declaringType.Reference.Id.Value);
            var isHandler = handlerTypes.Contains(declaringType.Reference.Id.Value) && IsHandleAsync(method);
            if (!isControllerAction && !isHandler)
            {
                // GCPC-021: a reachable helper on a framework-recognized type but with no framework
                // entry evidence of its own is not an EntryPoint candidate at all.
                continue;
            }

            if (context.ComponentForSymbol(method.Reference) is not { } component)
            {
                // GCPC-024: entry capability is otherwise positively indicated (reachable, and either
                // a controller action or a handler dispatch method), but the owning component cannot
                // be determined -- publish unresolved instead of silently dropping the callable.
                if (TryPublishUndeterminedCapability(context, method))
                {
                    unresolvedCount++;
                }

                continue;
            }

            context.Accumulator.AddFact(EntryPoint.Create(method.Reference, component.Reference));
            factCount++;

            if (isControllerAction && !HasRouteDeclaration(context, method))
            {
                var metadata = ReadField(method.Signature.Value, "metadata") ?? method.Reference.Id.Value;
                context.Accumulator.AddDiagnostic(
                    new DiagnosticRecord(
                        "missing-route-declaration",
                        $"Controller action '{metadata}' ({method.Reference.Id.Value}) has no RouteDeclaration.",
                        method.Reference.Id.Value));
            }
        }

        return new ClassifierPassResult(factCount, 0, 0, unresolvedCount);
    }

    /// <summary>
    /// GCPC-024: a callable whose entry capability cannot be determined is published as an unresolved
    /// record instead of a confirmed <see cref="EntryPoint"/>. When the callable carries no observation
    /// at all there is nothing to cite as available evidence, so no record is published for it either --
    /// <see cref="EvidenceChain.Create"/> requires at least one.
    /// </summary>
    private static bool TryPublishUndeterminedCapability(ClassifierContext context, Symbol method)
    {
        var identities = context.ObservationsByOwner(method.Reference)
            .Select(static observation => observation.Identity)
            .ToArray();
        if (identities.Length == 0)
        {
            return false;
        }

        context.Accumulator.AddUnresolved(
            UnresolvedRecord.Create(
                RelationKind.Executes,
                method.Reference,
                UnresolvedCause.NoCandidateFound,
                EvidenceChain.Create(identities)));
        return true;
    }

    private static bool IsHandleAsync(Symbol method) =>
        string.Equals(ReadField(method.Signature.Value, "metadata"), "HandleAsync", StringComparison.Ordinal);

    private static bool HasRouteDeclaration(ClassifierContext context, Symbol method) =>
        context.ObservationsByOwner(method.Reference)
            .Any(static observation => observation.Identity.Kind is ObservationKind.RouteDeclaration);

    private static bool IsDeclaredOn(Symbol method, Symbol type)
    {
        var container = ReadField(method.Signature.Value, "container");
        if (container is null)
        {
            return false;
        }

        var typeName = ReadField(type.Signature.Value, "type");
        if (string.Equals(container, typeName, StringComparison.Ordinal))
        {
            return true;
        }

        var typeContainer = ReadField(type.Signature.Value, "container");
        var typeMetadata = ReadField(type.Signature.Value, "metadata");
        return typeContainer is not null
            && typeMetadata is not null
            && string.Equals(container, typeContainer + "." + typeMetadata, StringComparison.Ordinal);
    }

    private static Symbol? FindDeclaringType(Symbol method, IEnumerable<Symbol> types) =>
        types.FirstOrDefault(type => IsDeclaredOn(method, type));

    private static bool PayloadContains(Observation observation, string needle)
    {
        foreach (var entry in observation.Identity.Payload.Entries)
        {
            if (string.Equals(entry.Key, TargetTypeKey, StringComparison.Ordinal)
                && entry.Value.Value.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
}
