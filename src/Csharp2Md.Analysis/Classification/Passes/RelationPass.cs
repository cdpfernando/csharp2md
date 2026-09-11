using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class RelationPass : IClassifierPass
{
    private static readonly FacetBinding EmptyFacets =
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static readonly ImmutableArray<FacetAxisDescriptor> PayloadRoleAxes =
        TaxonomyTables.Default.FacetAxes.Add(
            new FacetAxisDescriptor("payload-role", TaxonomyTables.Default.PayloadRoles));

    public string Name => "Relations";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var relationCount = EmitImplementsOperation(context, symbolsById);
        relationCount += EmitUsesContract(context, symbolsById);
        var unresolvedCount = EmitUnresolved(context, symbolsById);
        return new ClassifierPassResult(0, relationCount, 0, unresolvedCount);
    }

    private static int EmitImplementsOperation(ClassifierContext context, IReadOnlyDictionary<string, Symbol> symbolsById)
    {
        if (context.AnalysisVariants.IsDefaultOrEmpty)
        {
            return 0;
        }

        var count = 0;
        foreach (var operation in context.FactsByType<BoundaryOperation>()
            .OrderBy(static operation => operation.Reference.Id.Value, StringComparer.Ordinal))
        {
            if (!symbolsById.TryGetValue(operation.Symbol.Id.Value, out var callable)
                || !callable.Facets.Facets.Contains(SymbolFacet.Callable))
            {
                continue;
            }

            var derivedFrom = EvidenceFor(context, callable, operation);
            if (derivedFrom is null)
            {
                continue;
            }

            context.Accumulator.AddRelation(
                ConfirmedRelation.Create(
                    RelationKind.ImplementsOperation,
                    callable.Reference,
                    operation.Reference,
                    EmptyFacets,
                    derivedFrom.Value,
                    ClassifierFor(operation),
                    context.AnalysisVariants,
                    EvidenceMethod.Semantic,
                    sourceFact: callable,
                    targetFact: operation));
            count++;
        }

        return count;
    }

    private static int EmitUsesContract(ClassifierContext context, IReadOnlyDictionary<string, Symbol> symbolsById)
    {
        if (context.AnalysisVariants.IsDefaultOrEmpty)
        {
            return 0;
        }

        var operationsById = context.FactsByType<BoundaryOperation>()
            .ToDictionary(static operation => operation.Reference.Id.Value, StringComparer.Ordinal);
        var count = 0;
        foreach (var binding in context.FactsByType<ContractBinding>()
            .OrderBy(static binding => binding.Reference.Id.Value, StringComparer.Ordinal))
        {
            if (!operationsById.TryGetValue(binding.Operation.Id.Value, out var operation)
                || !symbolsById.TryGetValue(operation.Symbol.Id.Value, out var callable))
            {
                continue;
            }

            var derivedFrom = EvidenceFor(context, callable, operation);
            if (derivedFrom is null)
            {
                continue;
            }

            var facets = FacetBinding.Create(
                PayloadRoleAxes,
                ["payload-role"],
                [new FacetBindingEntry("payload-role", binding.PayloadRole)]);
            context.Accumulator.AddRelation(
                ConfirmedRelation.Create(
                    RelationKind.UsesContract,
                    binding.Operation,
                    binding.Contract,
                    facets,
                    derivedFrom.Value,
                    ContractPass.Identity,
                    context.AnalysisVariants,
                    EvidenceMethod.Semantic));
            count++;
        }

        return count;
    }

    private static int EmitUnresolved(ClassifierContext context, IReadOnlyDictionary<string, Symbol> symbolsById)
    {
        var existing = context.Accumulator.ToSnapshot().Unresolved
            .Select(static record => record.Source.Id.Value + "\u0000" + record.Kind + "\u0000" + record.Cause)
            .ToHashSet(StringComparer.Ordinal);
        var count = 0;

        // GCPC-087/GCPC-092: a published event type reaches contract binding once ContractPass proves it
        // shared (a real Contract fact exists naming it). Matches ContractAccounting.Build's own
        // "contracted" test exactly, so the arithmetic residual it still reports for backward
        // compatibility reconciles against the discrete records emitted below rather than only inferring
        // them.
        var contractedEventNames = context.FactsByType<Contract>()
            .Where(static contract => contract.Proof.Role == LiteralRole.ProtocolName)
            .Select(static contract => contract.Proof.Value)
            .ToHashSet(StringComparer.Ordinal);
        var outboundMessagingOperations = context.FactsByType<BoundaryOperation>()
            .Where(static operation => operation.Protocol is BoundaryProtocol.Messaging
                && operation.Direction is BoundaryDirection.Outbound)
            .GroupBy(static operation => (operation.Symbol.Id.Value, EventType: ProtocolOperationKeyValue(operation) ?? string.Empty))
            .ToDictionary(static group => group.Key, static group => group.First());

        foreach (var observation in context.ObservationsByKind(ObservationKind.MessageOperation)
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal))
        {
            var methodName = ReadPayloadValue(observation, BoundaryPass.MethodNameKey);
            if (methodName is not ("PublishAsync" or "Publish"))
            {
                continue;
            }

            if (!symbolsById.TryGetValue(observation.Identity.Owner.Id.Value, out var callable))
            {
                continue;
            }

            var typeArgument = ReadPayloadValue(observation, BoundaryPass.TypeArgumentKey);
            if (typeArgument is null || IsAnonymousTypeName(typeArgument))
            {
                count += TryAddUnresolved(
                    context,
                    existing,
                    RelationKind.UsesContract,
                    callable.Reference,
                    UnresolvedCause.NoCandidateFound,
                    EvidenceChain.Create([observation.Identity]));
                continue;
            }

            // A normally-named published payload with no in-solution handler falls through every
            // AddCandidate/AddUnresolved call site ContractPass and this same method already had -- it
            // is contracted only if a Contract fact was actually minted for its exact event type name
            // (ContractPass.Execute never mints one for zero inbound handlers). Its source is the
            // outbound payload slot (BoundaryOperation, GCPC-092's own unit) when BoundaryPass
            // recognized one, falling back to the publishing callable otherwise -- never silently
            // dropped either way.
            if (contractedEventNames.Contains(typeArgument))
            {
                continue;
            }

            var source = outboundMessagingOperations.TryGetValue(
                (observation.Identity.Owner.Id.Value, typeArgument), out var operation)
                ? operation.Reference
                : callable.Reference;
            count += TryAddUnresolved(
                context,
                existing,
                RelationKind.UsesContract,
                source,
                UnresolvedCause.NoCandidateFound,
                EvidenceChain.Create([observation.Identity]));
        }

        foreach (var observation in context.ObservationsByKind(ObservationKind.Invocation)
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal))
        {
            if (!IsCreateClient(observation) || ReadPayloadValue(observation, BoundaryPass.ClientNameKey) is not null)
            {
                continue;
            }

            if (!symbolsById.TryGetValue(observation.Identity.Owner.Id.Value, out var callable))
            {
                continue;
            }

            count += TryAddUnresolved(
                context,
                existing,
                RelationKind.Targets,
                callable.Reference,
                UnresolvedCause.InsufficientEvidence,
                EvidenceChain.Create([observation.Identity]));
        }

        return count;
    }

    private static int TryAddUnresolved(
        ClassifierContext context,
        HashSet<string> existing,
        RelationKind kind,
        FactReference source,
        UnresolvedCause cause,
        EvidenceChain available)
    {
        var key = source.Id.Value + "\u0000" + kind + "\u0000" + cause;
        if (!existing.Add(key))
        {
            return 0;
        }

        context.Accumulator.AddUnresolved(UnresolvedRecord.Create(kind, source, cause, available));
        return 1;
    }

    private static EvidenceChain? EvidenceFor(ClassifierContext context, Symbol callable, BoundaryOperation operation)
    {
        var identities = context.ObservationsByOwner(callable.Reference)
            .Where(observation => IsEvidenceKind(observation, operation))
            .Select(static observation => observation.Identity)
            .ToArray();
        if (identities.Length == 0)
        {
            identities = context.ObservationsByOwner(callable.Reference)
                .Select(static observation => observation.Identity)
                .ToArray();
        }

        if (identities.Length == 0)
        {
            var container = ReadField(callable.Signature.Value, "container");
            if (container is not null)
            {
                identities = context.FactsByType<Symbol>()
                    .Where(symbol =>
                        string.Equals(ReadField(symbol.Signature.Value, "kind"), "namedtype", StringComparison.Ordinal)
                        && string.Equals(ReadField(symbol.Signature.Value, "type"), container, StringComparison.Ordinal))
                    .SelectMany(type => context.ObservationsByOwner(type.Reference))
                    .Select(static observation => observation.Identity)
                    .ToArray();
            }
        }

        return identities.Length == 0 ? null : EvidenceChain.Create(identities);
    }

    private static bool IsEvidenceKind(Observation observation, BoundaryOperation operation)
    {
        var kind = observation.Identity.Kind;
        return operation.Protocol switch
        {
            BoundaryProtocol.Http when operation.Direction is BoundaryDirection.Inbound =>
                kind is ObservationKind.RouteDeclaration or ObservationKind.AttributeUsage,
            BoundaryProtocol.Http => kind is ObservationKind.Invocation,
            BoundaryProtocol.Messaging => kind is ObservationKind.MessageOperation or ObservationKind.BaseType,
            _ => true,
        };
    }

    private static ClassifierIdentity ClassifierFor(BoundaryOperation operation) =>
        (operation.Direction, operation.Protocol) switch
        {
            (BoundaryDirection.Inbound, BoundaryProtocol.Http) => BoundaryPass.HttpInboundIdentity,
            (BoundaryDirection.Outbound, BoundaryProtocol.Http) => BoundaryPass.HttpOutboundIdentity,
            (_, BoundaryProtocol.Messaging) => BoundaryPass.MessagingIdentity,
            _ => BoundaryPass.HttpInboundIdentity,
        };

    private static bool IsCreateClient(Observation observation) =>
        string.Equals(ReadPayloadValue(observation, BoundaryPass.MethodNameKey), BoundaryPass.CreateClientMethodName, StringComparison.Ordinal)
        && PayloadContains(observation, BoundaryPass.TargetTypeKey, BoundaryPass.HttpClientFactoryTypeName);

    private static string? ProtocolOperationKeyValue(BoundaryOperation operation) =>
        operation.ProtocolOperationKey is { } key ? key.Value : null;

    private static bool IsAnonymousTypeName(string fullyQualifiedName) =>
        fullyQualifiedName.Contains("<>", StringComparison.Ordinal)
        || fullyQualifiedName.Contains("AnonymousType", StringComparison.Ordinal);

    private static bool PayloadContains(Observation observation, string key, string needle)
    {
        foreach (var entry in observation.Identity.Payload.Entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal)
                && entry.Value.Value.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
