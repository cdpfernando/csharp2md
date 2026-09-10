using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class BoundaryPass : IClassifierPass
{
    internal const string RouteKey = "route";
    internal const string MethodNameKey = "method-name";
    internal const string TargetTypeKey = "target-type";
    internal const string ClientNameKey = "client-name";
    internal const string TypeArgumentKey = "type-argument";
    internal const string HttpClientFactoryTypeName = "IHttpClientFactory";
    internal const string EventBusTypeName = "IEventBus";
    internal const string IntegrationEventHandlerTypeName = "IIntegrationEventHandler";
    internal const string CreateClientMethodName = "CreateClient";

    private static readonly Dictionary<string, string> HttpMethodsByInvocationName = new(StringComparer.Ordinal)
    {
        ["PostAsJsonAsync"] = "POST",
        ["GetAsync"] = "GET",
        ["PutAsJsonAsync"] = "PUT",
        ["DeleteAsync"] = "DELETE",
        ["SendAsync"] = "SEND",
    };

    internal static ClassifierIdentity HttpInboundIdentity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.http-inbound", 1);

    internal static ClassifierIdentity HttpOutboundIdentity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.http-outbound", 1);

    internal static ClassifierIdentity MessagingIdentity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.messaging", 1);

    public string Name => "Boundaries";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var inbound = ClassifyHttpInbound(context);
        var outbound = ClassifyHttpOutbound(context);
        var messagingFacts = ClassifyMessagingOutbound(context) + ClassifyMessagingInbound(context);
        return new ClassifierPassResult(
            inbound.FactCount + outbound.FactCount + messagingFacts,
            0,
            outbound.CandidateCount,
            outbound.UnresolvedCount + inbound.UnresolvedCount);
    }

    private static (int FactCount, int UnresolvedCount) ClassifyHttpInbound(ClassifierContext context)
    {
        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var componentsById = context.FactsByType<Component>()
            .ToDictionary(static component => component.Reference.Id.Value, StringComparer.Ordinal);

        var factCount = 0;
        var unresolvedCount = 0;
        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);
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
            var httpMethod = ReadPayloadValue(routeDeclaration, MethodNameKey);
            if (template is null && httpMethod is null)
            {
                // SPEC_DEVIATION: spec EBC-06 empty template → empty-string protocolOperationKey.
                // Reason: StructuralLiteral.Create rejects empty canonical text, matching the
                // design missing-field rule (skip + diagnostic). Neither the verb nor the route is
                // proven here, so there is nothing to publish (GCPC-099..102 need at least one).
                context.Accumulator.AddDiagnostic(
                    new DiagnosticRecord(
                        "missing-route-template",
                        $"RouteDeclaration on '{symbol.Reference.Id.Value}' has no route template.",
                        symbol.Reference.Id.Value));
                continue;
            }

            // SPEC_DEVIATION: EBC-06 names the route template as protocolOperationKey.
            // Reason: TAX-30 inbound identity is that key; GET vs DELETE on the same
            // template must not share one identity (Domain example is "POST /charge").
            var operationKey = template is null
                ? httpMethod!
                : httpMethod is null ? template : httpMethod + " " + template;
            if (!emittedKeys.Add(string.Join('\u0000', component.Reference.Id.Value, operationKey)))
            {
                continue;
            }

            // GCPC-099/GCPC-100: the verb and route are published in their own fields whenever
            // proven, not left recoverable only by parsing protocolOperationKey.
            var protocolOperationKey = StructuralLiteral.Create(LiteralRole.ProtocolName, operationKey, "protocol-operation-key");
            var route = template is null ? null : (StructuralLiteral?)StructuralLiteral.Create(LiteralRole.Route, template, RouteKey);
            var operation = BoundaryOperation.Create(
                symbol.Reference,
                component.Reference,
                BoundaryDirection.Inbound,
                BoundaryProtocol.Http,
                httpMethod: httpMethod,
                route: route,
                protocolOperationKey: protocolOperationKey);
            context.Accumulator.AddFact(operation);
            factCount++;

            if (route is null || httpMethod is null)
            {
                // GCPC-102: only one of the verb and the route is proven here (the other branch above
                // already handled "neither") -- a conventional action carrying only a bare verb
                // attribute proves the verb but not the route, and a generic, verb-agnostic [Route]
                // attribute proves the route but not the verb. Publish the proven field and record the
                // unproven one as unresolved instead of leaving it undiscoverable.
                context.Accumulator.AddUnresolved(
                    UnresolvedRecord.Create(
                        RelationKind.Targets,
                        operation.Reference,
                        UnresolvedCause.InsufficientEvidence,
                        EvidenceChain.Create([routeDeclaration.Identity])));
                unresolvedCount++;
            }
        }

        return (factCount, unresolvedCount);
    }

    private static (int FactCount, int CandidateCount, int UnresolvedCount) ClassifyHttpOutbound(ClassifierContext context)
    {
        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);

        var factCount = 0;
        var candidateCount = 0;
        var unresolvedCount = 0;
        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);
        var emittedExternals = new HashSet<string>(StringComparer.Ordinal);

        var createClients = context.ObservationsByKind(ObservationKind.Invocation)
            .Where(IsCreateClient)
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Locator)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal)
            .ToArray();

        foreach (var createClient in createClients)
        {
            if (!symbolsById.TryGetValue(createClient.Identity.Owner.Id.Value, out var callable)
                || context.ComponentForSymbol(callable.Reference) is not { } component)
            {
                continue;
            }

            var clientName = ReadPayloadValue(createClient, ClientNameKey);
            if (clientName is null)
            {
                context.Accumulator.AddUnresolved(
                    UnresolvedRecord.Create(
                        RelationKind.Targets,
                        callable.Reference,
                        UnresolvedCause.InsufficientEvidence,
                        EvidenceChain.Create([createClient.Identity])));
                unresolvedCount++;
                continue;
            }

            var ownerInvocations = context.ObservationsByOwner(callable.Reference)
                .Where(static observation => observation.Identity.Kind is ObservationKind.Invocation)
                .OrderBy(static observation => observation.Locator)
                .ThenBy(static observation => observation.Identity.OccurrenceOrdinal)
                .ToArray();
            var nextCreate = ownerInvocations.FirstOrDefault(observation =>
                CompareSourceOrder(observation, createClient) > 0 && IsCreateClient(observation));

            foreach (var invocation in ownerInvocations)
            {
                if (CompareSourceOrder(invocation, createClient) <= 0
                    || (nextCreate is not null && CompareSourceOrder(invocation, nextCreate) >= 0)
                    || !TryHttpMethod(invocation, out var httpMethod))
                {
                    continue;
                }

                var routeValue = ReadPayloadValue(invocation, RouteKey);
                if (routeValue is null)
                {
                    context.Accumulator.AddUnresolved(
                        UnresolvedRecord.Create(
                            RelationKind.Targets,
                            callable.Reference,
                            UnresolvedCause.InsufficientEvidence,
                            EvidenceChain.Create([createClient.Identity, invocation.Identity])));
                    unresolvedCount++;
                    continue;
                }

                var tupleKey = string.Join('\u0000', clientName, httpMethod, routeValue);
                if (!emittedKeys.Add(tupleKey))
                {
                    continue;
                }

                var route = StructuralLiteral.Create(LiteralRole.Route, routeValue, RouteKey);
                var operation = BoundaryOperation.Create(
                    callable.Reference,
                    component.Reference,
                    BoundaryDirection.Outbound,
                    BoundaryProtocol.Http,
                    clientName,
                    httpMethod,
                    route);
                context.Accumulator.AddFact(operation);
                factCount++;

                var external = ExternalSystem.Create(
                    context.SolutionId,
                    StructuralLiteral.Create(LiteralRole.ClientName, clientName, ClientNameKey));
                if (emittedExternals.Add(external.Reference.Id.Value))
                {
                    context.Accumulator.AddFact(external);
                    factCount++;
                }

                context.Accumulator.AddCandidate(
                    CandidateLink.Create(
                        RelationKind.Targets,
                        operation.Reference,
                        external.Reference,
                        EvidenceChain.Create([createClient.Identity, invocation.Identity])));
                candidateCount++;
            }
        }

        return (factCount, candidateCount, unresolvedCount);
    }

    private static int ClassifyMessagingOutbound(ClassifierContext context)
    {
        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);

        var factCount = 0;
        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var observation in context.ObservationsByKind(ObservationKind.MessageOperation)
            .OrderBy(static o => o.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static o => o.Identity.OccurrenceOrdinal))
        {
            var methodName = ReadPayloadValue(observation, MethodNameKey);
            if (methodName is not ("PublishAsync" or "Publish")
                || !PayloadContains(observation, TargetTypeKey, EventBusTypeName))
            {
                continue;
            }

            if (!symbolsById.TryGetValue(observation.Identity.Owner.Id.Value, out var callable)
                || context.ComponentForSymbol(callable.Reference) is not { } component)
            {
                continue;
            }

            var eventType = ReadPayloadValue(observation, TypeArgumentKey);
            if (eventType is null)
            {
                context.Accumulator.AddDiagnostic(
                    new DiagnosticRecord(
                        "missing-message-type-argument",
                        $"MessageOperation on '{callable.Reference.Id.Value}' has no TEvent type argument.",
                        callable.Reference.Id.Value));
                continue;
            }

            if (!emittedKeys.Add(string.Join('\u0000', component.Reference.Id.Value, eventType)))
            {
                continue;
            }

            var protocolOperationKey = StructuralLiteral.Create(LiteralRole.ProtocolName, eventType, "protocol-operation-key");
            context.Accumulator.AddFact(
                BoundaryOperation.Create(
                    callable.Reference,
                    component.Reference,
                    BoundaryDirection.Outbound,
                    BoundaryProtocol.Messaging,
                    protocolOperationKey: protocolOperationKey));
            factCount++;
        }

        return factCount;
    }

    private static int ClassifyMessagingInbound(ClassifierContext context)
    {
        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var componentsById = context.FactsByType<Component>()
            .ToDictionary(static component => component.Reference.Id.Value, StringComparer.Ordinal);
        var types = symbolsById.Values
            .Where(static symbol => string.Equals(ReadField(symbol.Signature.Value, "kind"), "namedtype", StringComparison.Ordinal))
            .ToArray();

        var factCount = 0;
        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in context.FactsByType<EntryPoint>().OrderBy(static e => e.Reference.Id.Value, StringComparer.Ordinal))
        {
            if (!symbolsById.TryGetValue(entry.Symbol.Id.Value, out var symbol)
                || !componentsById.TryGetValue(entry.OwningComponent.Id.Value, out var component)
                || !string.Equals(ReadField(symbol.Signature.Value, "metadata"), "HandleAsync", StringComparison.Ordinal))
            {
                continue;
            }

            var declaringType = types.FirstOrDefault(type => IsDeclaredOn(symbol, type));
            var eventType = declaringType is null
                ? null
                : TryEventTypeFromHandlerBases(context, declaringType);
            eventType ??= TryFirstParameterType(symbol);
            if (eventType is null)
            {
                context.Accumulator.AddDiagnostic(
                    new DiagnosticRecord(
                        "missing-message-type-argument",
                        $"HandleAsync '{symbol.Reference.Id.Value}' has no TEvent type argument.",
                        symbol.Reference.Id.Value));
                continue;
            }

            if (!emittedKeys.Add(string.Join('\u0000', component.Reference.Id.Value, eventType)))
            {
                continue;
            }

            var protocolOperationKey = StructuralLiteral.Create(LiteralRole.ProtocolName, eventType, "protocol-operation-key");
            context.Accumulator.AddFact(
                BoundaryOperation.Create(
                    symbol.Reference,
                    component.Reference,
                    BoundaryDirection.Inbound,
                    BoundaryProtocol.Messaging,
                    protocolOperationKey: protocolOperationKey));
            factCount++;
        }

        return factCount;
    }

    private static string? TryEventTypeFromHandlerBases(ClassifierContext context, Symbol declaringType)
    {
        foreach (var observation in context.ObservationsByOwner(declaringType.Reference))
        {
            if (observation.Identity.Kind is not ObservationKind.BaseType
                || !PayloadContains(observation, TargetTypeKey, IntegrationEventHandlerTypeName))
            {
                continue;
            }

            var typeArgument = ReadPayloadValue(observation, TypeArgumentKey);
            if (typeArgument is not null)
            {
                return typeArgument;
            }
        }

        return null;
    }

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

    private static string? TryFirstParameterType(Symbol method)
    {
        var parameters = ReadField(method.Signature.Value, "parameters");
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return null;
        }

        var first = parameters.Split(',')[0].Trim();
        foreach (var prefix in new[] { "ref ", "out ", "in " })
        {
            if (first.StartsWith(prefix, StringComparison.Ordinal))
            {
                first = first[prefix.Length..];
                break;
            }
        }

        return string.IsNullOrWhiteSpace(first) ? null : first;
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

    private static int CompareSourceOrder(Observation left, Observation right)
    {
        var locator = left.Locator.CompareTo(right.Locator);
        return locator != 0
            ? locator
            : left.Identity.OccurrenceOrdinal.CompareTo(right.Identity.OccurrenceOrdinal);
    }

    private static bool IsCreateClient(Observation observation) =>
        string.Equals(ReadPayloadValue(observation, MethodNameKey), CreateClientMethodName, StringComparison.Ordinal)
        && PayloadContains(observation, TargetTypeKey, HttpClientFactoryTypeName);

    private static bool TryHttpMethod(Observation observation, out string httpMethod)
    {
        httpMethod = string.Empty;
        var methodName = ReadPayloadValue(observation, MethodNameKey);
        if (methodName is null || !HttpMethodsByInvocationName.TryGetValue(methodName, out var mapped))
        {
            return false;
        }

        httpMethod = mapped;
        return true;
    }

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
}
