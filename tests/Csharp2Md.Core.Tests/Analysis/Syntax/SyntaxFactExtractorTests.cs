using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Syntax;

public sealed class SyntaxFactExtractorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");

    public static TheoryData<string, string, string[]> DeclarationCases => new()
    {
        { "namespace", "namespace A; class C { }", ["namespace", "class"] },
        { "overloads", "class C { void M(int value) { } void M(string value) { } }", ["class", "method", "method"] },
        { "generic type", "class Box<T> where T : class { T Value; }", ["class", "field"] },
        { "generic method", "class C { T Echo<T>(T value) => value; }", ["class", "method"] },
        { "record", "record Person(string Name);", ["record"] },
        { "record struct", "readonly record struct Money(decimal Amount);", ["record-struct"] },
        { "interface", "interface IRun { void Run(); }", ["interface", "method"] },
        { "override", "class B { public virtual void M() { } } class D : B { public override void M() { } }", ["class", "method", "class", "method"] },
        { "members", "class C { int F; string P { get; } event Action E; C() { } }", ["class", "field", "property", "event", "constructor"] },
        { "operator", "struct S { public static S operator +(S a, S b) => a; }", ["struct", "operator"] },
        { "delegate", "delegate void Work<T>(T value);", ["delegate"] },
        { "enum", "enum State { One, Two }", ["enum", "enum-member", "enum-member"] },
        { "conditional", "#if DEBUG\nclass DebugOnly { }\n#else\nclass ReleaseOnly { }\n#endif", ["class"] },
        { "error", "class Broken<T { void M( }", ["class", "method"] },
    };

    [Theory]
    [MemberData(nameof(DeclarationCases))]
    public void Extract_EmitsSpecDefinedSyntacticDeclarationKinds(string name, string source, string[] expectedKinds)
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, $"src/App/{name.Replace(' ', '-')}.cs", source);

        Assert.Equal(expectedKinds.Order(StringComparer.Ordinal), extraction.Symbols.Select(static symbol => symbol.SymbolKind).Order(StringComparer.Ordinal));
        Assert.All(extraction.Symbols, static symbol => Assert.Equal("Syntactic", symbol.Header.Resolution.ToString()));
        Assert.Equal(extraction.Symbols.Select(static symbol => symbol.SymbolId), extraction.Document.SymbolIds);
    }

    [Fact]
    public void Extract_OverloadsHaveDistinctStableIdsThatIgnoreBodies()
    {
        const string original = "class C { void M(int first) { Console.WriteLine(1); } void M(string text) { } }";
        const string changedBodies = "class C { void M(int first) { throw null!; } void M(string text) { Console.WriteLine(text); } }";

        var originalIds = Methods(original);
        var changedIds = Methods(changedBodies);

        Assert.Equal(2, originalIds.Length);
        Assert.Equal(2, originalIds.Distinct().Count());
        Assert.Equal(originalIds, changedIds);
    }

    [Fact]
    public void Extract_IdsSurviveAbsoluteRootRelocationAndUnrelatedPrecedingDeclaration()
    {
        const string declaration = "namespace A; class Stable { void Run(int value) { } }";
        const string withPreceding = "namespace A; class Earlier { } class Stable { void Run(int value) { } }";

        var first = SyntaxFactExtractor.Extract(ProjectId, "src/App/Stable.cs", declaration);
        var second = SyntaxFactExtractor.Extract(ProjectId, "src/App/Stable.cs", withPreceding);

        var firstStable = first.Symbols.Where(static symbol => symbol.SymbolId.Value.Contains("class%3AStable", StringComparison.Ordinal)).Select(static symbol => symbol.SymbolId.Value);
        var secondStable = second.Symbols.Where(static symbol => symbol.SymbolId.Value.Contains("class%3AStable", StringComparison.Ordinal)).Select(static symbol => symbol.SymbolId.Value);
        Assert.Equal(firstStable, secondStable);
        Assert.DoesNotContain(first.Symbols, static symbol => symbol.SymbolId.Value.Contains("span", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_BaseTypesAttributesReferencesAndXmlProseAreRetainedAsSyntacticEvidence()
    {
        const string source = """
            namespace A;
            /// <summary>Runs work.</summary>
            [Marker]
            class Worker : Base, IDisposable
            {
                string Run(int count) => count.ToString();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Worker.cs", source);
        var worker = Assert.Single(extraction.Symbols, static symbol => symbol.SymbolKind == "class");
        var method = Assert.Single(extraction.Symbols, static symbol => symbol.SymbolKind == "method");

        Assert.Equal(["Marker"], worker.Attributes.ToArray());
        // Sorted by (OwnerId, RelationKind, ObservedTarget): "implements" < "inherits" ordinally, so
        // T6's inherits/implements split reorders these relative to ObservedTarget's own alphabetical order.
        Assert.Equal(
            new[] { "Base", "IDisposable" }.Order(StringComparer.Ordinal),
            extraction.RelationCandidates.Select(static candidate => candidate.ObservedTarget).Order(StringComparer.Ordinal));
        Assert.Equal(["int", "string"], method.RelevantTypeReferences.ToArray());
        Assert.Contains("Runs work.", Assert.Single(extraction.XmlProse[worker.SymbolId]), StringComparison.Ordinal);
        Assert.All(extraction.RelationCandidates, static candidate =>
        {
            Assert.True(candidate.StartLine > 0 && candidate.StartColumn > 0);
            Assert.True(candidate.EndLine > 0 && candidate.EndColumn > 0);
            Assert.True(
                candidate.EndLine > candidate.StartLine ||
                (candidate.EndLine == candidate.StartLine && candidate.EndColumn >= candidate.StartColumn));
        });
        Assert.All(extraction.RelationCandidates, static candidate =>
            Assert.Equal(Csharp2Md.Core.Facts.Model.FactResolution.Syntactic, candidate.ShapeConfidence));
    }

    [Fact]
    public void Extract_BaseListCandidateSpan_PointsAtTheExactSourceLocationOfTheEntry()
    {
        const string source = "class Worker : Base { }";

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Worker.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates);
        Assert.Equal("Base", candidate.ObservedTarget);
        Assert.Equal(1, candidate.StartLine);
        Assert.Equal(1, candidate.EndLine);
        Assert.Equal(
            "Base",
            source.Substring(candidate.StartColumn - 1, candidate.EndColumn - candidate.StartColumn));
    }

    [Fact]
    public void Extract_InterfaceBaseListEntry_IsAlwaysImplementsSyntactic()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/I2.cs", "interface I2 : I1 { }");

        var candidate = Assert.Single(extraction.RelationCandidates);
        Assert.Equal("implements", candidate.RelationKind);
        Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
    }

    [Fact]
    public void Extract_StructBaseListEntry_IsAlwaysImplementsSyntactic()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/S.cs", "struct S : IDisposable { }");

        var candidate = Assert.Single(extraction.RelationCandidates);
        Assert.Equal("implements", candidate.RelationKind);
        Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
    }

    [Fact]
    public void Extract_ClassWithBaseClassAndInterface_FirstEntryInheritsRestImplements()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/D.cs", "class D : Base, IDisposable { }");

        var baseEntry = Assert.Single(extraction.RelationCandidates, static candidate => candidate.ObservedTarget == "Base");
        var interfaceEntry = Assert.Single(extraction.RelationCandidates, static candidate => candidate.ObservedTarget == "IDisposable");
        Assert.Equal("inherits", baseEntry.RelationKind);
        Assert.Equal(FactResolution.Syntactic, baseEntry.ShapeConfidence);
        Assert.Equal("implements", interfaceEntry.RelationKind);
        Assert.Equal(FactResolution.Syntactic, interfaceEntry.ShapeConfidence);
    }

    [Fact]
    public void Extract_QualifiedBaseClassNotIPrefixed_ClassifiesAsInherits()
    {
        const string source = "namespace Acme.Payments; sealed class PaymentsService : Payments.PaymentsBase { }";

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/PaymentsService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates);
        Assert.Equal("inherits", candidate.RelationKind);
        Assert.True(candidate.ObservedTarget is "PaymentsBase" or "Payments.PaymentsBase");
    }

    [Fact]
    public void Extract_SingleIPrefixedBaseListEntryOnClass_ClassifiesAsImplementsHeuristic()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", "class C : IRepository { }");

        var candidate = Assert.Single(extraction.RelationCandidates);
        Assert.Equal("implements", candidate.RelationKind);
        Assert.Equal(FactResolution.Heuristic, candidate.ShapeConfidence);
    }

    [Fact]
    public void Extract_BaseListEntryNamingTheDeclaringTypesOwnTypeParameter_DefaultsToImplementsUnresolved()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Weird.cs", "class Weird<T> : T { }");

        var candidate = Assert.Single(extraction.RelationCandidates);
        Assert.Equal("implements", candidate.RelationKind);
        Assert.Equal(FactResolution.Unresolved, candidate.ShapeConfidence);
    }

    [Fact]
    public void Extract_PublishAsyncWithObjectCreationArgumentAndNoExplicitTypeArgument_EmitsPublishes()
    {
        const string source = """
            class Bus
            {
                void Run(IEventBus eventBus) => eventBus.PublishAsync(new PaymentProcessed());
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Bus.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "publishes");
        Assert.Equal("PaymentProcessed", candidate.ObservedTarget);
        Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
    }

    [Fact]
    public void Extract_PublishWithExplicitTypeArgument_EmitsPublishesFromTypeArgumentRegardlessOfArgumentShape()
    {
        const string source = """
            class Bus
            {
                void Run(IEventBus bus) => bus.Publish<OrderPlaced>(new OrderPlaced());
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Bus.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "publishes");
        Assert.Equal("OrderPlaced", candidate.ObservedTarget);
        Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
    }

    [Fact]
    public void Extract_SubscribeWithNamedHandlerMethod_EmitsSubscribesAndHandlesOwnedByHandler()
    {
        const string source = """
            class Worker
            {
                Worker(IEventBus eventBus) => eventBus.Subscribe<OrderPlaced>(HandleOrderPlacedAsync);
                void HandleOrderPlacedAsync(OrderPlaced orderPlaced) { }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Worker.cs", source);
        var handlerSymbol = Assert.Single(extraction.Symbols, static symbol => symbol.SymbolKind == "method");

        var subscribes = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "subscribes");
        Assert.Equal("OrderPlaced", subscribes.ObservedTarget);
        Assert.Equal(FactResolution.Syntactic, subscribes.ShapeConfidence);

        var handles = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "handles");
        Assert.Equal("OrderPlaced", handles.ObservedTarget);
        Assert.Equal(handlerSymbol.SymbolId.ToFactId(), handles.OwnerId);
    }

    [Fact]
    public void Extract_SubscribeWithInlineLambdaHandler_EmitsSubscribesOnlyNoHandles()
    {
        const string source = """
            class Worker
            {
                Worker(IEventBus bus) => bus.Subscribe<OrderPlaced>(msg => { });
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Worker.cs", source);

        Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "subscribes");
        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind == "handles");
    }

    [Fact]
    public void Extract_PublishAsyncWithNoExplicitTypeArgumentAndNonObjectCreationArgument_EmitsNoCandidate()
    {
        const string source = """
            class Bus
            {
                void Run(IEventBus bus, PaymentProcessed existingVariable) => bus.PublishAsync(existingVariable);
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Bus.cs", source);

        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind is "publishes" or "subscribes" or "handles");
    }

    [Fact]
    public void Extract_MessagingCandidates_CarrySyntacticConfidenceAndARealEvidenceSpan()
    {
        const string source = """
            class Worker
            {
                Worker(IEventBus eventBus) => eventBus.Subscribe<OrderPlaced>(HandleOrderPlacedAsync);
                void HandleOrderPlacedAsync(OrderPlaced orderPlaced) { }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Worker.cs", source);
        var messagingCandidates = extraction.RelationCandidates
            .Where(static candidate => candidate.RelationKind is "subscribes" or "handles")
            .ToArray();

        Assert.NotEmpty(messagingCandidates);
        Assert.All(messagingCandidates, static candidate =>
        {
            Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
            Assert.True(candidate.StartLine > 0 && candidate.StartColumn > 0);
            Assert.True(candidate.EndLine > 0 && candidate.EndColumn > 0);
        });
    }

    [Fact]
    public void Extract_CreateClientWithStringLiteralArgument_EmitsHttpClient()
    {
        const string source = """
            class Service
            {
                void Run(IHttpClientFactory httpClientFactory) => httpClientFactory.CreateClient("PaymentService");
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Service.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "http-client");
        Assert.Equal("PaymentService", candidate.ObservedTarget);
        Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
    }

    [Fact]
    public void Extract_HttpVerbNamedInvocationOnClientFromCreateClient_EmitsHttpCallWithMethodAndRoute()
    {
        const string source = """
            class OrderService
            {
                async Task Run(IHttpClientFactory httpClientFactory)
                {
                    var paymentClient = httpClientFactory.CreateClient("PaymentService");
                    await paymentClient.PostAsJsonAsync("payments/authorize", new { });
                }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "http-call");
        Assert.Equal("http_method=POST|route=payments/authorize", candidate.ObservedTarget);
    }

    [Fact]
    public void Extract_SameNamedVerbMethodOnDeclaredNonHttpClientReceiver_EmitsNoHttpCall()
    {
        const string source = """
            class Service
            {
                void Run(MyCustomService svc) => svc.PostAsync("x");
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Service.cs", source);

        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind == "http-call");
    }

    [Fact]
    public void Extract_ErrorBearingDeclarationRemainsSyntacticAndMarked()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Broken.cs", "class Broken<T { void Run( }");

        Assert.Contains(extraction.Symbols, static symbol => symbol.ContainsErrorSymbol);
        Assert.All(extraction.Symbols, static symbol => Assert.Equal(Csharp2Md.Core.Facts.Model.FactResolution.Syntactic, symbol.Header.Resolution));
    }

    [Fact]
    public void Extract_AttributeArgumentsWithWhitespaceRemainStableAndCanonicallyFormatted()
    {
        const string source = """
            namespace Application.Tests.CommandHandlers.Customer;

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public class VerifyHomeAndMainAddressAreEqualsHandlerTest
            {
                public async Task Should_Returns_False_When_HomeAddressId_Is_Null_Or_Empty_Or_White_Spaces(string homeAddressId) { }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/VerifyHomeAndMainAddressAreEqualsHandlerTest.cs", source);

        Assert.Contains(extraction.Symbols, static symbol => symbol.SymbolKind == "class");
        Assert.Contains(extraction.Symbols, static symbol => symbol.SymbolKind == "method");
        Assert.All(extraction.Symbols, static symbol => Assert.DoesNotContain("\n", symbol.SymbolId.Value, StringComparison.Ordinal));
        Assert.All(extraction.Symbols, static symbol => Assert.DoesNotContain("\t", symbol.SymbolId.Value, StringComparison.Ordinal));
        Assert.All(extraction.Symbols, static symbol => Assert.DoesNotContain("  ", symbol.SymbolId.Value, StringComparison.Ordinal));
    }

    private static string[] Methods(string source) =>
        SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source).Symbols
            .Where(static symbol => symbol.SymbolKind == "method")
            .Select(static symbol => symbol.SymbolId.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
