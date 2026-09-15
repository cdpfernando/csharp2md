using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Labels;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Source;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Markdown;

internal static class MarkdownProjector
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Reserved bytes at the end of a page's ceiling budget for the "N more direct relation(s)" note
    /// itself (F6/GCPC-038) -- generous enough for a handful of posting citations against a long fact id,
    /// so the note that describes the truncation never itself becomes the thing that overflows the page.
    /// </summary>
    private const int TruncationNoteReserveBytes = 4096;

    public static ImmutableArray<StagedFragment> Project(
        PublishedPackageView view, int ceilingBytes = ShardWriter.DefaultCeilingBytes)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ceilingBytes);

        var catalogs = IndexCatalogs(view);
        var postings = IndexPostings(view);
        var fragments = ImmutableArray.CreateBuilder<StagedFragment>();
        AddFamily(
            fragments,
            view,
            "entry-point",
            view.Document.EntryPoints.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings,
            ceilingBytes);
        AddFamily(
            fragments,
            view,
            "boundary-operation",
            view.Document.BoundaryOperations.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings,
            ceilingBytes);
        AddFamily(
            fragments,
            view,
            "component",
            view.Document.Components.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings,
            ceilingBytes);
        AddFamily(
            fragments,
            view,
            "deployment-unit",
            view.Document.DeploymentUnits.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings,
            ceilingBytes);
        AddFamily(
            fragments,
            view,
            "contract",
            view.Document.Contracts.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings,
            ceilingBytes);
        AddFamily(
            fragments,
            view,
            "data-store",
            view.Document.DataStores.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings,
            ceilingBytes);
        AddFamily(
            fragments,
            view,
            "data-object",
            view.Document.DataObjects.Select(static dto => PageSubject.From(dto)),
            catalogs,
            postings,
            ceilingBytes);
        return fragments.ToImmutable();
    }

    private static void AddFamily(
        ImmutableArray<StagedFragment>.Builder fragments,
        PublishedPackageView view,
        string type,
        IEnumerable<PageSubject> subjects,
        ImmutableDictionary<string, ArtifactCitation> catalogs,
        IReadOnlyDictionary<string, ImmutableArray<ArtifactCitation>> postings,
        int ceilingBytes)
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
                Utf8NoBom.GetBytes(Render(view, subject, citation, catalogs, postings, ceilingBytes)).ToImmutableArray()));
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
        IReadOnlyDictionary<string, ImmutableArray<ArtifactCitation>> postings,
        int ceilingBytes)
    {
        var labels = LabelProjector.For(new FactReferenceDto(subject.FactId, subject.FactType), view);
        var header = new StringBuilder();
        header.Append("# ").Append(Title(labels, subject.FactType, citation)).Append('\n');
        header.Append('\n');
        header.Append("Fact id: ").Append(Cite(subject.FactId, citation)).Append(" (").Append(subject.FactType).Append(")\n");
        header.Append('\n');
        header.Append("## Facets").Append('\n');
        foreach (var (axis, value) in subject.Facets)
        {
            header.Append("- ").Append(Cite(axis, citation)).Append(": ").Append(Cite(value, citation)).Append('\n');
        }

        header.Append('\n');
        header.Append("## Relations").Append('\n');

        postings.TryGetValue(subject.FactId, out var postingCitations);

        var evidence = new StringBuilder();
        evidence.Append('\n');
        evidence.Append("## Evidence").Append('\n');
        if (catalogs.TryGetValue(subject.FactId, out var catalog))
        {
            evidence.Append("- ").Append(Cite(subject.FactId, catalog)).Append('\n');
        }

        if (!postingCitations.IsDefaultOrEmpty)
        {
            foreach (var posting in postingCitations)
            {
                evidence.Append("- ").Append(Cite(subject.FactId, posting)).Append('\n');
            }
        }

        if (SourceCitation(view, subject.SymbolId) is { } source)
        {
            evidence.Append("- ").Append(Cite(source.ArtifactKey, source)).Append('\n');
        }

        // F6/GCPC-038: a fact with a large fan-in (e.g. a Component many Symbols belong to) can carry
        // enough direct relations to push the whole page past the ceiling on its own, by a multiple
        // rather than a margin. Rather than enumerate every direct relation inline,
        // include as many as fit the remaining budget and point the rest at the same posting artifact(s)
        // the ## Evidence section below already cites for this fact -- the postings family exists
        // precisely to hold a fact's full relation set without a whole-payload read (GCPC-047).
        var relations = DirectRelations(view, subject.FactId);
        var budget = Math.Max(
            0,
            ceilingBytes - Utf8NoBom.GetByteCount(header.ToString()) - Utf8NoBom.GetByteCount(evidence.ToString())
                - TruncationNoteReserveBytes);
        var relationsSection = new StringBuilder();
        var usedBytes = 0;
        var included = 0;
        foreach (var relation in relations)
        {
            var line = "- "
                + Cite(relation.Record.Kind, relation.Citation) + " "
                + Cite(relation.Record.Source.Id, relation.Citation) + " -> "
                + Cite(relation.Record.Target.Id, relation.Citation) + "\n";
            var lineBytes = Utf8NoBom.GetByteCount(line);
            if (usedBytes + lineBytes > budget)
            {
                break;
            }

            relationsSection.Append(line);
            usedBytes += lineBytes;
            included++;
        }

        if (included < relations.Length)
        {
            var remaining = relations.Length - included;
            relationsSection.Append("- ").Append(remaining).Append(" more direct relation(s); see ");
            if (!postingCitations.IsDefaultOrEmpty)
            {
                relationsSection.Append(string.Join(
                    ", ", postingCitations.Select(posting => Cite(subject.FactId, posting))));
            }
            else
            {
                var families = relations
                    .Select(static relation => relation.Citation.ArtifactKey)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static key => key, StringComparer.Ordinal);
                relationsSection.Append(string.Join(", ", families.Select(static key => "`" + key + "`")));
            }

            relationsSection.Append('\n');
        }

        return header.ToString() + relationsSection + evidence;
    }

    private static string Cite(string value, ArtifactCitation citation) =>
        "[" + value + "](" + citation.ArtifactKey + ") <!-- "
        + citation.Ordinal.ToString(CultureInfo.InvariantCulture) + " -->";

    /// <summary>
    /// The page title: leads with the proven compact label instead of the fact type plus an encoded id
    /// (GCPC-096), each piece cited to the exact artifact and ordinal <see cref="LabelProjector"/> proved
    /// it against (GCPC-094). A name-only identity (component, deployment unit, contract, data store, data
    /// object) titles by its name. An entry point or boundary operation titles by its method (falling back
    /// to its type, then its owning component, if the symbol itself is not proven), with the proven HTTP
    /// verb and route led in front when they exist and never synthesized when they do not (GCPC-101,
    /// GCPC-102). When nothing is proven at all, the fact type is the only fallback left (GCPC-098).
    /// </summary>
    private static string Title(ImmutableArray<LabelDto> labels, string factType, ArtifactCitation selfCitation)
    {
        var name = FindLabel(labels, LabelProjector.Name);
        if (name is not null)
        {
            return CiteLabel(name);
        }

        var component = FindLabel(labels, LabelProjector.Component);
        var headline = FindLabel(labels, LabelProjector.Method) ?? FindLabel(labels, LabelProjector.Type) ?? component;
        if (headline is null)
        {
            return Cite(factType, selfCitation);
        }

        var title = new StringBuilder();
        var verb = FindLabel(labels, LabelProjector.Verb);
        if (verb is not null)
        {
            title.Append(CiteLabel(verb)).Append(' ');
        }

        var route = FindLabel(labels, LabelProjector.Route);
        if (route is not null)
        {
            title.Append(CiteLabel(route)).Append(' ');
        }

        title.Append(CiteLabel(headline));
        if (component is not null && component != headline)
        {
            title.Append(" (").Append(CiteLabel(component)).Append(')');
        }

        return title.ToString();
    }

    private static LabelDto? FindLabel(ImmutableArray<LabelDto> labels, string kind)
    {
        foreach (var label in labels)
        {
            if (label.Kind == kind)
            {
                return label;
            }
        }

        return null;
    }

    private static string CiteLabel(LabelDto label) => Cite(label.Value, new ArtifactCitation(label.ArtifactKey, label.Ordinal));

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
