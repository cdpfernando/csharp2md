using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Source;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Markdown;

internal static class MarkdownProjector
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static ImmutableArray<StagedFragment> Project(PublishedPackageView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var catalogs = IndexCatalogs(view);
        var postings = IndexPostings(view);
        var fragments = ImmutableArray.CreateBuilder<StagedFragment>();
        AddFamily(
            fragments,
            view,
            "entry-point",
            view.Document.EntryPoints.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings);
        AddFamily(
            fragments,
            view,
            "boundary-operation",
            view.Document.BoundaryOperations.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings);
        AddFamily(
            fragments,
            view,
            "component",
            view.Document.Components.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings);
        AddFamily(
            fragments,
            view,
            "deployment-unit",
            view.Document.DeploymentUnits.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings);
        AddFamily(
            fragments,
            view,
            "contract",
            view.Document.Contracts.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings);
        AddFamily(
            fragments,
            view,
            "data-store",
            view.Document.DataStores.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings);
        AddFamily(
            fragments,
            view,
            "data-object",
            view.Document.DataObjects.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings);
        return fragments.ToImmutable();
    }

    private static void AddFamily(
        ImmutableArray<StagedFragment>.Builder fragments,
        PublishedPackageView view,
        string type,
        IEnumerable<PageSubject> subjects,
        ImmutableDictionary<string, ArtifactCitation> catalogs,
        IReadOnlyDictionary<string, ImmutableArray<ArtifactCitation>> postings)
    {
        foreach (var subject in subjects.OrderBy(static page => page.FactId, StringComparer.Ordinal))
        {
            if (!view.TryLocate(subject.FactId, out var citation))
            {
                continue;
            }

            fragments.Add(new StagedFragment(
                ArtifactRole.Payload,
                PageKey(type, subject.FactId),
                Utf8NoBom.GetBytes(Render(view, subject, citation, catalogs, postings)).ToImmutableArray()));
        }
    }

    internal static string PageKey(string type, string factId)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Utf8NoBom.GetBytes(factId)));
        return "markdown/" + type + "/" + digest + ".md";
    }

    private static string Render(
        PublishedPackageView view,
        PageSubject subject,
        ArtifactCitation citation,
        ImmutableDictionary<string, ArtifactCitation> catalogs,
        IReadOnlyDictionary<string, ImmutableArray<ArtifactCitation>> postings)
    {
        var text = new StringBuilder();
        text.Append("# ").Append(Cite(subject.FactType, citation)).Append('\n');
        text.Append('\n');
        text.Append("Fact id: ").Append(Cite(subject.FactId, citation)).Append('\n');
        text.Append('\n');
        text.Append("## Facets").Append('\n');
        foreach (var (axis, value) in subject.Facets)
        {
            text.Append("- ").Append(Cite(axis, citation)).Append(": ").Append(Cite(value, citation)).Append('\n');
        }

        text.Append('\n');
        text.Append("## Relations").Append('\n');
        foreach (var relation in DirectRelations(view, subject.FactId))
        {
            text.Append("- ")
                .Append(Cite(relation.Record.Kind, relation.Citation))
                .Append(' ')
                .Append(Cite(relation.Record.Source.Id, relation.Citation))
                .Append(" -> ")
                .Append(Cite(relation.Record.Target.Id, relation.Citation))
                .Append('\n');
        }

        text.Append('\n');
        text.Append("## Evidence").Append('\n');
        if (catalogs.TryGetValue(subject.FactId, out var catalog))
        {
            text.Append("- ").Append(Cite(subject.FactId, catalog)).Append('\n');
        }

        if (postings.TryGetValue(subject.FactId, out var postingCitations))
        {
            foreach (var posting in postingCitations)
            {
                text.Append("- ").Append(Cite(subject.FactId, posting)).Append('\n');
            }
        }

        if (SourceCitation(view, subject.SymbolId) is { } source)
        {
            text.Append("- ").Append(Cite(source.ArtifactKey, source)).Append('\n');
        }

        return text.ToString();
    }

    private static string Cite(string value, ArtifactCitation citation) =>
        "[" + value + "](" + citation.ArtifactKey + ") <!-- "
        + citation.Ordinal.ToString(CultureInfo.InvariantCulture) + " -->";

    private static ImmutableArray<(ConfirmedRelationDto Record, ArtifactCitation Citation)> DirectRelations(
        PublishedPackageView view,
        string factId)
    {
        var relations = ImmutableArray.CreateBuilder<(ConfirmedRelationDto, ArtifactCitation)>();
        foreach (var descriptor in Csharp2Md.Domain.Registry.TaxonomyTables.Default.Relations)
        {
            if (!view.Document.ConfirmedRelations.TryGetValue(descriptor.WireName, out var records)
                || records.IsDefaultOrEmpty)
            {
                continue;
            }

            for (var index = 0; index < records.Length; index++)
            {
                var record = records[index];
                if (!string.Equals(record.Source.Id, factId, StringComparison.Ordinal)
                    && !string.Equals(record.Target.Id, factId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!view.TryLocateRelation(descriptor.WireName, index, out var citation))
                {
                    continue;
                }

                relations.Add((record, citation));
            }
        }

        return relations.ToImmutable();
    }

    private static ArtifactCitation? SourceCitation(PublishedPackageView view, string? symbolId)
    {
        if (string.IsNullOrEmpty(symbolId))
        {
            return null;
        }

        var symbol = view.Document.Symbols.FirstOrDefault(candidate =>
            string.Equals(candidate.Identity.Id, symbolId, StringComparison.Ordinal));
        if (symbol?.DeclarationLocator is not { } locator)
        {
            return null;
        }

        var document = view.Document.Documents.FirstOrDefault(candidate =>
            string.Equals(candidate.Identity.Id, locator.Document, StringComparison.Ordinal)
            || string.Equals(candidate.RelativePath, locator.RelativePath, StringComparison.Ordinal));
        if (document is null)
        {
            return null;
        }

        return new ArtifactCitation(SourceProjector.ArtifactKey(document), 0);
    }

    private static ImmutableDictionary<string, ArtifactCitation> IndexCatalogs(PublishedPackageView view)
    {
        var index = ImmutableDictionary.CreateBuilder<string, ArtifactCitation>(StringComparer.Ordinal);
        foreach (var fragment in CatalogProjector.Project(view))
        {
            var entries = CanonicalJson.Read<ImmutableArray<CatalogEntryDto>>(Payload(fragment).AsSpan());
            for (var ordinal = 0; ordinal < entries.Length; ordinal++)
            {
                index[entries[ordinal].FactId] = new ArtifactCitation(fragment.CanonicalKey, ordinal);
            }
        }

        return index.ToImmutable();
    }

    private static ImmutableDictionary<string, ImmutableArray<ArtifactCitation>> IndexPostings(PublishedPackageView view)
    {
        var index = new Dictionary<string, ImmutableArray<ArtifactCitation>.Builder>(StringComparer.Ordinal);
        foreach (var fragment in PostingProjector.Project(view))
        {
            var groups = CanonicalJson.Read<ImmutableArray<PostingGroupDto>>(Payload(fragment).AsSpan());
            for (var ordinal = 0; ordinal < groups.Length; ordinal++)
            {
                var factId = groups[ordinal].FactId;
                if (!index.TryGetValue(factId, out var citations))
                {
                    citations = ImmutableArray.CreateBuilder<ArtifactCitation>();
                    index[factId] = citations;
                }

                citations.Add(new ArtifactCitation(fragment.CanonicalKey, ordinal));
            }
        }

        return index.ToImmutableDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToImmutable(),
            StringComparer.Ordinal);
    }

    private static ImmutableArray<byte> Payload(StagedFragment fragment) =>
        fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;

    private readonly record struct PageSubject(
        string FactId,
        string FactType,
        string? SymbolId,
        ImmutableArray<(string Axis, string Value)> Facets)
    {
        public static PageSubject From(EntryPointDto dto) =>
            new(dto.Identity.Id, dto.Identity.FactType, dto.Symbol.Id, []);

        public static PageSubject From(BoundaryOperationDto dto)
        {
            var facets = ImmutableArray.CreateBuilder<(string, string)>();
            facets.Add(("direction", dto.Direction));
            if (!string.IsNullOrEmpty(dto.Protocol))
            {
                facets.Add(("protocol", dto.Protocol));
            }

            return new(dto.Identity.Id, dto.Identity.FactType, dto.Symbol.Id, facets.ToImmutable());
        }

        public static PageSubject From(ComponentDto dto) =>
            new(dto.Identity.Id, dto.Identity.FactType, null, []);

        public static PageSubject From(DeploymentUnitDto dto) =>
            new(dto.Identity.Id, dto.Identity.FactType, null, []);

        public static PageSubject From(ContractDto dto) =>
            new(dto.Identity.Id, dto.Identity.FactType, null, []);

        public static PageSubject From(DataStoreDto dto) =>
            new(dto.Identity.Id, dto.Identity.FactType, null, [("technology", dto.Technology)]);

        public static PageSubject From(DataObjectDto dto) =>
            new(dto.Identity.Id, dto.Identity.FactType, null, [("form", dto.Form), ("mapping_state", dto.MappingState)]);
    }
}
