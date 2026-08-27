using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage;

public interface IBatchComposer
{
    SolutionContribution Contribute(PublishedPackageView view, SolutionCoordinate coordinate, string packageDirectory);

    ImmutableArray<StagedFragment> Compose(BatchView batch);
}

public sealed record BatchView(
    ImmutableArray<BatchSolutionRecord> Solutions,
    ImmutableArray<SolutionContribution> Contributions)
{
    public bool Complete =>
        Solutions.IsDefaultOrEmpty
        || !Solutions.Any(static record => record.Status == PublicationStatus.Unpublished);

    public string? IncompleteScopeReason => Complete ? null : "solution-unpublished";
}
