using System.Security.Cryptography;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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

    [Fact]
    [Trait("Requirement", "GCPC-020")]
    public async Task ExecuteAsync_AccessibilityShapes_PublicMethodOnPublicType_CarriesExternallyReachable()
    {
        var symbols = await EmitAccessibilityShapesAsync();

        Assert.Contains(SymbolFacet.ExternallyReachable, FacetsNamed(symbols, "PublicMethod"));
    }

    [Fact]
    [Trait("Requirement", "GCPC-020")]
    public async Task ExecuteAsync_AccessibilityShapes_PrivateMethod_DoesNotCarryExternallyReachable()
    {
        var symbols = await EmitAccessibilityShapesAsync();

        Assert.DoesNotContain(SymbolFacet.ExternallyReachable, FacetsNamed(symbols, "PrivateMethod"));
    }

    [Fact]
    [Trait("Requirement", "GCPC-020")]
    public async Task ExecuteAsync_AccessibilityShapes_ProtectedMethodOnPublicType_CarriesExternallyReachable()
    {
        var symbols = await EmitAccessibilityShapesAsync();

        Assert.Contains(SymbolFacet.ExternallyReachable, FacetsNamed(symbols, "ProtectedMethod"));
    }

    [Fact]
    [Trait("Requirement", "GCPC-020")]
    public async Task ExecuteAsync_AccessibilityShapes_InternalMethodOnPublicType_DoesNotCarryExternallyReachable()
    {
        var symbols = await EmitAccessibilityShapesAsync();

        Assert.DoesNotContain(SymbolFacet.ExternallyReachable, FacetsNamed(symbols, "InternalMethod"));
    }

    [Fact]
    [Trait("Requirement", "GCPC-020")]
    public async Task ExecuteAsync_AccessibilityShapes_PublicMethodOnInternalNestedType_DoesNotCarryExternallyReachable()
    {
        var symbols = await EmitAccessibilityShapesAsync();

        Assert.DoesNotContain(SymbolFacet.ExternallyReachable, FacetsNamed(symbols, "OnInternalNested"));
    }

    [Fact]
    [Trait("Requirement", "GCPC-020")]
    public async Task ExecuteAsync_AccessibilityShapes_PublicMethodOnPrivateNestedType_DoesNotCarryExternallyReachable()
    {
        var symbols = await EmitAccessibilityShapesAsync();

        Assert.DoesNotContain(SymbolFacet.ExternallyReachable, FacetsNamed(symbols, "OnPrivateNested"));
    }

    [Fact]
    [Trait("Requirement", "ROSE-16")]
    public async Task ExecuteAsync_AcmeOrders_OrdersControllerIsNotTaggedController()
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

            Assert.DoesNotContain(SymbolFacet.Controller, controller.Facets.Facets);
            Assert.DoesNotContain(SymbolFacet.Handler, controller.Facets.Facets);
            Assert.DoesNotContain(SymbolFacet.Repository, controller.Facets.Facets);
            Assert.DoesNotContain(SymbolFacet.Client, controller.Facets.Facets);
            Assert.DoesNotContain(SymbolFacet.Service, controller.Facets.Facets);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-16")]
    public async Task ExecuteAsync_AcmeOrders_OrderRepositoryIsNotTaggedRepository()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

            var repository = Assert.Single(
                context.Accumulator.ToSnapshot().Facts.OfType<Symbol>(),
                symbol => symbol.Signature.Value.Contains("metadata=OrderRepository", StringComparison.Ordinal)
                    && symbol.Signature.Value.Contains("kind=namedtype", StringComparison.Ordinal));

            Assert.DoesNotContain(SymbolFacet.Repository, repository.Facets.Facets);
            Assert.DoesNotContain(SymbolFacet.Controller, repository.Facets.Facets);
            Assert.DoesNotContain(SymbolFacet.Handler, repository.Facets.Facets);
            Assert.DoesNotContain(SymbolFacet.Client, repository.Facets.Facets);
            Assert.DoesNotContain(SymbolFacet.Service, repository.Facets.Facets);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-16")]
    public async Task ExecuteAsync_AcmeOrders_TypePropertyFieldAndEventFacetsOmitArchitectureRoles()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

            var architectureFacets = new[]
            {
                SymbolFacet.Controller,
                SymbolFacet.Handler,
                SymbolFacet.Repository,
                SymbolFacet.Client,
                SymbolFacet.Service,
            };
            var structural = context.Accumulator.ToSnapshot().Facts.OfType<Symbol>()
                .Where(symbol => symbol.Signature.Value.Contains("kind=namedtype", StringComparison.Ordinal)
                    || symbol.Signature.Value.Contains("kind=property", StringComparison.Ordinal)
                    || symbol.Signature.Value.Contains("kind=field", StringComparison.Ordinal)
                    || symbol.Signature.Value.Contains("kind=event", StringComparison.Ordinal))
                .ToArray();
            Assert.NotEmpty(structural);
            Assert.All(
                structural,
                symbol => Assert.DoesNotContain(symbol.Facets.Facets, facet => architectureFacets.Contains(facet)));
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "RP-14")]
    [Trait("Requirement", "RP-15")]
    public async Task ExecuteAsync_AcmeOrders_PlaceOrderAsyncLocatorSliceParsesAsCompleteMember()
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
            var locator = Assert.NotNull(placeOrder.DeclarationLocator);

            var absolute = FixtureFile(locator.RelativePath);
            Assert.Equal(
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(absolute))),
                locator.Hash.Value);
            Assert.False(Path.IsPathRooted(locator.RelativePath));
            Assert.DoesNotContain('\\', locator.RelativePath);
            Assert.Contains("PlaceOrderAsync", File.ReadAllText(absolute), StringComparison.Ordinal);

            var slice = Slice(absolute, locator.Span);
            Assert.Contains("await eventBus.PublishAsync", slice, StringComparison.Ordinal);

            var member = SyntaxFactory.ParseMemberDeclaration(slice);
            Assert.NotNull(member);
            Assert.IsType<MethodDeclarationSyntax>(member);
            Assert.Equal("PlaceOrderAsync", ((MethodDeclarationSyntax)member).Identifier.ValueText);
            Assert.DoesNotContain(
                member.GetDiagnostics(),
                diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "RP-16")]
    public async Task Emit_PartialTypeInTwoDocuments_PublishesOrdinallyFirstLocatorFromBothTreeOrders()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-partial-locator-");
        try
        {
            var solutionPath = WritePartialTypeSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
                Assert.False(context.Accumulator.StructuralCorruption);
                Assert.NotNull(context.BoundSolution);

                var forward = SplitTypeLocator(context.Accumulator);
                Assert.Contains("alpha.cs", forward.RelativePath, StringComparison.Ordinal);
                Assert.DoesNotContain("zeta.cs", forward.RelativePath, StringComparison.Ordinal);

                var reversed = ReverseTreeOrder(context.BoundSolution.Compilations[0]);
                var replay = ReplayEmit(context, reversed);
                Assert.False(replay.StructuralCorruption);
                Assert.Equal(forward, SplitTypeLocator(replay));
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
    [Trait("Requirement", "RP-14")]
    public async Task Emit_PartialTypeMembers_LocatorUsesEachDeclarationsOwnDocument()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-partial-member-locator-");
        try
        {
            var solutionPath = WritePartialTypeSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

                var fromAlpha = Assert.Single(
                    context.Accumulator.ToSnapshot().Facts.OfType<Symbol>(),
                    symbol => symbol.Signature.Value.Contains("metadata=FromAlpha", StringComparison.Ordinal));
                var fromZeta = Assert.Single(
                    context.Accumulator.ToSnapshot().Facts.OfType<Symbol>(),
                    symbol => symbol.Signature.Value.Contains("metadata=FromZeta", StringComparison.Ordinal));

                Assert.Contains("alpha.cs", Assert.NotNull(fromAlpha.DeclarationLocator).RelativePath, StringComparison.Ordinal);
                Assert.Contains("zeta.cs", Assert.NotNull(fromZeta.DeclarationLocator).RelativePath, StringComparison.Ordinal);
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
    [Trait("Requirement", "RP-14")]
    public async Task ExecuteAsync_AcmeOrders_PlaceOrderAsyncLocatorDocumentIdIsTheDocumentFactId()
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
            var locator = Assert.NotNull(placeOrder.DeclarationLocator);
            var document = Assert.Single(
                context.Accumulator.ToSnapshot().Facts.OfType<Document>(),
                candidate => string.Equals(candidate.RelativePath, locator.RelativePath, StringComparison.Ordinal));

            Assert.Equal(document.Reference.Id.Value, locator.Document.Value);
            Assert.StartsWith("id1:document", locator.Document.Value, StringComparison.Ordinal);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "RP-16")]
    public async Task Emit_PartialTypeReversedTreeOrder_MemberLocatorsStayOnTheirOwnDocuments()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-partial-member-order-");
        try
        {
            var solutionPath = WritePartialTypeSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
                Assert.NotNull(context.BoundSolution);

                var reversed = ReverseTreeOrder(context.BoundSolution.Compilations[0]);
                var replay = ReplayEmit(context, reversed);
                var fromAlpha = Assert.Single(
                    replay.ToSnapshot().Facts.OfType<Symbol>(),
                    symbol => symbol.Signature.Value.Contains("metadata=FromAlpha", StringComparison.Ordinal));
                var fromZeta = Assert.Single(
                    replay.ToSnapshot().Facts.OfType<Symbol>(),
                    symbol => symbol.Signature.Value.Contains("metadata=FromZeta", StringComparison.Ordinal));

                Assert.Contains("alpha.cs", Assert.NotNull(fromAlpha.DeclarationLocator).RelativePath, StringComparison.Ordinal);
                Assert.Contains("zeta.cs", Assert.NotNull(fromZeta.DeclarationLocator).RelativePath, StringComparison.Ordinal);
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
    [Trait("Requirement", "RP-14")]
    public async Task Emit_DeclarationWithoutDocumentFact_YieldsNoLocator()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-no-document-locator-");
        try
        {
            var solutionPath = WriteDeclaredShapesSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
                Assert.NotNull(context.BoundSolution);

                var hiddenPath = Path.Combine(tree.FullName, "App", "obj", "Hidden.cs");
                Directory.CreateDirectory(Path.GetDirectoryName(hiddenPath)!);
                var hiddenSource = """
                    public class HiddenType
                    {
                        public void Hidden() {}
                    }
                    """;
                File.WriteAllText(hiddenPath, hiddenSource);
                var parseOptions = (CSharpParseOptions)context.BoundSolution.Compilations[0].SyntaxTrees.First().Options;
                var hiddenTree = CSharpSyntaxTree.ParseText(hiddenSource, parseOptions, hiddenPath);
                var compilation = context.BoundSolution.Compilations[0].AddSyntaxTrees(hiddenTree);
                var replay = ReplayEmit(context, compilation);

                var hidden = Assert.Single(
                    replay.ToSnapshot().Facts.OfType<Symbol>(),
                    symbol => symbol.Signature.Value.Contains("metadata=HiddenType", StringComparison.Ordinal)
                        && symbol.Signature.Value.Contains("kind=namedtype", StringComparison.Ordinal));
                Assert.Null(hidden.DeclarationLocator);

                var visible = Assert.Single(
                    replay.ToSnapshot().Facts.OfType<Symbol>(),
                    symbol => symbol.Signature.Value.Contains("metadata=Declared", StringComparison.Ordinal)
                        && symbol.Signature.Value.Contains("kind=namedtype", StringComparison.Ordinal));
                Assert.NotNull(visible.DeclarationLocator);
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

    private static async Task<Symbol[]> EmitAccessibilityShapesAsync()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-accessibility-");
        try
        {
            var solutionPath = WriteAccessibilityShapesSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                var result = await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
                Assert.False(result.AbortPublication, context.Detail);

                return context.Accumulator.ToSnapshot().Facts.OfType<Symbol>().ToArray();
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

    private static string WriteAccessibilityShapesSolution(string root)
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
            Path.Combine(projectDir, "Accessibility.cs"),
            """
            public class Reachable
            {
                public void PublicMethod() {}
                private void PrivateMethod() {}
                protected void ProtectedMethod() {}
                internal void InternalMethod() {}

                internal class InternalNested
                {
                    public void OnInternalNested() {}
                }

                private class PrivateNested
                {
                    public void OnPrivateNested() {}
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

    private static string WritePartialTypeSolution(string root)
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
            Path.Combine(projectDir, "zeta.cs"),
            """
            public partial class Split
            {
                public void FromZeta() {}
            }
            """);
        File.WriteAllText(
            Path.Combine(projectDir, "alpha.cs"),
            """
            public partial class Split
            {
                public void FromAlpha() {}
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

    private static DeclarationLocator SplitTypeLocator(SnapshotAccumulator accumulator)
    {
        var split = Assert.Single(
            accumulator.ToSnapshot().Facts.OfType<Symbol>(),
            symbol => symbol.Signature.Value.Contains("metadata=Split", StringComparison.Ordinal)
                && symbol.Signature.Value.Contains("kind=namedtype", StringComparison.Ordinal));
        return Assert.NotNull(split.DeclarationLocator);
    }

    private static SnapshotAccumulator ReplayEmit(PipelineContext context, Microsoft.CodeAnalysis.Compilation compilation)
    {
        var replay = new SnapshotAccumulator();
        foreach (var fact in context.Accumulator.ToSnapshot().Facts)
        {
            if (fact is Symbol)
            {
                continue;
            }

            replay.AddFact(fact);
        }

        using var bound = new BoundSolution([], [compilation]);
        SymbolFactEmitter.Emit(bound, replay, context.SolutionPath);
        return replay;
    }

    private static Microsoft.CodeAnalysis.Compilation ReverseTreeOrder(Microsoft.CodeAnalysis.Compilation compilation)
    {
        var trees = compilation.SyntaxTrees.ToArray();
        Assert.True(trees.Length >= 2, "Expected at least two syntax trees to reverse.");
        return compilation.RemoveSyntaxTrees(trees).AddSyntaxTrees(trees.Reverse());
    }

    private static string FixtureFile(string relativePath)
    {
        var absolute = Path.GetFullPath(Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.True(File.Exists(absolute), $"Expected fixture file at '{absolute}'.");
        return absolute;
    }

    private static string Slice(string absolutePath, SourceSpan span)
    {
        var text = SourceText.From(File.ReadAllText(absolutePath));
        var start = text.Lines[span.StartLine - 1].Start + span.StartColumn - 1;
        var end = text.Lines[span.EndLine - 1].Start + span.EndColumn - 1;
        return text.ToString(TextSpan.FromBounds(start, end));
    }
}
