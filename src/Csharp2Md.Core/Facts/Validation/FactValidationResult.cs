using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Facts.Validation;

internal sealed record ValidatedFactFragment
{
    public ImmutableArray<IFact> Facts { get; }

    public ImmutableArray<AnalysisDiagnostic> Diagnostics { get; }

    internal ValidatedFactFragment(ImmutableArray<IFact> facts, ImmutableArray<AnalysisDiagnostic> diagnostics)
    {
        Facts = facts;
        Diagnostics = diagnostics;
    }
}

internal sealed record FactValidationResult(
    ValidatedFactFragment? Fragment,
    ImmutableArray<AnalysisDiagnostic> ValidationDiagnostics)
{
    public bool IsValid => Fragment is not null;
}
