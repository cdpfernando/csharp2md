using System.Security.Cryptography;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ObservationMaterializerTests
{
    [Fact]
    [Trait("Requirement", "ROSE-50")]
    public async Task ExtractInto_AcmeOrders_LocatorIsRelativeWithForwardSlashesAndOneBasedSpan()
    {
        var observations = await ExtractAcmeOrdersAsync();
        Assert.NotEmpty(observations);

        Assert.All(
            observations,
            observation =>
            {
                Assert.Contains('/', observation.Locator.RelativePath);
                Assert.DoesNotContain('\\', observation.Locator.RelativePath);
                Assert.False(Path.IsPathRooted(observation.Locator.RelativePath));
                Assert.False(HasDrivePrefix(observation.Locator.RelativePath));
                Assert.True(
                    observation.Locator.Span.StartLine >= 1,
                    $"Span start line was {observation.Locator.Span.StartLine}.");
                Assert.StartsWith("id1:document", observation.Locator.Document.Value, StringComparison.Ordinal);
                Assert.False(HasDrivePrefix(observation.Locator.Document.Value));
                Assert.Equal(1, observation.ExtractorVersion.Value);
            });
    }

    [Fact]
    [Trait("Requirement", "ROSE-50")]
    [Trait("Requirement", "ROSE-51")]
    public async Task ExtractInto_AcmeOrders_DocumentHashMatchesSha256OfOnDiskFileBytes()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var observation = observations.First(candidate =>
            candidate.Locator.RelativePath.Replace('\\', '/')
                .EndsWith("Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal));
        var absolute = Path.GetFullPath(
            Path.Combine(
                AnalysisTestPaths.RepoRoot,
                "fixtures",
                "SyntheticSolution",
                observation.Locator.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.True(File.Exists(absolute), $"Expected source file at '{absolute}'.");

        var expected = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(absolute)));

        Assert.Equal(expected, observation.DocumentHash.Value);
        Assert.Equal(64, observation.DocumentHash.Value.Length);
        Assert.Equal(expected, ObservationMaterializer.HashFileBytes(absolute).Value);
    }

    [Fact]
    [Trait("Requirement", "ROSE-50")]
    [Trait("Requirement", "ROSE-51")]
    public void HashFileBytes_SameContentAtDifferentClonePaths_ProducesTheSameDigest()
    {
        var fixtureFile = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Api",
            "OrdersController.cs");
        Assert.True(File.Exists(fixtureFile), $"Expected fixture at '{fixtureFile}'.");
        var bytes = File.ReadAllBytes(fixtureFile);
        var clone = Directory.CreateTempSubdirectory("csharp2md-hash-clone-");
        try
        {
            var cloneFile = Path.Combine(clone.FullName, "OrdersController.cs");
            File.WriteAllBytes(cloneFile, bytes);

            var original = ObservationMaterializer.HashFileBytes(fixtureFile);
            var copied = ObservationMaterializer.HashFileBytes(cloneFile);

            Assert.Equal(original, copied);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), original.Value);
        }
        finally
        {
            clone.Delete(recursive: true);
        }
    }

    private static bool HasDrivePrefix(string value) =>
        value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':';

    private static async Task<Observation[]> ExtractAcmeOrdersAsync()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            AlwaysWhenBindableWalker.ExtractInto(context, CancellationToken.None);
            return [.. context.Accumulator.ToSnapshot().Observations];
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }
}
