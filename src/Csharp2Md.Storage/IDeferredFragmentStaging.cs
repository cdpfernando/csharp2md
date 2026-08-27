using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Storage;

internal interface IDeferredFragmentStaging
{
    void StageDeferred(StagedFragment fragment);
}
