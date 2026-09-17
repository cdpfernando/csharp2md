using Csharp2Md.Core.Analysis.Semantics;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class ProjectVariantWorkspaceTests
{
    private static readonly string SyntheticRoot =
        Path.Combine(CoreTestPaths.RepoRoot, "fixtures", "SyntheticSolution");

    private static readonly string PaymentsSolutionPath =
        Path.Combine(SyntheticRoot, "Acme.Payments", "Acme.Payments.slnx");

    [Fact]
    [Trait("Requirement", "VAR-01")]
    [Trait("Requirement", "VAR-02")]
    public async Task OpenAsync_AppliesOnlyTheRequestedTargetFrameworkProperty()
    {
        var variant = new PlannedProjectVariant("Acme.Payments/Acme.Payments.csproj", "net10.0");

        await using var workspace = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant);

        Assert.Equal(variant, workspace.Variant);
        Assert.Equal("Acme.Payments", workspace.RootProject.Name);
        Assert.Contains(
            workspace.RootProject.ProjectReferences,
            reference => workspace.Solution.GetProject(reference.ProjectId)?.Name == "Acme.Shared.Contracts");
        Assert.Contains("net10.0", workspace.RootProject.Name + workspace.Variant.TargetFramework, StringComparison.Ordinal);
        Assert.Equal("net10.0", workspace.Variant.TargetFramework);
    }

    [Fact]
    [Trait("Requirement", "DEP-01")]
    public async Task ReferencedProjects_NamesOnlyTheDirectReference_NotATransitiveOne()
    {
        // Acme.Shipping.Tests directly references only Acme.Shipping; Acme.Shipping in turn references
        // Acme.Shared.Contracts. Roslyn must load Contracts to compile the chain, so before the fix
        // ReferencedProjects() (Solution.Projects minus the root) named it too, as if Shipping.Tests
        // referenced it directly.
        var variant = new PlannedProjectVariant("Acme.Shipping.Tests/Acme.Shipping.Tests.csproj", "net10.0");

        await using var workspace = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant);

        var referenced = workspace.ReferencedProjects().Select(project => project.Name).ToArray();
        Assert.Equal(["Acme.Shipping"], referenced);
    }

    [Fact]
    [Trait("Requirement", "VAR-04")]
    public async Task OpenAsync_ReferencedProjectsSupportCompilationButAreNotRoots()
    {
        var variant = new PlannedProjectVariant("Acme.Payments/Acme.Payments.csproj", "net10.0");

        await using var workspace = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant);
        var compilation = await workspace.GetRootCompilationAsync();

        Assert.NotNull(compilation);
        Assert.True(workspace.IsRootProject(workspace.RootProject.Id));
        Assert.Contains(workspace.ReferencedProjects(), project => project.Name == "Acme.Shared.Contracts");
        Assert.All(workspace.ReferencedProjects(), project => Assert.False(workspace.IsRootProject(project.Id)));
    }

    [Fact]
    [Trait("Requirement", "VAR-01")]
    public async Task OpenAsync_TwoVariants_UseIsolatedWorkspaces()
    {
        var first = new PlannedProjectVariant("Acme.Payments/Acme.Payments.csproj", "net10.0");
        var second = new PlannedProjectVariant("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", "net10.0");

        await using var left = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, first);
        await using var right = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, second);

        Assert.NotEqual(left.RootProject.Id, right.RootProject.Id);
        Assert.Equal("Acme.Payments", left.RootProject.Name);
        Assert.Equal("Acme.Shared.Contracts", right.RootProject.Name);
        Assert.True(left.IsRootProject(left.RootProject.Id));
        Assert.False(left.IsRootProject(right.RootProject.Id));
    }

    [Fact]
    [Trait("Requirement", "CRT-06")]
    public async Task DisposeAsync_AfterSuccessfulOpen_MarksWorkspaceDisposed()
    {
        var variant = new PlannedProjectVariant("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", "net10.0");
        var workspace = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant);

        await workspace.DisposeAsync();

        Assert.True(workspace.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = workspace.Solution);
        Assert.Throws<ObjectDisposedException>(() => workspace.IsRootProject(workspace.RootProject.Id));
    }

    [Fact]
    [Trait("Requirement", "CRT-06")]
    public async Task OpenAsync_MissingProject_DisposesWorkspaceAndThrows()
    {
        var variant = new PlannedProjectVariant("Acme.DoesNotExist/Acme.DoesNotExist.csproj", "net10.0");

        await Assert.ThrowsAnyAsync<Exception>(
            () => ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant));
    }

    [Fact]
    [Trait("Requirement", "VAR-02")]
    public async Task DiscoverThenOpen_PaymentsPlan_OpensEachPairInIsolation()
    {
        var plan = await ProjectVariantPlanner.DiscoverAsync(PaymentsSolutionPath, SyntheticRoot);

        Assert.NotEmpty(plan);
        foreach (var variant in plan)
        {
            await using var workspace = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant);
            Assert.Equal(variant.TargetFramework, workspace.Variant.TargetFramework);
            Assert.True(workspace.IsRootProject(workspace.RootProject.Id));
            Assert.All(workspace.ReferencedProjects(), project => Assert.False(workspace.IsRootProject(project.Id)));
        }
    }

    [Fact]
    [Trait("Requirement", "PUB-08")]
    public async Task OpenAsync_CancelledBeforeOpen_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var variant = new PlannedProjectVariant("Acme.Payments/Acme.Payments.csproj", "net10.0");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant, cts.Token));
    }

    [Fact]
    [Trait("Requirement", "VAR-04")]
    public async Task GetRootCompilationAsync_UsesOnlyTheOpenedRootProject()
    {
        var variant = new PlannedProjectVariant("Acme.Payments/Acme.Payments.csproj", "net10.0");
        await using var workspace = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant);

        var compilation = await workspace.GetRootCompilationAsync();

        Assert.NotNull(compilation);
        Assert.Contains("Acme.Payments", compilation!.AssemblyName, StringComparison.OrdinalIgnoreCase);
    }
}
