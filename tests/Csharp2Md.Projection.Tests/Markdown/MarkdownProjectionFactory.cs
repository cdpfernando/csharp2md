using System.Text;
using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Markdown;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Source;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Projection.Tests.Source;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Markdown;

internal static class MarkdownProjectionFactory
{
    private static readonly Regex CitationPattern = new(
        @"\[(?<text>[^\]]*)\]\((?<key>[^)]+)\) <!-- (?<ordinal>\d+) -->",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static ConfirmedRelation Executes(EntryPoint entry, Symbol symbol, int occurrenceOrdinal = 1) =>
        ConfirmedRelation.Create(
            RelationKind.Executes,
            entry.Reference,
            symbol.Reference,
            EmptyFacets(),
            Evidence(entry.Reference, occurrenceOrdinal),
            ClassifierIdentity.Create("csharp2md.projection.executes", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Semantic,
            entry,
            symbol);

    internal static ConfirmedRelation ImplementsOperation(
        Symbol symbol,
        BoundaryOperation operation,
        int occurrenceOrdinal = 1) =>
        ConfirmedRelation.Create(
            RelationKind.ImplementsOperation,
            symbol.Reference,
            operation.Reference,
            EmptyFacets(),
            Evidence(symbol.Reference, occurrenceOrdinal),
            ClassifierIdentity.Create("csharp2md.projection.implements-operation", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Semantic,
            symbol);

    internal static FacetBinding EmptyFacets() => FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    internal static EvidenceChain Evidence(FactReference owner, int occurrenceOrdinal) =>
        EvidenceChain.Create(
        [
            new ObservationIdentity(owner, ObservationKind.Invocation, NormalizedPayload.Create([]), occurrenceOrdinal),
        ]);

    internal static string TextOf(StagedFragment fragment)
    {
        var payload = fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;
        return Encoding.UTF8.GetString(payload.AsSpan());
    }

    internal static string PageFor(ImmutableArray<StagedFragment> fragments, string factId)
    {
        var fragment = Assert.Single(
            fragments,
            candidate => candidate.CanonicalKey.StartsWith("markdown/", StringComparison.Ordinal)
                && TextOf(candidate).Contains(factId, StringComparison.Ordinal));
        return TextOf(fragment);
    }

    internal static ImmutableArray<StagedFragment> Pages(ImmutableArray<StagedFragment> fragments) =>
        [.. fragments.Where(static fragment => fragment.CanonicalKey.StartsWith("markdown/", StringComparison.Ordinal))];

    internal readonly record struct MarkdownCitation(string Text, string ArtifactKey, int Ordinal);

    internal static ImmutableArray<MarkdownCitation> ParseCitations(string page)
    {
        var citations = ImmutableArray.CreateBuilder<MarkdownCitation>();
        foreach (Match match in CitationPattern.Matches(page))
        {
            citations.Add(new MarkdownCitation(
                match.Groups["text"].Value,
                match.Groups["key"].Value,
                int.Parse(match.Groups["ordinal"].Value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        return citations.ToImmutable();
    }

    internal static HashSet<string> PublicationKeys(PublishedPackageView view, ISourceDocumentReader reader)
    {
        var keys = view.Slots.Select(static slot => slot.CanonicalKey).ToHashSet(StringComparer.Ordinal);
        foreach (var fragment in SourceProjector.Project(view, reader)
            .AddRange(CatalogProjector.Project(view))
            .AddRange(PostingProjector.Project(view)))
        {
            keys.Add(fragment.CanonicalKey);
        }

        return keys;
    }
}
