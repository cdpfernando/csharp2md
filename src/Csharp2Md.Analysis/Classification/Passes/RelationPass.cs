using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
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

        foreach (var observation in context.ObservationsByKind(ObservationKind.MessageOperation)
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal))
        {
            var methodName = ReadPayloadValue(observation, BoundaryPass.MethodNameKey);
            if (methodName is not ("PublishAsync" or "Publish"))
            {
                continue;
            }

            var typeArgument = ReadPayloadValue(observation, BoundaryPass.TypeArgumentKey);
            if (typeArgument is not null && !IsAnonymousTypeName(typeArgument))
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
                RelationKind.UsesContract,
                callable.Reference,
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
