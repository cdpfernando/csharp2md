using Csharp2Md.Analysis.Semantics;

namespace Csharp2Md.Analysis.Tests.Semantics;

public sealed class MsBuildWorkspaceFactoryTests
{
    [Fact]
    [Trait("Requirement", "ROSE-24")]
    public async Task Open_AcmeOrdersWithDebugAndFixtureTfm_ReturnsDisposableLease()
    {
        var factory = new MsBuildWorkspaceFactory();
        var solutionPath = AcmeOrdersSolutionPath();

        await using var lease = await factory.Open(solutionPath, "Debug", "net10.0", CancellationToken.None);

        Assert.NotNull(lease);
        Assert.IsAssignableFrom<IDisposable>(lease);
        Assert.IsAssignableFrom<IAsyncDisposable>(lease);
    }

    [Fact]
    [Trait("Requirement", "ROSE-24")]
    public async Task Open_SolutionListingAcmeDoesNotExist_DoesNotThrow()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        Assert.Contains(
            "Acme.DoesNotExist",
            File.ReadAllText(solutionPath),
            StringComparison.Ordinal);

        var factory = new MsBuildWorkspaceFactory();

        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var lease = await factory.Open(solutionPath, "Debug", "net10.0", CancellationToken.None);
        });

        Assert.Null(exception);
    }

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }
}
