namespace Csharp2Md.Cli;

/// <summary>
/// The exit-code space of the current contract, shared by <c>analyze</c> and <c>validate</c>. One code
/// per outcome class the two commands can reach; no other value is produced.
/// </summary>
internal static class ExitCodes
{
    /// <summary>The command completed and the package is committed and certified.</summary>
    internal const int Success = 0;

    /// <summary>The invocation itself was invalid; nothing was published.</summary>
    internal const int InvalidInvocation = 1;

    /// <summary>Certification rejected the package; nothing was committed.</summary>
    internal const int CertificationFailed = 4;

    /// <summary>A package or publication is structurally corrupt.</summary>
    internal const int StructuralCorruption = 5;
}
