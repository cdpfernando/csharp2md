using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Validation;

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
