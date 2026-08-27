using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Guides;

internal static class AgentsGuideProjector
{
    internal const string Key = "AGENTS.md";

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
        var source = Pick(slots, "facts/structural.json", "coverage.json");
        var text = new StringBuilder();
        text.Append("# Agent instructions\n\n");
        text.Append("## Manifest\n\n");
        text.Append("Begin at the publication manifest. It is the entry point. Every other artifact is listed there as a relative key.\n\n");
        text.Append("## Proof state\n\n");
        text.Append("Proof is recorded on three axes. Do not invent confidence scores.\n\n");
        text.Append("- evidence method: semantic, syntactic, or configured\n");
        text.Append("- resolution: confirmed, candidate, or unresolved\n");
        text.Append("- frontier: closed or open\n\n");
        text.Append("## Source retrieval\n\n");
        text.Append("Read `").Append(source).Append("` for locators, then open the matching source projection. Once selected, retrieve that body completely.\n\n");
        text.Append("## Markdown\n\n");
        text.Append("Markdown is a projection and never authority. Treat payload artifacts as the source of truth.\n");
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
