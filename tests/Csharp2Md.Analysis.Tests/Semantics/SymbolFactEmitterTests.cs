using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;

namespace Csharp2Md.Analysis.Tests.Semantics;

public sealed class SymbolFactEmitterTests
{
    [Fact]
    [Trait("Requirement", "ROSE-14")]
    public async Task ExecuteAsync_AcmeOrders_EmitsPlaceOrderAsyncSymbolOnAccumulator()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

            var snapshot = context.Accumulator.ToSnapshot();
            var project = Assert.Single(
                snapshot.Facts.OfType<Project>(),
                candidate => candidate.Id.Value.Contains("Acme.Orders.csproj", StringComparison.Ordinal));
            var placeOrder = Assert.Single(
                snapshot.Facts.OfType<Symbol>(),
                symbol => symbol.Signature.Value.Contains("metadata=PlaceOrderAsync", StringComparison.Ordinal));

            Assert.Equal(project.Id, placeOrder.OwningProject);
            Assert.Contains("kind=method", placeOrder.Signature.Value, StringComparison.Ordinal);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-14")]
    public async Task Emit_SameSymbolFromSecondTfm_DoesNotAddSecondIdentity()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            Assert.NotNull(context.BoundSolution);

            var firstCount = PlaceOrderAsyncCount(context);
            Assert.Equal(1, firstCount);

            SymbolFactEmitter.Emit(context.BoundSolution, context.Accumulator, context.SolutionPath);

            Assert.Equal(1, PlaceOrderAsyncCount(context));
            Assert.False(context.Accumulator.StructuralCorruption);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-14")]
    public async Task ExecuteAsync_DeclaredShapes_EmitsTypeMethodPropertyFieldEventConstructorLocalFunctionAndLambda()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-symbols-");
        try
        {
            var solutionPath = WriteDeclaredShapesSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                var result = await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
                Assert.False(result.AbortPublication, context.Detail);

                var signatures = context.Accumulator.ToSnapshot().Facts.OfType<Symbol>()
                    .Select(symbol => symbol.Signature.Value)
                    .ToArray();

                Assert.Contains(signatures, value => value.Contains("metadata=Declared", StringComparison.Ordinal)
                    && value.Contains("kind=namedtype", StringComparison.Ordinal));
                Assert.Contains(signatures, value => value.Contains("metadata=Method", StringComparison.Ordinal)
                    && value.Contains("kind=method", StringComparison.Ordinal));
                Assert.Contains(signatures, value => value.Contains("metadata=Prop", StringComparison.Ordinal)
                    && value.Contains("kind=property", StringComparison.Ordinal));
                Assert.Contains(signatures, value => value.Contains("metadata=Field", StringComparison.Ordinal)
                    && value.Contains("kind=field", StringComparison.Ordinal));
                Assert.Contains(signatures, value => value.Contains("metadata=Changed", StringComparison.Ordinal)
                    && value.Contains("kind=event", StringComparison.Ordinal));
                Assert.Contains(signatures, value => value.Contains("metadata=.ctor", StringComparison.Ordinal)
                    && value.Contains("kind=method", StringComparison.Ordinal));
                Assert.Contains(signatures, value => value.Contains("metadata=Local", StringComparison.Ordinal)
                    && value.Contains("kind=method", StringComparison.Ordinal));
                Assert.Contains(signatures, value => value.Contains("metadata=identifiable", StringComparison.Ordinal)
                    && value.Contains("kind=method", StringComparison.Ordinal));
            }
            finally
            {
                context.BoundSolution?.Dispose();
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-15")]
    public async Task ExecuteAsync_AcmeOrders_PlaceOrderAsyncFacetsContainCallable()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

            var placeOrder = Assert.Single(
                context.Accumulator.ToSnapshot().Facts.OfType<Symbol>(),
                symbol => symbol.Signature.Value.Contains("metadata=PlaceOrderAsync", StringComparison.Ordinal));

            Assert.Contains(SymbolFacet.Callable, placeOrder.Facets.Facets);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-15")]
    public async Task ExecuteAsync_AcmeOrders_OrdersControllerTypeFacetsDoNotContainCallable()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

            var controller = Assert.Single(
                context.Accumulator.ToSnapshot().Facts.OfType<Symbol>(),
                symbol => symbol.Signature.Value.Contains("metadata=OrdersController", StringComparison.Ordinal)
                    && symbol.Signature.Value.Contains("kind=namedtype", StringComparison.Ordinal));

            Assert.DoesNotContain(SymbolFacet.Callable, controller.Facets.Facets);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-15")]
    public async Task ExecuteAsync_DeclaredShapes_CallableOnlyOnMethodsConstructorsLocalFunctionsAndLambdas()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-callable-");
        try
        {
            var solutionPath = WriteDeclaredShapesSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

                var symbols = context.Accumulator.ToSnapshot().Facts.OfType<Symbol>().ToArray();
                Assert.Contains(SymbolFacet.Callable, FacetsNamed(symbols, "Method"));
                Assert.Contains(SymbolFacet.Callable, FacetsNamed(symbols, ".ctor"));
                Assert.Contains(SymbolFacet.Callable, FacetsNamed(symbols, "Local"));
                Assert.Contains(SymbolFacet.Callable, FacetsNamed(symbols, "identifiable"));
                Assert.DoesNotContain(SymbolFacet.Callable, FacetsNamed(symbols, "Declared", "namedtype"));
                Assert.DoesNotContain(SymbolFacet.Callable, FacetsNamed(symbols, "Prop", "property"));
                Assert.DoesNotContain(SymbolFacet.Callable, FacetsNamed(symbols, "Field", "field"));
                Assert.DoesNotContain(SymbolFacet.Callable, FacetsNamed(symbols, "Changed", "event"));
            }
            finally
            {
                context.BoundSolution?.Dispose();
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static ImmutableArray<SymbolFacet> FacetsNamed(
        IReadOnlyList<Symbol> symbols,
        string metadata,
        string? kind = null)
    {
        var match = Assert.Single(
            symbols,
            symbol => symbol.Signature.Value.Contains("metadata=" + metadata, StringComparison.Ordinal)
                && (kind is null || symbol.Signature.Value.Contains("kind=" + kind, StringComparison.Ordinal)));
        return match.Facets.Facets;
    }

    private static int PlaceOrderAsyncCount(PipelineContext context) =>
        context.Accumulator.ToSnapshot().Facts.OfType<Symbol>()
            .Count(symbol => symbol.Signature.Value.Contains("metadata=PlaceOrderAsync", StringComparison.Ordinal));

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

    private static string WriteDeclaredShapesSolution(string root)
    {
        var projectDir = Path.Combine(root, "App");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(projectDir, "Declared.cs"),
            """
            using System;
            class Declared
            {
                public int Field;
                public int Prop { get; set; }
                public event Action? Changed;
                public Declared() {}
                public void Method()
                {
                    void Local() {}
                    Action identifiable = () => { };
                    Local();
                    identifiable();
                }
            }
            """);

        var solutionPath = Path.Combine(root, "App.slnx");
        File.WriteAllText(
            solutionPath,
            """
            <Solution>
              <Project Path="App/App.csproj" />
            </Solution>
            """);
        return solutionPath;
    }
}
