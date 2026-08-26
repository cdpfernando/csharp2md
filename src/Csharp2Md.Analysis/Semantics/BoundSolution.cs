using Microsoft.CodeAnalysis;

namespace Csharp2Md.Analysis.Semantics;

internal sealed class BoundSolution : IAsyncDisposable, IDisposable
{
    private int _disposed;

    internal BoundSolution(
        ImmutableArray<MsBuildWorkspaceLease> leases,
        ImmutableArray<Compilation> compilations)
    {
        Leases = leases;
        Compilations = compilations;
    }

    internal ImmutableArray<MsBuildWorkspaceLease> Leases { get; }

    internal ImmutableArray<Compilation> Compilations { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (var lease in Leases)
        {
            lease.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
