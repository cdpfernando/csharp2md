using Csharp2Md.Core.Loading;

namespace Csharp2Md.Core.Tests.Loading;

[Trait("Category", "Integration")]
public sealed class SolutionLoaderTests
{
    // Acme.Orders.slnx bundles a healthy project (Acme.Orders, restored), a healthy dependency
    // (Acme.Shared.Contracts, restored), Acme.Broken (unrestorable Sdk="Acme.NonExistent.Sdk"), and
    // a reference to a project file that doesn't exist on disk at all (Acme.DoesNotExist).
    private static readonly string OrdersSolutionPath =
        TestPaths.SyntheticSolution(Path.Combine("Acme.Orders", "Acme.Orders.slnx"));

    // Acme.Payments.slnx bundles Acme.Payments (deliberately never restored — real Grpc.AspNetCore +
    // protobuf-codegen dependency) and Acme.Shared.Contracts (restored).
    private static readonly string PaymentsSolutionPath =
        TestPaths.SyntheticSolution(Path.Combine("Acme.Payments", "Acme.Payments.slnx"));

    [Fact]
    public async Task LoadAsync_HealthyProject_IsClassifiedOk()
    {
        var loader = new SolutionLoader();

        var loaded = await loader.LoadAsync(OrdersSolutionPath);

        var orders = Assert.Single(loaded.Report.Projects, p => p.ProjectName == "Acme.Orders");
        Assert.Equal(ProjectLoadStatus.Ok, orders.Status);
    }

    [Fact]
    public async Task LoadAsync_HealthyDependencyProject_IsClassifiedOk()
    {
        var loader = new SolutionLoader();

        var loaded = await loader.LoadAsync(OrdersSolutionPath);

        var sharedContracts = Assert.Single(loaded.Report.Projects, p => p.ProjectName == "Acme.Shared.Contracts");
        Assert.Equal(ProjectLoadStatus.Ok, sharedContracts.Status);
    }

    [Fact]
    public async Task LoadAsync_ProjectWithUnresolvableSdk_IsClassifiedDegraded()
    {
        var loader = new SolutionLoader();

        var loaded = await loader.LoadAsync(OrdersSolutionPath);

        var broken = Assert.Single(loaded.Report.Projects, p => p.ProjectName == "Acme.Broken");
        Assert.Equal(ProjectLoadStatus.Degraded, broken.Status);
        Assert.NotEmpty(broken.Messages);
    }

    [Fact]
    public async Task LoadAsync_DegradedProject_DoesNotAbortRemainingProjects()
    {
        var loader = new SolutionLoader();

        var loaded = await loader.LoadAsync(OrdersSolutionPath);

        Assert.Equal(3, loaded.Report.Projects.Count);
    }

    [Fact]
    public async Task LoadAsync_ReferenceToNonExistentProjectFile_IsSkippedNotThrown()
    {
        var loader = new SolutionLoader();

        // SkipUnrecognizedProjects = true (P1-06): without it, OpenSolutionAsync throws for a
        // project reference whose file doesn't exist on disk (per the API's own doc comment).
        var loaded = await loader.LoadAsync(OrdersSolutionPath);

        Assert.DoesNotContain(loaded.Report.Projects, p => p.ProjectName.Contains("DoesNotExist"));
        Assert.Equal(3, loaded.Report.Projects.Count);
    }

    [Fact]
    public async Task LoadAsync_UnrestoredProjectWithRealPackageDependency_IsClassifiedPossibleMissingRestore()
    {
        var loader = new SolutionLoader();

        var loaded = await loader.LoadAsync(PaymentsSolutionPath);

        var payments = Assert.Single(loaded.Report.Projects, p => p.ProjectName == "Acme.Payments");
        Assert.Equal(ProjectLoadStatus.PossibleMissingRestore, payments.Status);
        Assert.NotEmpty(payments.Messages);
    }

    [Fact]
    public async Task LoadAsync_UnrestoredProjectsHealthyDependency_IsStillClassifiedOk()
    {
        var loader = new SolutionLoader();

        var loaded = await loader.LoadAsync(PaymentsSolutionPath);

        var sharedContracts = Assert.Single(loaded.Report.Projects, p => p.ProjectName == "Acme.Shared.Contracts");
        Assert.Equal(ProjectLoadStatus.Ok, sharedContracts.Status);
    }

    [Fact]
    public async Task LoadAsync_ReturnsUnderlyingSolutionForDownstreamAnalysis()
    {
        var loader = new SolutionLoader();

        var loaded = await loader.LoadAsync(OrdersSolutionPath);

        Assert.NotNull(loaded.Solution);
        Assert.NotEmpty(loaded.Solution.Projects);
    }
}
