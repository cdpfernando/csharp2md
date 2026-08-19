using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Facts.Model;

public enum FactLevel
{
    Solution,
    Project,
    Target,
    Document,
    Detector,
}

public enum CoverageApplicability
{
    Applicable,
    NotApplicable,
}

public enum CoverageAttempt
{
    NotAttempted,
    Attempted,
}

public sealed record CoverageFact(
    FactId ScopeId,
    FactLevel FactLevel,
    DetectorId? DetectorId,
    CoverageApplicability Applicability,
    CoverageAttempt Attempt,
    FactResolution Resolution,
    ImmutableArray<DiagnosticId> DiagnosticIds);
