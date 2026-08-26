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
    internal const string HttpClientFactoryTypeName = "IHttpClientFactory";
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

    public string Name => "Boundaries";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var factCount = ClassifyHttpInbound(context);
        var outbound = ClassifyHttpOutbound(context);
        return new ClassifierPassResult(
            factCount + outbound.FactCount,
            0,
            outbound.CandidateCount,
            outbound.UnresolvedCount);
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

    private static (int FactCount, int CandidateCount, int UnresolvedCount) ClassifyHttpOutbound(ClassifierContext context)
    {
        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var projectsById = context.FactsByType<Project>()
            .ToDictionary(static project => project.Id.Value, StringComparer.Ordinal);
        var componentsByPath = context.FactsByType<Component>()
            .ToDictionary(static component => component.Name, StringComparer.Ordinal);

        var factCount = 0;
        var candidateCount = 0;
        var unresolvedCount = 0;
        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);
        var emittedExternals = new HashSet<string>(StringComparer.Ordinal);

        var createClients = context.ObservationsByKind(ObservationKind.Invocation)
            .Where(IsCreateClient)
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal)
            .ToArray();

        foreach (var createClient in createClients)
        {
            if (!symbolsById.TryGetValue(createClient.Identity.Owner.Id.Value, out var callable)
                || !projectsById.TryGetValue(callable.OwningProject.Value, out var project)
                || TryLogicalPath(project) is not { } path
                || !componentsByPath.TryGetValue(path, out var component))
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
                .OrderBy(static observation => observation.Identity.OccurrenceOrdinal)
                .ToArray();
            var nextCreateOrdinal = ownerInvocations
                .Where(observation =>
                    observation.Identity.OccurrenceOrdinal > createClient.Identity.OccurrenceOrdinal
                    && IsCreateClient(observation))
                .Select(static observation => observation.Identity.OccurrenceOrdinal)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            foreach (var invocation in ownerInvocations)
            {
                if (invocation.Identity.OccurrenceOrdinal <= createClient.Identity.OccurrenceOrdinal
                    || invocation.Identity.OccurrenceOrdinal >= nextCreateOrdinal
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
