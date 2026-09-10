namespace Csharp2Md.Analysis.Tests.Certification;

/// <summary>
/// GCPC-074..GCPC-076: one ground-truth item from a labeled corpus, authored against the source and
/// never derived from classifier output. Kind names why the item was chosen: <c>Positive</c> proves
/// recall (the classifier must produce it), <c>Negative</c> proves precision against an unambiguous
/// non-case, and <c>Lookalike</c> proves precision against a near-neighbor a naive classifier could
/// mistake for the positive case.
/// </summary>
public enum LabelKind
{
    Positive,
    Negative,
    Lookalike,
}

/// <summary>
/// The classifier-observable outcome a labeled item is expected to reach. <c>Present</c> means the
/// engine must publish the fact/relation the item names; <c>Absent</c> means it must publish none for
/// that construct (a counted exclusion or no output at all); <c>Unresolved</c> means it must publish an
/// explicit candidate or unresolved record rather than a confirmed outcome.
/// </summary>
public enum ExpectedState
{
    Present,
    Absent,
    Unresolved,
}

/// <summary>The four areas GCPC-078's normative thresholds are measured against.</summary>
public enum CertifiedArea
{
    EntryPoint,
    LinkedCall,
    Contract,
    Persistence,
}

/// <summary>
/// One row of a labeled corpus file under <c>fixtures/CertificationCorpus/labels/</c>. Every field is
/// authored by a human reading the fixture source, never read back from a classifier's own output
/// (GCPC-075).
/// </summary>
internal sealed record LabeledCorpusEntry(
    string Id,
    LabelKind Kind,
    ExpectedState Expected,
    string SourceFile,
    string? RelatedSourceFile,
    string Symbol,
    string Rationale);
