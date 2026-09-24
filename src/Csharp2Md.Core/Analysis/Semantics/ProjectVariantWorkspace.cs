using Csharp2Md.Core.Analysis.Inventory;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Csharp2Md.Core.Analysis.Semantics;

internal sealed class ProjectVariantWorkspace : IAsyncDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private bool _disposed;

    private ProjectVariantWorkspace(
        MSBuildWorkspace workspace,
        PlannedProjectVariant variant,
        Project rootProject)
    {
        _workspace = workspace;
        Variant = variant;
        RootProject = rootProject;
    }

    public PlannedProjectVariant Variant { get; }

    public Project RootProject { get; }

    public Solution Solution
    {
        get
        {
            ThrowIfDisposed();
            return _workspace.CurrentSolution;
        }
    }

    public bool IsDisposed => _disposed;

    public static async Task<ProjectVariantWorkspace> OpenAsync(
        string authorizedRoot,
        PlannedProjectVariant variant,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedRoot);
        ArgumentNullException.ThrowIfNull(variant);
        cancellationToken.ThrowIfCancellationRequested();

        var root = PathGuard.Normalize(authorizedRoot);
        var projectPath = Path.GetFullPath(Path.Combine(root, variant.ProjectLogicalRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        PathGuard.RejectEscapes(root, projectPath);

        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TargetFramework"] = variant.TargetFramework,
        };

        var workspace = MSBuildWorkspace.Create(properties);
        workspace.SkipUnrecognizedProjects = true;
        try
        {
            var project = await workspace.OpenProjectAsync(projectPath, progress: null, cancellationToken)
                .ConfigureAwait(false);
            return new ProjectVariantWorkspace(workspace, variant, project);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    public async Task<Compilation?> GetRootCompilationAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var project = Solution.GetProject(RootProject.Id)
            ?? throw new InvalidOperationException("The root project left the workspace.");
        return await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
    }

    public bool IsRootProject(ProjectId projectId)
    {
        ThrowIfDisposed();
        return projectId == RootProject.Id;
    }

    public IEnumerable<Project> ReferencedProjects()
    {
        ThrowIfDisposed();
        var solution = Solution;
        var root = solution.GetProject(RootProject.Id)
            ?? throw new InvalidOperationException("The root project left the workspace.");
        return root.ProjectReferences
            .Select(reference => solution.GetProject(reference.ProjectId))
            .Where(static project => project is not null)!;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _workspace.Dispose();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
