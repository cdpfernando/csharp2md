using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Validation;

public static class ProjectionValidator
{
    public static void Validate(PublishedPackageView view, ImmutableArray<StagedFragment> projections)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (projections.IsDefaultOrEmpty)
        {
            return;
        }

        var counts = view.Slots.ToDictionary(
            static slot => slot.CanonicalKey,
            static slot => slot.Count,
            StringComparer.Ordinal);

        foreach (var fragment in projections)
        {
            if (fragment.IsDeferred || fragment.Payload.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var citation in EnumerateCitations(fragment.Payload))
            {
                if (!counts.TryGetValue(citation.ArtifactKey, out var count))
                {
                    throw new PublicationRejectedException("projection-key", citation.ArtifactKey);
                }

                if ((uint)citation.Ordinal >= (uint)count)
                {
                    throw new PublicationRejectedException(
                        "projection-ordinal",
                        citation.ArtifactKey + " " + citation.Ordinal.ToString());
                }
            }
        }
    }

    private static IEnumerable<ArtifactCitation> EnumerateCitations(ImmutableArray<byte> payload)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload.AsSpan());
        }
        catch (JsonException)
        {
            yield break;
        }

        foreach (var citation in Walk(node))
        {
            yield return citation;
        }
    }

    private static IEnumerable<ArtifactCitation> Walk(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (TryReadCitation(obj, out var citation))
                {
                    yield return citation;
                }

                foreach (var property in obj)
                {
                    foreach (var nested in Walk(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    foreach (var nested in Walk(element))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static bool TryReadCitation(JsonObject obj, out ArtifactCitation citation)
    {
        citation = default;
        if (!obj.TryGetPropertyValue("artifact_key", out var keyNode)
            || keyNode is not JsonValue keyValue
            || !keyValue.TryGetValue<string>(out var key)
            || string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (!obj.TryGetPropertyValue("ordinal", out var ordinalNode)
            || ordinalNode is not JsonValue ordinalValue
            || !ordinalValue.TryGetValue<int>(out var ordinal))
        {
            return false;
        }

        citation = new ArtifactCitation(key, ordinal);
        return true;
    }
}
