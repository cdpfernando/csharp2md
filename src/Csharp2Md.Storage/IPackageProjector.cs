using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage;

public interface IPackageProjector
{
    ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source);
}
