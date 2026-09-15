namespace Csharp2Md.Cli;

/// <summary>
/// The stable exit-code space shared by <c>analyze</c>, <c>validate</c> and <c>compose</c> (GCPC-069
/// through GCPC-073). One code per outcome class the user named, extending the existing <c>0</c>,
/// <c>1</c> and <c>2</c> meanings rather than renumbering them; <c>3</c> through <c>6</c> are new.
/// </summary>
internal static class ExitCodes
{
    /// <summary>
    /// The command completed successfully with certification <c>passed</c> (GCPC-069), or -- for an
    /// invocation that carries no certification concept of its own -- with no unpublished solution and no
    /// batch-publication failure (the pre-existing meaning, unchanged).
    /// </summary>
    internal const int Success = 0;

    /// <summary>The invocation itself was invalid; nothing was published (GCPC-073).</summary>
    internal const int InvalidInvocation = 1;

    /// <summary>
    /// At least one solution in a batch is unpublished; every already-committed package is left unmodified
    /// (GCPC-072). The pre-existing meaning, unchanged.
    /// </summary>
    internal const int PartialComposition = 2;

    /// <summary>The command completed with certification <c>degraded</c> (GCPC-070).</summary>
    internal const int Degraded = 3;

    /// <summary>The command completed with certification <c>failed</c> (GCPC-070).</summary>
    internal const int CertificationFailed = 4;

    /// <summary>A package or publication is structurally corrupt (GCPC-071).</summary>
    internal const int StructuralCorruption = 5;

    /// <summary>Provenance or a contract version is incompatible (GCPC-071).</summary>
    internal const int IncompatibleProvenance = 6;
}
