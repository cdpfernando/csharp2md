using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class ContractPass : IClassifierPass
{
    internal const string PayloadRoleRequest = "request";

    internal static ClassifierIdentity Identity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.contract-messaging", 1);

    public string Name => "Contracts";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var symbols = symbolsById.Values.ToArray();
        var factCount = 0;

        var groups = context.FactsByType<BoundaryOperation>()
            .Where(static operation =>
                operation.Protocol is BoundaryProtocol.Messaging
                && operation.ProtocolOperationKey is not null)
            .GroupBy(static operation => operation.ProtocolOperationKey!.Value.Value, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var outbound = group
                .Where(static operation => operation.Direction is BoundaryDirection.Outbound)
                .OrderBy(static operation => operation.Reference.Id.Value, StringComparer.Ordinal)
                .ToArray();
            var inbound = group
                .Where(static operation => operation.Direction is BoundaryDirection.Inbound)
                .OrderBy(static operation => operation.Reference.Id.Value, StringComparer.Ordinal)
                .ToArray();
            if (inbound.Length == 0)
            {
                continue;
            }

            var eventTypeName = group.Key;
            if (IsAnonymousTypeName(eventTypeName))
            {
                continue;
            }

            var eventType = FindNamedType(symbols, eventTypeName);
            if (eventType is null)
            {
                continue;
            }

            if (outbound.Length == 0)
            {
                if (AllCallablesInProject(inbound, symbolsById, eventType.OwningProject.Value))
                {
                    continue;
                }
            }
            else if (!IsSharedAcrossProjects(eventType, outbound, inbound, symbolsById))
            {
                continue;
            }

            var contract = Contract.Create(
                StructuralLiteral.Create(LiteralRole.ProtocolName, eventTypeName, "protocol-name"));
            context.Accumulator.AddFact(contract);
            factCount++;

            foreach (var operation in outbound.Concat(inbound).OrderBy(static op => op.Reference.Id.Value, StringComparer.Ordinal))
            {
                context.Accumulator.AddFact(
                    ContractBinding.Create(
                        operation.Reference,
                        PayloadRoleRequest,
                        eventType.Reference,
                        contract.Reference));
                factCount++;
            }

            context.Accumulator.AddFact(
                ContractRevision.Create(contract.Reference, Fingerprint(eventType, symbols)));
            factCount++;
        }

        return new ClassifierPassResult(factCount, 0, 0, 0);
    }

    private static bool IsAnonymousTypeName(string fullyQualifiedName) =>
        fullyQualifiedName.Contains("<>", StringComparison.Ordinal)
        || fullyQualifiedName.Contains("AnonymousType", StringComparison.Ordinal);

    private static Symbol? FindNamedType(IReadOnlyList<Symbol> symbols, string fullyQualifiedName)
    {
        foreach (var symbol in symbols)
        {
            if (!string.Equals(ReadField(symbol.Signature.Value, "kind"), "namedtype", StringComparison.Ordinal))
            {
                continue;
            }

            var typeName = ReadField(symbol.Signature.Value, "type");
            if (string.Equals(typeName, fullyQualifiedName, StringComparison.Ordinal))
            {
                return symbol;
            }
        }

        return null;
    }

    private static bool IsSharedAcrossProjects(
        Symbol eventType,
        IReadOnlyList<BoundaryOperation> outbound,
        IReadOnlyList<BoundaryOperation> inbound,
        IReadOnlyDictionary<string, Symbol> symbolsById)
    {
        var typeProject = eventType.OwningProject.Value;
        return !AllCallablesInProject(outbound, symbolsById, typeProject)
            || !AllCallablesInProject(inbound, symbolsById, typeProject);
    }

    private static bool AllCallablesInProject(
        IReadOnlyList<BoundaryOperation> operations,
        IReadOnlyDictionary<string, Symbol> symbolsById,
        string projectId)
    {
        foreach (var operation in operations)
        {
            if (!symbolsById.TryGetValue(operation.Symbol.Id.Value, out var callable)
                || !string.Equals(callable.OwningProject.Value, projectId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return operations.Count > 0;
    }

    private static string Fingerprint(Symbol eventType, IReadOnlyList<Symbol> symbols)
    {
        var typeName = ReadField(eventType.Signature.Value, "type");
        if (typeName is null)
        {
            return eventType.Reference.Id.Value;
        }

        var parts = symbols
            .Where(symbol =>
                string.Equals(ReadField(symbol.Signature.Value, "kind"), "property", StringComparison.Ordinal)
                && string.Equals(ReadField(symbol.Signature.Value, "container"), typeName, StringComparison.Ordinal))
            .Select(symbol =>
            {
                var metadata = ReadField(symbol.Signature.Value, "metadata") ?? string.Empty;
                var propertyType = ReadField(symbol.Signature.Value, "type") ?? string.Empty;
                return metadata + ":" + propertyType;
            })
            .OrderBy(static part => part, StringComparer.Ordinal)
            .ToArray();

        return parts.Length == 0 ? typeName : string.Join('|', parts);
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
