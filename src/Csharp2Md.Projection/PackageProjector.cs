using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Guides;
using Csharp2Md.Projection.Markdown;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Source;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection;

public sealed class PackageProjector : IPackageProjector
{
    private readonly int _ceilingBytes;

    public PackageProjector()
        : this(ShardWriter.DefaultCeilingBytes)
    {
    }

    public PackageProjector(int ceilingBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ceilingBytes);
        _ceilingBytes = ceilingBytes;
    }

    public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(source);
        if (HasNoFacts(view))
        {
            return [];
        }

        return SourceProjector.Project(view, source)
            .AddRange(CatalogProjector.Project(view, _ceilingBytes))
            .AddRange(PostingProjector.Project(view, _ceilingBytes))
            .AddRange(MarkdownProjector.Project(view))
            .AddRange(RetrievalGuideProjector.Project(view))
            .AddRange(AgentsGuideProjector.Project(view));
    }

    private static bool HasNoFacts(PublishedPackageView view) =>
        view.Slots.All(static slot =>
            !slot.CanonicalKey.StartsWith("facts/", StringComparison.Ordinal)
            && !slot.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal)
            && !slot.CanonicalKey.StartsWith("relations/", StringComparison.Ordinal)
            && !slot.CanonicalKey.StartsWith("quarantine/", StringComparison.Ordinal));
}
