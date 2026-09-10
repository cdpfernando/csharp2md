using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Validation;

/// <summary>
/// One required solution's certification-relevant facts, gathered from its own already-published
/// package: its own run-certification status (<see cref="RunCertificationEnvelope.Status"/>, or
/// <c>null</c> when unknown) and its provenance (or <c>null</c> when unknown). Publication status
/// itself is not repeated here -- <see cref="BatchView.Complete"/> already decides it from
/// <c>BatchSolutionRecord.Status</c>, and <see cref="Certify"/> defers to that existing check first.
/// </summary>
public sealed record BatchSolutionCertificationStatus(
    string Identity,
    string? RunCertificationStatus,
    ProvenanceDto? Provenance);

/// <summary>Whether the batch is certified and, when it is not, the single reason that disqualified it.</summary>
public sealed record BatchCertificationResult(bool Certified, string? Reason);

public static class BatchValidator
{
    public static void Validate(BatchView batch, ImmutableArray<StagedFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (fragments.IsDefaultOrEmpty)
        {
            return;
        }

        var identities = batch.Solutions.IsDefaultOrEmpty
            ? new HashSet<string>(StringComparer.Ordinal)
            : batch.Solutions.Select(static record => record.Identity.Value).ToHashSet(StringComparer.Ordinal);
        var locators = IndexLocators(batch.Contributions);

        foreach (var fragment in fragments)
        {
            var payload = fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;
            if (payload.IsDefaultOrEmpty)
            {
                throw new PublicationRejectedException("batch-composition", fragment.CanonicalKey);
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(payload.AsSpan());
            }
            catch (JsonException exception)
            {
                throw new PublicationRejectedException("batch-composition", fragment.CanonicalKey, exception);
            }

            if (node is JsonArray { Count: 0 } || node is null)
            {
                throw new PublicationRejectedException("batch-composition", fragment.CanonicalKey);
            }

            foreach (var entry in EnumerateObjects(node))
            {
                ValidateEntry(entry, identities, locators);
            }
        }
    }

    /// <summary>
    /// GCPC-112..GCPC-114: extends <see cref="BatchView.Complete"/> and <see
    /// cref="BatchView.IncompleteScopeReason"/>'s own shape -- a boolean plus a named reason -- with
    /// the two additional conditions a certified batch requires beyond every solution being published:
    /// every required solution's provenance must agree with every other solution's in the batch (the
    /// same generator build, comparable version axes), and every required solution must itself be
    /// individually certifiable (its own published run-certification status is not <c>"failed"</c>;
    /// <c>"passed"</c> and <c>"degraded"</c> both qualify, matching the run-certification vocabulary in
    /// GCPC-001). Publication status is checked first, by deferring to <see cref="BatchView.Complete"/>
    /// exactly as it already reports it -- this method does not re-derive or re-word that reason. The
    /// first disqualifying condition beyond that wins and names the offending solution, checked in the
    /// fixed order provenance-incompatible then not-certifiable, so a batch failing more than one
    /// condition still reports one deterministic reason. This method only decides and reports; it never
    /// writes, and GCPC-114 (committed per-solution packages stay untouched by an incomplete batch) is a
    /// publication invariant enforced by the write path, not by this pure computation.
    /// </summary>
    public static BatchCertificationResult Certify(
        BatchView batch,
        ImmutableArray<BatchSolutionCertificationStatus> solutions)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (!batch.Complete)
        {
            return new BatchCertificationResult(false, batch.IncompleteScopeReason);
        }

        if (solutions.IsDefaultOrEmpty)
        {
            return new BatchCertificationResult(true, null);
        }

        ProvenanceDto? reference = null;
        foreach (var solution in solutions)
        {
            if (solution.Provenance is not { } provenance)
            {
                continue;
            }

            if (reference is null)
            {
                reference = provenance;
                continue;
            }

            if (!ProvenanceCompatible(reference, provenance))
            {
                return new BatchCertificationResult(false, $"provenance-incompatible:{solution.Identity}");
            }
        }

        foreach (var solution in solutions)
        {
            if (solution.RunCertificationStatus == "failed")
            {
                return new BatchCertificationResult(false, $"solution-not-certifiable:{solution.Identity}");
            }
        }

        return new BatchCertificationResult(true, null);
    }

    /// <summary>Two solutions in the same batch are provenance-compatible when they were produced by the
    /// same generator build over the same contract version axes -- the values a consumer would use to
    /// decide whether two packages are comparable (GCPC-056, GCPC-057).</summary>
    private static bool ProvenanceCompatible(ProvenanceDto left, ProvenanceDto right) =>
        string.Equals(left.GeneratorVersion, right.GeneratorVersion, StringComparison.Ordinal)
        && string.Equals(left.BuildIdentity, right.BuildIdentity, StringComparison.Ordinal)
        && left.SchemaVersion == right.SchemaVersion
        && left.TaxonomyVersion == right.TaxonomyVersion
        && left.ObservationSchemaVersion == right.ObservationSchemaVersion
        && left.ExtractorSetVersion == right.ExtractorSetVersion
        && left.ClassifierSetVersion == right.ClassifierSetVersion;

    private static void ValidateEntry(
        JsonObject entry,
        HashSet<string> identities,
        Dictionary<string, HashSet<Locator>> locators)
    {
        var source = ReadString(entry, "source_solution_identity");
        var target = ReadString(entry, "target_solution_identity");
        if (source is not null && target is not null && string.Equals(source, target, StringComparison.Ordinal))
        {
            throw new PublicationRejectedException("batch-composition", source);
        }

        EnsureIdentity(identities, source);
        EnsureIdentity(identities, target);
        EnsureIdentity(identities, ReadString(entry, "solution_identity"));

        EnsureLocator(locators, source, ReadString(entry, "source_artifact_key"), ReadInt(entry, "source_ordinal"));
        EnsureLocator(locators, target, ReadString(entry, "target_artifact_key"), ReadInt(entry, "target_ordinal"));
        EnsureLocator(
            locators,
            ReadString(entry, "solution_identity"),
            ReadString(entry, "artifact_key"),
            ReadInt(entry, "ordinal"));
    }

    private static void EnsureIdentity(HashSet<string> identities, string? identity)
    {
        if (identity is not null && !identities.Contains(identity))
        {
            throw new PublicationRejectedException("batch-composition", identity);
        }
    }

    private static void EnsureLocator(
        Dictionary<string, HashSet<Locator>> locators,
        string? identity,
        string? artifactKey,
        int? ordinal)
    {
        if (identity is null || artifactKey is null || ordinal is null)
        {
            return;
        }

        if (!locators.TryGetValue(identity, out var known) || !known.Contains(new Locator(artifactKey, ordinal.Value)))
        {
            throw new PublicationRejectedException("batch-composition", artifactKey + " " + ordinal.Value.ToString());
        }
    }

    private static Dictionary<string, HashSet<Locator>> IndexLocators(ImmutableArray<SolutionContribution> contributions)
    {
        var index = new Dictionary<string, HashSet<Locator>>(StringComparer.Ordinal);
        if (contributions.IsDefaultOrEmpty)
        {
            return index;
        }

        foreach (var contribution in contributions)
        {
            if (!index.TryGetValue(contribution.SolutionIdentity, out var known))
            {
                known = [];
                index[contribution.SolutionIdentity] = known;
            }

            Add(known, contribution.BoundaryOperations, static item => item.ArtifactKey, static item => item.Ordinal);
            Add(known, contribution.Contracts, static item => item.ArtifactKey, static item => item.Ordinal);
            Add(known, contribution.Components, static item => item.ArtifactKey, static item => item.Ordinal);
            Add(known, contribution.DeploymentUnits, static item => item.ArtifactKey, static item => item.Ordinal);
            Add(known, contribution.ExternalSystems, static item => item.ArtifactKey, static item => item.Ordinal);
        }

        return index;
    }

    private static void Add<T>(
        HashSet<Locator> known,
        ImmutableArray<T> items,
        Func<T, string> artifactKey,
        Func<T, int> ordinal)
    {
        if (items.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var item in items)
        {
            known.Add(new Locator(artifactKey(item), ordinal(item)));
        }
    }

    private static IEnumerable<JsonObject> EnumerateObjects(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                yield return obj;
                foreach (var property in obj)
                {
                    foreach (var nested in EnumerateObjects(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    foreach (var nested in EnumerateObjects(element))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static string? ReadString(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node)
        && node is JsonValue value
        && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static int? ReadInt(JsonObject obj, string name)
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is not JsonValue value)
        {
            return null;
        }

        return value.TryGetValue<int>(out var number) ? number : null;
    }

    private readonly record struct Locator(string ArtifactKey, int Ordinal);
}
