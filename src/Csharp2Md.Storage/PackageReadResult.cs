using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage;

public sealed record PackageReadResult(
    FactualSnapshot Snapshot,
    ImmutableArray<QuarantineRecord> Quarantine,
    CoverageEnvelope Coverage,
    RunCertificationEnvelope Certification,
    ImmutableArray<StagedFragment> Projections);
