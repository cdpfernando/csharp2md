using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Guides;

internal static class RetrievalGuideProjector
{
    internal const string Key = "retrieval.md";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static ImmutableArray<StagedFragment> Project(PublishedPackageView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var slots = view.Slots
            .Select(static slot => slot.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);
        return
        [
            new StagedFragment(ArtifactRole.Payload, Key, Utf8NoBom.GetBytes(Render(slots)).ToImmutableArray()),
        ];
    }

    private static string Render(HashSet<string> slots)
    {
        var locate = Pick(
            slots,
            "facts/architecture.json",
            "facts/contract.json",
            "facts/structural.json",
            "facts/persistence.json");
        var fact = Pick(slots, "facts/architecture.json", "facts/structural.json", "facts/contract.json");
        var follow = Pick(
            slots,
            "relations/confirmed/executes.json",
            "relations/confirmed/implements-operation.json",
            "relations/confirmed/invokes.json",
            "relations/confirmed/contains.json");
        var effects = Pick(
            slots,
            "relations/confirmed/uses-contract.json",
            "relations/confirmed/accesses-data.json",
            "relations/confirmed/operates-on.json",
            "relations/confirmed/targets.json",
            "relations/confirmed/contains.json");
        var source = Pick(slots, "facts/structural.json", "coverage.json");
        var frontiers = Pick(slots, "relations/candidates.json", "relations/frontiers.json", "coverage.json");
        var stop = Pick(slots, "coverage.json", "diagnostics.json", "run-certification.json");

        var text = new StringBuilder();
        text.Append("# Retrieval\n\n");
        text.Append("Start at the publication manifest. The sections below document each retrieval scenario.\n\n");
        text.Append("## 1. locate an entry point, operation, contract, symbol or data field\n\n");
        text.Append("Read `").Append(locate).Append("`.\n\n");
        text.Append("## 2. open the canonical fact and direct relations\n\n");
        text.Append("Read `").Append(fact).Append("`.\n\n");
        text.Append("## 3. follow `executes`, `implements-operation` and `invokes`\n\n");
        text.Append("Read `").Append(follow).Append("`.\n\n");
        text.Append("## 4. inspect `uses-contract`, `accesses-data`, `operates-on` and `targets` effects\n\n");
        text.Append("Read `").Append(effects).Append("`.\n\n");
        text.Append("## 5. open complete callable bodies through source locators\n\n");
        text.Append("Read `").Append(source).Append("` for locators, then open the matching source projection.\n\n");
        text.Append("## 6. inspect candidates and open frontiers separately\n\n");
        text.Append("Read `").Append(frontiers).Append("`.\n\n");
        text.Append("## 7. stop on terminal effects, cycles, unsupported capabilities or a declared reading budget\n\n");
        text.Append("Read `").Append(stop).Append("`.\n");
        return text.ToString();
    }

    private static string Pick(HashSet<string> slots, params string[] preferred)
    {
        foreach (var key in preferred)
        {
            if (slots.Contains(key))
            {
                return key;
            }
        }

        return slots.OrderBy(static key => key, StringComparer.Ordinal).First();
    }
}
