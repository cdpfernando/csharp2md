using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Csharp2Md.Analysis.Semantics;

internal interface IMsBuildWorkspaceFactory
{
    ValueTask<MsBuildWorkspaceLease> Open(
        string solutionPath,
        string configuration,
        string targetFramework,
        CancellationToken cancellationToken);
}

internal sealed class MsBuildWorkspaceFactory : IMsBuildWorkspaceFactory
{
    public async ValueTask<MsBuildWorkspaceLease> Open(
        string solutionPath,
        string configuration,
        string targetFramework,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);

        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Configuration"] = configuration,
            ["TargetFramework"] = targetFramework,
        };

        var workspace = MSBuildWorkspace.Create(properties);
        workspace.SkipUnrecognizedProjects = true;
        try
        {
            await workspace.OpenSolutionAsync(solutionPath, progress: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }

        return new MsBuildWorkspaceLease(workspace);
    }
}

internal sealed class MsBuildWorkspaceLease : IAsyncDisposable, IDisposable
{
    private readonly MSBuildWorkspace _workspace;

    internal MsBuildWorkspaceLease(MSBuildWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
    }

    internal Solution Solution => _workspace.CurrentSolution;

    internal IReadOnlyCollection<WorkspaceDiagnostic> Diagnostics => _workspace.Diagnostics;

    public void Dispose() => _workspace.Dispose();

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
