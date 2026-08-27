using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection.Source;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection;

public sealed class PackageProjector : IPackageProjector
{
    public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(source);
        return SourceProjector.Project(view, source);
    }
}
