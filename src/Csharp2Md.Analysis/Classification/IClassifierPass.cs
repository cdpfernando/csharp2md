namespace Csharp2Md.Analysis.Classification;

internal interface IClassifierPass
{
    string Name { get; }

    ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken);
}

internal readonly record struct ClassifierPassResult(
    int FactCount,
    int RelationCount,
    int CandidateCount,
    int UnresolvedCount);
