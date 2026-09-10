using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Pipeline;

internal sealed class SnapshotAccumulator
{
    private readonly Dictionary<string, IFact> _facts = new(StringComparer.Ordinal);
    private readonly Dictionary<ObservationIdentity, Observation> _observations = [];
    private readonly List<ConfirmedRelation> _relations = [];
    private readonly List<CandidateLink> _candidates = [];
    private readonly List<UnresolvedRecord> _unresolved = [];
    private readonly List<OpenFrontier> _frontiers = [];
    private readonly List<DiagnosticRecord> _diagnostics = [];
    private readonly List<SuspectedSecretEvidence> _secrets = [];
    private DocumentPolicyReport _documentPolicy = DocumentPolicyReport.Empty;

    public bool StructuralCorruption { get; private set; }

    public string? CollidingIdentity { get; private set; }

    public void AddFact(IFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var key = fact.Reference.Id.Value;
        if (_facts.TryGetValue(key, out var existing))
        {
            if (!existing.Equals(fact))
            {
                StructuralCorruption = true;
                CollidingIdentity = key;
            }

            return;
        }

        _facts.Add(key, fact);
    }

    public void AddObservation(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (_observations.TryGetValue(observation.Identity, out var existing))
        {
            if (IsBound(observation) && !IsBound(existing))
            {
                _observations[observation.Identity] = observation;
            }

            return;
        }

        _observations.Add(observation.Identity, observation);
    }

    public void AddRelation(ConfirmedRelation relation)
    {
        ArgumentNullException.ThrowIfNull(relation);
        _relations.Add(relation);
    }

    public void AddCandidate(CandidateLink candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        _candidates.Add(candidate);
    }

    public bool RemoveCandidate(CandidateLink candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return _candidates.RemoveAll(existing => existing.Equals(candidate)) > 0;
    }

    public void AddUnresolved(UnresolvedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _unresolved.Add(record);
    }

    public void AddOpenFrontier(OpenFrontier frontier)
    {
        ArgumentNullException.ThrowIfNull(frontier);
        _frontiers.Add(frontier);
    }

    public void AddDiagnostic(DiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _diagnostics.Add(record);
    }

    public void AddSuspectedSecret(SuspectedSecretEvidence evidence) => _secrets.Add(evidence);

    public void AddDocumentPolicyReport(DocumentPolicyReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _documentPolicy = _documentPolicy.Merge(report);
    }

    public FactualSnapshot ToSnapshot() =>
        new(
            [.. _facts.Values],
            [.. _observations.Values],
            [.. _relations],
            [.. _candidates],
            [.. _unresolved],
            [.. _frontiers],
            [.. _diagnostics],
            [.. _secrets],
            _documentPolicy);

    private static bool IsBound(Observation observation) =>
        string.Equals(observation.Diagnostic.Code, "bound", StringComparison.Ordinal);
}
