using System.Text;
using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Guides;

/// <summary>
/// Documents how an LLM walks the published package: every scenario starts at a catalog (GCPC-046),
/// teaches posting-bucket selection and ordinal resolution instead of reading a canonical payload whole
/// (GCPC-047, GCPC-050), names the artifact that holds each of the seven confirmed relation kinds and
/// each of the three unproven proof states (GCPC-048, GCPC-049), and states the five stopping rules
/// (GCPC-051). Every artifact key this guide names is drawn from what this publication actually produced
/// -- never a static literal -- so a key this run did not produce can never be cited, and the render is
/// still checked against the full set of keys this publication is about to hold before it is returned,
/// aborting the publication if it ever names one that publication does not contain (GCPC-055, extends
/// RP-42).
/// </summary>
internal static class RetrievalGuideProjector
{
    internal const string Key = "retrieval.md";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly Regex ArtifactKeyPattern = new("`([^`]+)`", RegexOptions.Compiled);

    /// <summary>The seven confirmed relation kinds the guide covers (GCPC-048), in their published order.</summary>
    private static readonly string[] RelationKinds =
    [
        "executes",
        "implements-operation",
        "invokes",
        "uses-contract",
        "accesses-data",
        "operates-on",
        "targets",
    ];

    private static readonly (string Marker, string Hint)[] PostingFamilies =
    [
        ("outgoing", "every confirmed relation this fact id is the source of"),
        ("incoming", "every confirmed relation this fact id is the target of"),
        ("callers", "`invokes` narrowed to this fact id as callee"),
        ("callees", "`invokes` narrowed to this fact id as caller"),
        ("contract-producers", "`uses-contract` narrowed to this fact id as producer"),
        ("contract-consumers", "`uses-contract` narrowed to this fact id as consumer"),
        ("data-readers", "`operates-on` narrowed to this fact id as reader"),
        ("data-writers", "`operates-on` narrowed to this fact id as writer"),
        ("unknowns", "unresolved records owned by this fact id"),
        ("frontiers", "open frontiers owned by this fact id"),
    ];

    public static ImmutableArray<StagedFragment> Project(
        PublishedPackageView view,
        int ceilingBytes = ShardWriter.DefaultCeilingBytes)
    {
        ArgumentNullException.ThrowIfNull(view);

        var slots = view.Slots.Select(static slot => slot.CanonicalKey).ToHashSet(StringComparer.Ordinal);
        var catalogKeys = CatalogProjector.Project(view, ceilingBytes)
            .Select(static fragment => fragment.CanonicalKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToImmutableArray();
        var postingKeys = PostingProjector.Project(view, ceilingBytes)
            .Select(static fragment => fragment.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);

        var known = new HashSet<string>(slots, StringComparer.Ordinal);
        known.UnionWith(catalogKeys);
        known.UnionWith(postingKeys);

        var text = Render(slots, catalogKeys, postingKeys, known);
        return [new StagedFragment(ArtifactRole.Payload, Key, Utf8NoBom.GetBytes(text).ToImmutableArray())];
    }

    private static string Render(
        HashSet<string> slots,
        ImmutableArray<string> catalogKeys,
        HashSet<string> postingKeys,
        HashSet<string> known)
    {
        var text = new StringBuilder();
        text.Append("# Retrieval\n\n");
        text.Append(
            "Every scenario below starts at a catalog, teaches how to pick the right posting bucket and "
            + "ordinal without reading a canonical payload in full, and states where to stop.\n\n");

        AppendLocateSection(text, catalogKeys);
        AppendPostingsSection(text, postingKeys);
        AppendRelationsSection(text, slots);
        AppendDispositionsSection(text, slots);
        AppendSourceSection(text, slots);
        AppendStoppingRules(text);

        var rendered = text.ToString();
        ValidateNoAbsentKeys(rendered, known);
        return rendered;
    }

    private static void AppendLocateSection(StringBuilder text, ImmutableArray<string> catalogKeys)
    {
        text.Append("## 1. Locate an identity\n\n");
        if (catalogKeys.IsDefaultOrEmpty)
        {
            text.Append("No catalog is published: this package holds no fact of a cataloged family.\n\n");
            return;
        }

        text.Append("Open the catalog for the identity's family -- never the canonical fact payload:\n\n");
        foreach (var key in catalogKeys)
        {
            text.Append("- `").Append(key).Append("` for ").Append(CatalogHint(key)).Append('\n');
        }

        text.Append('\n');
        text.Append(
            "Each entry carries `fact_id`, `artifact_key`, `ordinal` and proven compact `labels`; read the "
            + "entry to recognize the identity (GCPC-093).\n\n");
    }

    private static string CatalogHint(string key) => key switch
    {
        _ when key.Contains("entry-points", StringComparison.Ordinal) => "an entry point",
        _ when key.Contains("boundary-operations", StringComparison.Ordinal) => "a boundary operation",
        _ when key.Contains("components-and-deployment-units", StringComparison.Ordinal)
            => "a component or deployment unit",
        _ when key.Contains("contracts", StringComparison.Ordinal) => "a contract",
        _ when key.Contains("data-stores-objects-and-fields", StringComparison.Ordinal)
            => "a data store, data object or data field",
        _ when key.Contains("unknowns", StringComparison.Ordinal)
            => "an identity ranked by unresolved-occurrence degree",
        _ => "this catalog's identities",
    };

    private static void AppendPostingsSection(StringBuilder text, HashSet<string> postingKeys)
    {
        text.Append("## 2. Select a postings bucket and resolve by ordinal\n\n");
        text.Append(
            "A posting groups every relation touching one fact id under that id; each entry cites the "
            + "exact `artifact_key` and `ordinal` of the canonical record, so open only the cited artifact "
            + "at the cited ordinal, never the whole canonical payload:\n\n");
        foreach (var (key, hint) in PostingHints(postingKeys))
        {
            text.Append("- ");
            text.Append(
                postingKeys.Contains(key)
                    ? "`" + key + "`"
                    : key + " (bucketed by fact id across shards)");
            text.Append(" -- ").Append(hint).Append('\n');
        }

        if (postingKeys.Count == 0)
        {
            text.Append("No posting is published: this package holds no confirmed relation, unresolved record or open frontier.\n");
        }

        text.Append('\n');
    }

    /// <summary>
    /// One hint per posting family (GCPC-047), not one per shard: a family that split under the ceiling
    /// still has exactly one line here, naming its base key without backticks (the caller decides whether
    /// to cite it literally) -- printing one line per <c>ShardWriter</c> bucket would let this section
    /// itself grow past the publication ceiling under scale.
    /// </summary>
    private static IEnumerable<(string Key, string Hint)> PostingHints(HashSet<string> postingKeys)
    {
        foreach (var (marker, hint) in PostingFamilies)
        {
            var baseKey = "postings/" + marker + ".json";
            if (HasFamily(postingKeys, baseKey))
            {
                yield return (baseKey, hint);
            }
        }
    }

    private static void AppendRelationsSection(StringBuilder text, HashSet<string> slots)
    {
        text.Append("## 3. Follow a confirmed relation\n\n");
        text.Append("Every confirmed relation kind this workstream covers has its own retrieval path:\n\n");
        foreach (var kind in RelationKinds)
        {
            var artifactKey = "relations/confirmed/" + kind + ".json";
            text.Append("- `").Append(kind).Append("` -- ");
            if (slots.Contains(artifactKey))
            {
                text.Append("select its posting bucket in step 2, then read `")
                    .Append(artifactKey)
                    .Append("` at the cited ordinal.\n");
            }
            else if (HasFamily(slots, artifactKey))
            {
                // Named without backticks on purpose, same reason as AppendSourceSection below: a split
                // family has no artifact at this exact key, only at relations/confirmed/<kind>.<bucket>.json
                // shards, and ValidateNoAbsentKeys rejects any backtick-quoted key this publication does
                // not actually hold.
                text.Append("select its posting bucket in step 2, then read the matching ")
                    .Append(artifactKey)
                    .Append(" shard at the cited ordinal.\n");
            }
            else
            {
                text.Append("no such relation is recognized in this package.\n");
            }
        }

        text.Append('\n');
    }

    private static void AppendDispositionsSection(StringBuilder text, HashSet<string> slots)
    {
        text.Append("## 4. Follow an unproven disposition\n\n");
        text.Append("Each proof state that is not a confirmed relation has its own artifact and, where one exists, its own posting:\n\n");
        AppendDisposition(
            text,
            slots,
            "candidate link",
            "relations/candidates.json",
            key => "scan `" + key + "` directly by source or proposed target fact id -- candidates carry no posting bucket",
            key => "scan its " + key + " shards directly by source or proposed target fact id -- candidates carry no posting bucket");
        AppendDisposition(
            text,
            slots,
            "unresolved record",
            "relations/unresolved.json",
            key => "select its bucket in `postings/unknowns.json`, then read `" + key + "` at the cited ordinal",
            key => "select its bucket in `postings/unknowns.json`, then read the matching " + key + " shard at the cited ordinal");
        AppendDisposition(
            text,
            slots,
            "open frontier",
            "relations/frontiers.json",
            key => "select its bucket in `postings/frontiers.json`, then read `" + key + "` at the cited ordinal",
            key => "select its bucket in `postings/frontiers.json`, then read the matching " + key + " shard at the cited ordinal");
        text.Append('\n');
    }

    /// <summary>
    /// <paramref name="how"/> renders the exact-match case (the family's own key, quotable with backticks);
    /// <paramref name="howSharded"/> renders the split case, naming the family without backticks -- a split
    /// family has no artifact at that exact key, only at <c>&lt;stem&gt;.&lt;bucket&gt;.json</c> shards,
    /// and <see cref="ValidateNoAbsentKeys"/> rejects any backtick-quoted key this publication does not
    /// actually hold.
    /// </summary>
    private static void AppendDisposition(
        StringBuilder text,
        HashSet<string> slots,
        string label,
        string artifactKey,
        Func<string, string> how,
        Func<string, string> howSharded)
    {
        text.Append("- ").Append(label).Append(": ");
        text.Append(
            slots.Contains(artifactKey) ? how(artifactKey)
            : HasFamily(slots, artifactKey) ? howSharded(artifactKey)
            : "none is recognized in this package");
        text.Append('\n');
    }

    private static void AppendSourceSection(StringBuilder text, HashSet<string> slots)
    {
        text.Append("## 5. Open complete callable bodies through source locators\n\n");
        if (HasFamily(slots, "facts/structural.json"))
        {
            // "facts/structural.json" is named without backticks here on purpose: once the family
            // exceeds the ceiling it publishes as facts/structural.<bucket>.json shards instead of that
            // exact key (GCPC-039), and ValidateNoAbsentKeys rejects any backtick-quoted key this
            // publication does not actually hold -- the cited ordinal always resolves through the fact's
            // own citation, never through this literal name.
            text.Append(
                "Read a symbol's `declaration_locator` from its facts/structural family at the cited "
                + "ordinal, then open the matching source projection: its key starts with source/, "
                + "followed by the declaring document's relative path.\n\n");
        }
        else
        {
            text.Append("No symbol is published in this package, so no source locator can be followed.\n\n");
        }
    }

    /// <summary>True when <paramref name="slots"/> holds <paramref name="baseKey"/> itself or any shard
    /// of it (<c>base.&lt;bucket&gt;.json</c>) -- so a family that split under the ceiling is still
    /// recognized, not reported as absent (GCPC-039, GCPC-048).</summary>
    private static bool HasFamily(HashSet<string> slots, string baseKey) =>
        slots.Contains(baseKey) || slots.Any(key => IsShardOf(key, baseKey));

    private static bool IsShardOf(string key, string baseKey)
    {
        var dot = baseKey.LastIndexOf('.');
        var stem = dot < 0 ? baseKey : baseKey[..dot];
        var extension = dot < 0 ? string.Empty : baseKey[dot..];
        return key.StartsWith(stem + ".", StringComparison.Ordinal) && key.EndsWith(extension, StringComparison.Ordinal);
    }

    private static void AppendStoppingRules(StringBuilder text)
    {
        text.Append("## 6. Stop\n\n");
        text.Append("Stop a traversal when any of the following holds:\n\n");
        text.Append("1. a terminal effect is reached -- a boundary crossed with no statically demonstrable continuation\n");
        text.Append("2. a traversal cycle is detected -- a fact id already visited on the current path\n");
        text.Append("3. an open frontier is reached with no further posting to follow\n");
        text.Append("4. a capability is unsupported for the current identity -- no catalog, posting or relation artifact exists for it\n");
        text.Append("5. the declared reading budget is exhausted -- 100,000 read tokens or 25 file reads for the scenario\n");
    }

    /// <summary>
    /// Aborts the publication if this guide names an artifact key this publication does not hold
    /// (GCPC-055, extends RP-42) -- the same rejection shape <c>ProjectionValidator</c> already uses for
    /// every other dangling projection citation.
    /// </summary>
    internal static void ValidateNoAbsentKeys(string text, ISet<string> known)
    {
        foreach (Match match in ArtifactKeyPattern.Matches(text))
        {
            var candidate = match.Groups[1].Value;
            if (!LooksLikeArtifactKey(candidate))
            {
                continue;
            }

            if (!known.Contains(candidate))
            {
                throw new PublicationRejectedException("projection-key", candidate);
            }
        }
    }

    private static bool LooksLikeArtifactKey(string candidate) =>
        candidate.Contains('/', StringComparison.Ordinal)
        && (candidate.EndsWith(".json", StringComparison.Ordinal) || candidate.EndsWith(".md", StringComparison.Ordinal));
}
