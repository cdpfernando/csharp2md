using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Pipeline;

/// <summary>
/// Turns <c>InvokesPass</c>'s per-occurrence disposition ledger (GCPC-011) into the published
/// invocation-accounting envelope: a per-disposition total that sums exactly to the recognized
/// invocation-occurrence count (GCPC-012, GCPC-015), with an occurrence that carries both an unresolved
/// record and an open frontier counted once in its exclusive bucket and once more only in the frontier
/// overlay (GCPC-014), and any occurrence that carries no disposition at all named rather than silently
/// dropped (GCPC-013). Built while classification still has the ledger in hand -- an exclusion leaves no
/// other published trace today, so this is the one place that split is still recoverable.
/// </summary>
internal static class InvocationAccounting
{
    public static InvocationAccountingReport Build(
        IReadOnlyCollection<ObservationIdentity> recognizedOccurrences,
        InvocationDispositionLedger ledger,
        IReadOnlyCollection<ObservationIdentity> openFrontierOccurrences)
    {
        ArgumentNullException.ThrowIfNull(recognizedOccurrences);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(openFrontierOccurrences);

        var confirmed = 0;
        var candidate = 0;
        var unresolved = 0;
        var exclusionCounts = new Dictionary<InvocationExclusionCategory, int>();

        foreach (var disposition in ledger.Dispositions)
        {
            switch (disposition.Kind)
            {
                case InvocationDispositionKind.Confirmed:
                    confirmed++;
                    break;
                case InvocationDispositionKind.Candidate:
                    candidate++;
                    break;
                case InvocationDispositionKind.Unresolved:
                    unresolved++;
                    break;
                case InvocationDispositionKind.Excluded:
                    var category = disposition.ExclusionCategory!.Value;
                    exclusionCounts[category] = exclusionCounts.GetValueOrDefault(category) + 1;
                    break;
                case InvocationDispositionKind.OpenFrontier:
                    // Never assigned directly by InvokesPass today: a frontier always accompanies a
                    // Candidate or Unresolved disposition on the same occurrence instead (GCPC-014), so
                    // there is nothing exclusive to bucket here.
                    break;
            }
        }

        var exclusions = exclusionCounts
            .OrderBy(static pair => pair.Key)
            .Select(static pair => new ExclusionCategoryCount(WireName(pair.Key), pair.Value))
            .ToImmutableArray();

        var accountedFor = new HashSet<ObservationIdentity>(ledger.Dispositions.Select(static d => d.Occurrence));
        var unaccounted = recognizedOccurrences
            .Where(occurrence => !accountedFor.Contains(occurrence))
            .Select(Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();

        var openFrontier = openFrontierOccurrences.Distinct().Count();

        return new InvocationAccountingReport(
            recognizedOccurrences.Count,
            confirmed,
            candidate,
            unresolved,
            openFrontier,
            exclusions,
            unaccounted);
    }

    /// <summary>A stable, human-readable name for an unaccounted occurrence (GCPC-013).</summary>
    private static string Name(ObservationIdentity occurrence) =>
        $"{occurrence.Owner.Id.Value}#{occurrence.Kind}#{occurrence.OccurrenceOrdinal}";

    private static string WireName(InvocationExclusionCategory category) => category switch
    {
        InvocationExclusionCategory.ExternalFrameworkCallable => "external-framework-callable",
        InvocationExclusionCategory.DuplicateEdge => "duplicate-edge",
        _ => throw new ArgumentOutOfRangeException(
            nameof(category), category, $"'{category}' is not a defined value of the '{nameof(InvocationExclusionCategory)}' axis."),
    };
}

/// <summary>
/// Turns what <c>ContractPass</c> and <c>BoundaryPass</c> already publish into the contract-accounting
/// envelope (GCPC-088, GCPC-089): every recognized message operation observation and messaging payload
/// slot reaches exactly one outcome, and the absence of a contract is never presented as the absence of
/// messaging -- every recognized item is counted here regardless of outcome. Derived from published
/// facts and observations only (never a classifier pass's transient state), the same way
/// <see cref="ValidationAndCoverageStage"/> derives entry-point and linked-call coverage.
/// </summary>
internal static class ContractAccounting
{
    private const string TypeArgumentKey = "type-argument";

    public static ContractAccountingReport Build(FactualSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var contractedEventNames = snapshot.Facts.OfType<Contract>()
            .Where(static contract => contract.Proof.Role == LiteralRole.ProtocolName)
            .Select(static contract => contract.Proof.Value)
            .ToHashSet(StringComparer.Ordinal);

        var messageOperations = snapshot.Observations
            .Where(static o => o.Identity.Kind is ObservationKind.MessageOperation)
            .ToArray();
        var payloadSlots = snapshot.Facts.OfType<BoundaryOperation>()
            .Where(static operation => operation.Protocol is BoundaryProtocol.Messaging)
            .ToArray();

        var boundOperationIds = snapshot.Facts.OfType<ContractBinding>()
            .Select(static binding => binding.Operation.Id.Value)
            .ToHashSet(StringComparer.Ordinal);

        var contractedMessageOperations = messageOperations.Count(observation =>
            PayloadValue(observation, TypeArgumentKey) is { } eventType && contractedEventNames.Contains(eventType));
        var contractedPayloadSlots = payloadSlots.Count(operation => boundOperationIds.Contains(operation.Reference.Id.Value));

        var recognizedTotal = messageOperations.Length + payloadSlots.Length;
        var contracted = contractedMessageOperations + contractedPayloadSlots;
        var unresolved = recognizedTotal - contracted;

        return new ContractAccountingReport(
            recognizedTotal,
            contracted,
            Candidate: 0,
            unresolved,
            Exclusions: ImmutableArray<ExclusionCategoryCount>.Empty);
    }

    private static string? PayloadValue(Observation observation, string key)
    {
        foreach (var entry in observation.Identity.Payload.Entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(entry.Value.Value))
            {
                return entry.Value.Value;
            }
        }

        return null;
    }
}
