using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class SolutionScopedRetrievalTests
{
    [Fact]
    [Trait("Requirement", "NAV-05")]
    public void RetrievalModel_TwoSolutionsWithSameLocalHandles_KeepsCausalDataScoped()
    {
        var model = Model(Solution("orders"), Solution("shipping"));

        Assert.DoesNotContain(
            typeof(RetrievalModel).GetProperties(),
            property => property.Name is "Indexes" or "Dependencies" or "Measures" or "RetainedGraph");
        Assert.Equal(2, model.Solutions.Length);
        Assert.All(model.Solutions, solution => Assert.Equal("component:shared", Assert.Single(solution.Roots).Value));

        var orders = Assert.Single(model.Solutions, solution => solution.Solution.LogicalRelativePath == "src/orders.sln");
        var shipping = Assert.Single(model.Solutions, solution => solution.Solution.LogicalRelativePath == "src/shipping.sln");
        Assert.Equal("component:orders-target", Assert.Single(orders.Dependencies).Target.Value);
        Assert.Equal(DependencyCategory.Contract, Assert.Single(orders.Dependencies).Category);
        Assert.Equal("component:shipping-target", Assert.Single(shipping.Dependencies).Target.Value);
        Assert.Equal(DependencyCategory.Messaging, Assert.Single(shipping.Dependencies).Category);
        Assert.Equal(11, Assert.Single(orders.Measures).FanOut);
        Assert.Equal(22, Assert.Single(shipping.Measures).FanOut);
    }

    [Fact]
    [Trait("Requirement", "PKG-01")]
    [Trait("Requirement", "STO-04")]
    public void Write_TwoSolutionsWithSameHandlesAndIndexKinds_EmitsIsolatedArtifacts()
    {
        var machine = MachineArtifactWriter.Write(Model(Solution("orders"), Solution("shipping")), includeTests: false);
        var orders = Assert.Single(machine.Manifest.Solutions, solution => solution.LogicalRelativePath == "src/orders.sln");
        var shipping = Assert.Single(machine.Manifest.Solutions, solution => solution.LogicalRelativePath == "src/shipping.sln");

        Assert.Matches("^sol_[0-9a-v]{16}$", orders.Id.Value);
        Assert.Matches("^sol_[0-9a-v]{16}$", shipping.Id.Value);
        Assert.NotEqual(orders.Id, shipping.Id);
        Assert.Equal(Assert.Single(orders.Roots).Handle, Assert.Single(shipping.Roots).Handle);
        Assert.Equal(
            orders.Indexes.Select(index => index.Kind),
            shipping.Indexes.Select(index => index.Kind));
        Assert.All(
            orders.Indexes,
            index =>
            {
                Assert.StartsWith($"solutions/{orders.Id.Value}/indexes/", index.EntryPath, StringComparison.Ordinal);
                Assert.EndsWith(".json", index.EntryPath, StringComparison.Ordinal);
                Assert.DoesNotContain(".000000.", index.EntryPath, StringComparison.Ordinal);
            });

        var ordersDependencyIndex = Read<NavigationIndexData>(machine, Entry(orders, NavigationIndexKind.Outgoing));
        var shippingDependencyIndex = Read<NavigationIndexData>(machine, Entry(shipping, NavigationIndexKind.Outgoing));
        var ordersMeasureIndex = Read<NavigationIndexData>(machine, Entry(orders, NavigationIndexKind.Measures));
        var shippingMeasureIndex = Read<NavigationIndexData>(machine, Entry(shipping, NavigationIndexKind.Measures));
        Assert.StartsWith($"solutions/{orders.Id.Value}/measures/", ordersDependencyIndex.ArtifactPath, StringComparison.Ordinal);
        Assert.StartsWith($"solutions/{shipping.Id.Value}/measures/", shippingDependencyIndex.ArtifactPath, StringComparison.Ordinal);
        var ordersDependencies = Read<ImmutableArray<AggregatedDependency>>(machine, ordersDependencyIndex.ArtifactPath);
        var shippingDependencies = Read<ImmutableArray<AggregatedDependency>>(machine, shippingDependencyIndex.ArtifactPath);
        var ordersMeasures = Read<ImmutableArray<ScopeMeasures>>(machine, ordersMeasureIndex.ArtifactPath);
        var shippingMeasures = Read<ImmutableArray<ScopeMeasures>>(machine, shippingMeasureIndex.ArtifactPath);

        Assert.Equal("component:orders-target", Assert.Single(ordersDependencies).Target.Value);
        Assert.DoesNotContain(ordersDependencies, dependency => dependency.Target.Value == "component:shipping-target");
        Assert.Equal("component:shipping-target", Assert.Single(shippingDependencies).Target.Value);
        Assert.DoesNotContain(shippingDependencies, dependency => dependency.Target.Value == "component:orders-target");
        Assert.Equal(11, Assert.Single(ordersMeasures).FanOut);
        Assert.Equal(22, Assert.Single(shippingMeasures).FanOut);
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    public void Write_PermutedSolutions_ProducesIdenticalOrderPathsAndBytes()
    {
        var orders = Solution("orders");
        var shipping = Solution("shipping");

        var first = MachineArtifactWriter.Write(Model(orders, shipping), includeTests: false);
        var second = MachineArtifactWriter.Write(Model(shipping, orders), includeTests: false);

        Assert.Equal(first.Manifest.Solutions.Select(solution => solution.Id), second.Manifest.Solutions.Select(solution => solution.Id));
        Assert.Equal(first.Artifacts.Select(artifact => artifact.Path.Value), second.Artifacts.Select(artifact => artifact.Path.Value));
        Assert.Equal(first.Artifacts.Length, second.Artifacts.Length);
        for (var index = 0; index < first.Artifacts.Length; index++)
        {
            Assert.True(
                first.Artifacts[index].Payload.AsSpan().SequenceEqual(second.Artifacts[index].Payload.AsSpan()),
                $"Payload differs at '{first.Artifacts[index].Path.Value}'.");
        }
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    [Trait("Requirement", "STO-04")]
    public void Read_TwoSolutionsWithDifferentCausalData_DoesNotLeakAcrossSolutions()
    {
        var model = Model(Solution("orders"), Solution("shipping"));
        var machine = MachineArtifactWriter.Write(model, includeTests: false);
        var artifacts = machine.Artifacts
            .AddRange(MarkdownRenderer.Render(model, machine.Manifest))
            .ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);

        var restored = RetrievalModelReader.Read(artifacts);

        var orders = Assert.Single(restored.Solutions, solution => solution.Solution.LogicalRelativePath == "src/orders.sln");
        var shipping = Assert.Single(restored.Solutions, solution => solution.Solution.LogicalRelativePath == "src/shipping.sln");
        Assert.Equal("component:orders-target", Assert.Single(orders.Dependencies).Target.Value);
        Assert.Equal(DependencyCategory.Contract, Assert.Single(orders.Dependencies).Category);
        Assert.Equal("component:shipping-target", Assert.Single(shipping.Dependencies).Target.Value);
        Assert.Equal(DependencyCategory.Messaging, Assert.Single(shipping.Dependencies).Category);
        Assert.Equal(11, Assert.Single(orders.Measures).FanOut);
        Assert.Equal(22, Assert.Single(shipping.Measures).FanOut);
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    [Trait("Requirement", "STO-04")]
    public void Read_CorruptionOnlyInSecondSolution_ReportsSecondSolutionArtifact()
    {
        var model = Model(Solution("orders"), Solution("shipping"));
        var machine = MachineArtifactWriter.Write(model, includeTests: false);
        var second = machine.Manifest.Solutions[1];
        var corruptedPath = Entry(second, NavigationIndexKind.Outgoing);
        var artifacts = machine.Artifacts
            .AddRange(MarkdownRenderer.Render(model, machine.Manifest))
            .ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        artifacts[corruptedPath] = "not-json"u8.ToArray().ToImmutableArray();

        var exception = Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts));

        Assert.Equal(corruptedPath, exception.Artifact);
        Assert.Contains(second.Id.Value, exception.Artifact, StringComparison.Ordinal);
    }

    private static RetrievalModel Model(params SolutionRetrievalModel[] solutions) => new(solutions.ToImmutableArray());

    private static SolutionRetrievalModel Solution(string name)
    {
        var target = new EntityHandle($"component:{name}-target");
        var category = name == "orders" ? DependencyCategory.Contract : DependencyCategory.Messaging;
        var fanOut = name == "orders" ? 11 : 22;
        return new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution(name, $"src/{name}.sln"),
            [new EntityHandle("component:shared")],
            [new AggregatedDependency(
                AggregationScope.Component,
                new EntityHandle("component:shared"),
                target,
                category,
                DependencyNature.Direct,
                1,
                [],
                [],
                [])],
            [new ScopeMeasures(
                AggregationScope.Component,
                new EntityHandle("component:shared"),
                fanIn: 0,
                fanOut: fanOut,
                crossComponentEdges: 1,
                [],
                [],
                new GapCounts(0, 0, 0))],
            retainedGraph: null);
    }

    private static string Entry(SolutionManifestEntry solution, NavigationIndexKind kind) =>
        Assert.Single(solution.Indexes, index => index.Kind == kind).EntryPath;

    private static T Read<T>(MachineArtifactSet machine, string path) =>
        CanonicalJson.Read<T>(Assert.Single(machine.Artifacts, artifact => artifact.Path.Value == path).Payload.AsSpan());
}
