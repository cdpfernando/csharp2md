using System.Runtime.CompilerServices;

namespace Csharp2Md.Core.Tests;

internal static class VerifySetup
{
    /// <summary>
    /// Keeps Verify non-interactive: a mismatched snapshot fails the gate instead of popping a
    /// diff tool. Snapshots are never auto-accepted; a new <c>.received.txt</c> is reviewed and
    /// promoted to <c>.verified.txt</c> by hand.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize() => DiffEngine.DiffRunner.Disabled = true;
}
