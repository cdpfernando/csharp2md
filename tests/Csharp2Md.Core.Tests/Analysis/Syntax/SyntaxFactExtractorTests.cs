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
        // Scoped to inherits/implements: T9's "calls" pass also (correctly, per RELC-12) surfaces
        // count.ToString() from the method body, which is out of scope for this base-list assertion.
        Assert.Equal(
            new[] { "Base", "IDisposable" }.Order(StringComparer.Ordinal),
            extraction.RelationCandidates
                .Where(static candidate => candidate.RelationKind is "inherits" or "implements")
                .Select(static candidate => candidate.ObservedTarget)
                .Order(StringComparer.Ordinal));
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
    public void Extract_MemberAccessInvocationOnApplicationTypedReceiver_EmitsCallsWithReceiverDotMember()
    {
        const string source = """
            class OrderService
            {
                Task<string> Run(PaymentsClient paymentsClient) => paymentsClient.AuthorizePayment("1", 2m);
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        // RELR-05/RELR-21: target_text (ObservedTarget) is still exactly what it was before shape
        // capture existed, so no relation identity changes.
        Assert.Equal("paymentsClient.AuthorizePayment", candidate.ObservedTarget);
        Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
        Assert.Equal("paymentsClient", candidate.ReceiverText);
        Assert.Equal("PaymentsClient", candidate.ReceiverTypeText);
        Assert.Equal("AuthorizePayment", candidate.MemberName);
        Assert.Equal(2, candidate.ArgumentCount);
        Assert.Equal<string?>(["string", "decimal"], candidate.ArgumentTypes);
    }

    [Fact]
    public void Extract_CallsInvocationWithNoArguments_ReportsZeroArgumentCountAndNoArgumentTypes()
    {
        const string source = """
            class OrderService
            {
                void Run(PaymentsClient paymentsClient) => paymentsClient.Ping();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal(0, candidate.ArgumentCount);
        Assert.Empty(candidate.ArgumentTypes);
    }

    [Fact]
    public void Extract_CallsInvocationWithAnIdentifierArgument_ReportsNullForTheUnreadableArgumentType()
    {
        const string source = """
            class OrderService
            {
                void Run(PaymentsClient paymentsClient, decimal amount) => paymentsClient.Authorize(amount);
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal(1, candidate.ArgumentCount);
        Assert.Equal<string?>([null], candidate.ArgumentTypes);
    }

    [Fact]
    public void Extract_CallThroughAFieldBackedReceiver_ResolvesTheFieldsDeclaredType()
    {
        const string source = """
            class OrderService
            {
                PaymentsClient paymentsClient;
                void Run() => paymentsClient.Authorize();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal("PaymentsClient", candidate.ReceiverTypeText);
    }

    [Fact]
    public void Extract_CallThroughAPropertyBackedReceiver_ResolvesThePropertysDeclaredType()
    {
        const string source = """
            class OrderService
            {
                PaymentsClient PaymentsClient { get; }
                void Run() => PaymentsClient.Authorize();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal("PaymentsClient", candidate.ReceiverTypeText);
    }

    [Fact]
    public void Extract_CallThroughAReceiverWithNoMatchingDeclaration_LeavesReceiverTypeTextNull()
    {
        const string source = """
            class OrderService
            {
                void Run() => unknownReceiver.Authorize();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Null(candidate.ReceiverTypeText);
    }

    [Fact]
    public void Extract_CallThroughAConstructorParameterReceiver_ResolvesTheParametersDeclaredType()
    {
        // The design's worked example: public OrderService(PaymentClient paymentClient) makes
        // paymentClient resolve to PaymentClient.
        const string source = """
            class OrderService
            {
                public OrderService(PaymentClient paymentClient) => paymentClient.Authorize();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal("PaymentClient", candidate.ReceiverTypeText);
    }

    [Fact]
    public void Extract_CallThroughAPrimaryConstructorParameterReceiverOnAClass_ResolvesTheParametersDeclaredType()
    {
        const string source = """
            class OrderService(PaymentsClient paymentsClient)
            {
                void Run() => paymentsClient.Authorize();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal("PaymentsClient", candidate.ReceiverTypeText);
    }

    [Fact]
    public void Extract_CallThroughAPrimaryConstructorParameterReceiverOnARecord_ResolvesTheParametersDeclaredType()
    {
        const string source = """
            record OrderService(PaymentsClient paymentsClient)
            {
                void Run() => paymentsClient.Authorize();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal("PaymentsClient", candidate.ReceiverTypeText);
    }

    [Fact]
    public void Extract_CallThroughAPrimaryConstructorParameterShadowedByALocal_ResolvesTheLocalsDeclaredType()
    {
        // C# scoping: a local variable declared inside a method hides the outer parameter of the
        // same name, so the local's own (different) declared type is what resolves - not the
        // primary-constructor parameter's.
        const string source = """
            class OrderService(PaymentsClient paymentsClient)
            {
                void Run()
                {
                    LegacyPaymentsClient paymentsClient = GetLegacyClient();
                    paymentsClient.Authorize();
                }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal("LegacyPaymentsClient", candidate.ReceiverTypeText);
    }

    [Fact]
    public void Extract_CallThroughADeclarationPatternVariableReceiver_ResolvesThePatternsDeclaredType()
    {
        const string source = """
            class OrderService
            {
                void Run(object client)
                {
                    if (client is PaymentClient paymentClient)
                    {
                        paymentClient.Authorize();
                    }
                }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Equal("PaymentClient", candidate.ReceiverTypeText);
    }

    // A var-declared local's type is written nowhere in the syntax; this is a deliberate, permanent
    // limit of syntax-only receiver resolution, not a gap - pinned so a future change can't silently
    // regress it into a guess.
    [Fact]
    public void Extract_CallThroughAVarDeclaredLocalReceiver_LeavesReceiverTypeTextNullByDesign()
    {
        const string source = """
            class OrderService
            {
                void Run()
                {
                    var paymentsClient = CreatePaymentsClient();
                    paymentsClient.Authorize();
                }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderService.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
        Assert.Null(candidate.ReceiverTypeText);
    }

    [Fact]
    public void Extract_NewOfDenylistedFrameworkType_EmitsNoCreates()
    {
        const string source = """
            class C
            {
                void Run() { var list = new List<int>(); }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source);

        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind == "creates");
    }

    [Fact]
    public void Extract_NewOfApplicationType_EmitsCreates()
    {
        const string source = """
            class C
            {
                void Run() { var authorizer = new PaymentAuthorizer(); }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source);

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "creates");
        Assert.Equal("PaymentAuthorizer", candidate.ObservedTarget);
        Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
    }

    [Fact]
    public void Extract_InvocationAlreadyClassifiedAsPublishes_IsNotAlsoEmittedAsCalls()
    {
        const string source = """
            class Bus
            {
                void Run(IEventBus bus) => bus.PublishAsync(new PaymentProcessed());
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Bus.cs", source);

        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
    }

    [Fact]
    public void Extract_ObjectCreationConsumedAsPublishesTarget_IsNotDoubleEmittedAsCreates()
    {
        const string source = """
            class Bus
            {
                void Run(IEventBus bus) => bus.PublishAsync(new PaymentProcessed());
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Bus.cs", source);

        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind == "creates");
        var publishes = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "publishes");
        Assert.Equal("PaymentProcessed", publishes.ObservedTarget);
    }

    [Fact]
    public void Extract_InvocationOnDenylistedFrameworkTypeReceiver_EmitsNoCalls()
    {
        const string source = """
            class C
            {
                void Run() => Guid.NewGuid();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source);

        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind == "calls");
    }

    [Fact]
    public void Extract_PropertyTypedAsApplicationType_EmitsReferences()
    {
        const string source = """
            class Service
            {
                PaymentAuthorizer Authorizer { get; set; }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Service.cs", source);
        var property = Assert.Single(extraction.Symbols, static symbol => symbol.SymbolKind == "property");

        var candidate = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "references");
        Assert.Equal("PaymentAuthorizer", candidate.ObservedTarget);
        Assert.Equal(FactResolution.Syntactic, candidate.ShapeConfidence);
        Assert.Equal(property.SymbolId.ToFactId(), candidate.OwnerId);
        Assert.True(candidate.StartLine > 0 && candidate.StartColumn > 0);
    }

    [Fact]
    public void Extract_ParameterTypedCancellationTokenOrStringOrPrimitive_EmitsNoReferences()
    {
        const string source = """
            class Service
            {
                void Run(CancellationToken cancellationToken, string name) { }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Service.cs", source);

        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind == "references");
    }

    [Fact]
    public void Extract_TypeAlreadySurfacedViaCreatesOnSameMember_IsNotAlsoEmittedAsReferences()
    {
        const string source = """
            class Service
            {
                PaymentAuthorizer Build() => new PaymentAuthorizer();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Service.cs", source);

        Assert.DoesNotContain(extraction.RelationCandidates, static candidate => candidate.RelationKind == "references");
        var creates = Assert.Single(extraction.RelationCandidates, static candidate => candidate.RelationKind == "creates");
        Assert.Equal("PaymentAuthorizer", creates.ObservedTarget);
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

    [Fact]
    public void Extract_EveryDeclaration_HasNameAndSignaturePopulated()
    {
        const string source = """
            namespace A;
            class Outer
            {
                class Inner { void Run() { } }
                int Field;
                string Property { get; }
                event Action Changed;
                Outer() { }
            }
            enum State { One }
            delegate void Work(int value);
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Outer.cs", source);

        Assert.NotEmpty(extraction.Symbols);
        Assert.All(extraction.Symbols, static symbol =>
        {
            Assert.False(string.IsNullOrWhiteSpace(symbol.Name));
            Assert.False(string.IsNullOrWhiteSpace(symbol.Signature));
            Assert.False(string.IsNullOrWhiteSpace(symbol.FullyQualifiedName));
        });
    }

    [Fact]
    public void Extract_NestedTypeMember_ReportsOuterNamespaceAndTheNestedTypesOwnQualifiedName()
    {
        const string source = """
            namespace Acme.Payments;
            class Outer
            {
                class Inner
                {
                    void Run() { }
                }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Outer.cs", source);
        var run = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "Run");
        var inner = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "Inner");

        Assert.Equal("Acme.Payments", run.Namespace);
        Assert.Equal("global::Acme.Payments.Outer.Inner", run.ContainingType);
        Assert.Equal("global::Acme.Payments.Outer.Inner.Run", run.FullyQualifiedName);
        Assert.Equal("global::Acme.Payments.Outer.Inner", inner.FullyQualifiedName);
        Assert.Equal("global::Acme.Payments.Outer", inner.ContainingType);
    }

    [Theory]
    [InlineData("class Box<T> { }", "Box", 1)]
    [InlineData("class Pair<TKey, TValue> { }", "Pair", 2)]
    [InlineData("class Plain { }", "Plain", 0)]
    [InlineData("class C { T Echo<T>(T value) => value; }", "Echo", 1)]
    [InlineData("class C { void Run() { } }", "Run", 0)]
    [InlineData("delegate void Work<T>(T value);", "Work", 1)]
    public void Extract_GenericTypeOrMethod_ReportsItsDeclaredTypeParameterCountAsArity(
        string source,
        string name,
        int expectedArity)
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Arity.cs", source);

        var symbol = Assert.Single(extraction.Symbols, symbol => symbol.Name == name);
        Assert.Equal(expectedArity, symbol.Arity);
    }

    [Fact]
    public void Extract_MethodParameters_AreNormalizedSoKeywordAndQualifiedSpellingsMatch()
    {
        const string source = """
            class C
            {
                void Keyword(string name, int count) { }
                void Qualified(System.String name, System.Int32 count) { }
                void None() { }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source);
        var keyword = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "Keyword");
        var qualified = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "Qualified");
        var none = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "None");

        Assert.Equal(["global::System.String", "global::System.Int32"], keyword.ParameterTypes.ToArray());
        Assert.Equal(keyword.ParameterTypes.ToArray(), qualified.ParameterTypes.ToArray());
        Assert.Empty(none.ParameterTypes);
    }

    [Fact]
    public void Extract_RawSignatureKeepsTheOriginalSpellingTheNormalizedFieldsCollapse()
    {
        const string source = "class C { void Run(string name) { } }";

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source);
        var run = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "Run");

        Assert.Contains("string", run.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("System.String", run.Signature, StringComparison.Ordinal);
        Assert.Equal(["global::System.String"], run.ParameterTypes.ToArray());
    }

    // DAD-15: a field initializer's literal is part of the signature and the id derived from it - both
    // are facts, so a credential-shaped literal must never survive into either.
    [Fact]
    public void Extract_FieldInitializerCarryingACredential_RedactsTheLiteralInTheSignatureAndId()
    {
        const string source = """
            class C
            {
                private const string ConnectionString =
                    "Server=db;Database=Orders;User Id=app;Password=hunter2;";
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source);
        var field = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "ConnectionString");

        Assert.DoesNotContain("hunter2", field.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", field.SymbolId.Value, StringComparison.Ordinal);
        Assert.Contains("<redacted>", field.Signature, StringComparison.Ordinal);
        Assert.Contains("private const string ConnectionString", field.Signature, StringComparison.Ordinal);
    }

    // DAD-15's redaction is narrow: an ordinary literal, including one that merely mentions "password"
    // as a plain word rather than assigning one, is untouched.
    [Theory]
    [InlineData("""class C { private const string Greeting = "hello password"; }""", "hello password")]
    [InlineData("""class C { private const string Query = "SET Password = @password"; }""", "SET Password = @password")]
    [InlineData("""class C { private const int MaxRetries = 3; }""", "3")]
    public void Extract_OrdinaryOrParameterizedLiteral_IsNotRedacted(string source, string expectedText)
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source);
        var field = Assert.Single(extraction.Symbols, symbol => symbol.SymbolKind == "field");

        Assert.Contains(expectedText, field.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("<redacted>", field.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_MemberInsideAType_ReportsThatTypesOwnSymbolFactIdAsContainingSymbolId()
    {
        const string source = """
            namespace A;
            class Owner
            {
                void Run() { }
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Owner.cs", source);
        var owner = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "Owner");
        var run = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "Run");
        var @namespace = Assert.Single(extraction.Symbols, static symbol => symbol.SymbolKind == "namespace");

        Assert.Equal(owner.SymbolId, run.ContainingSymbolId);
        Assert.Equal(@namespace.SymbolId, owner.ContainingSymbolId);
    }

    [Fact]
    public void Extract_TopLevelDeclarationWithNoEnclosingDeclaration_HasNullContainingSymbolIdAndNamespace()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", "class C { }");

        var symbol = Assert.Single(extraction.Symbols);
        Assert.Null(symbol.ContainingSymbolId);
        Assert.Null(symbol.Namespace);
        Assert.Null(symbol.ContainingType);
        Assert.Equal("global::C", symbol.FullyQualifiedName);
    }

    [Fact]
    public void Extract_SameSimpleNameInTwoNamespaces_StaysDistinctByQualifiedNameAndIdentity()
    {
        const string source = """
            namespace Company.Legacy { class PaymentService { } }
            namespace Company.Payments { class PaymentService { } }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/PaymentService.cs", source);
        var services = extraction.Symbols
            .Where(static symbol => symbol.Name == "PaymentService")
            .ToArray();

        Assert.Equal(2, services.Length);
        Assert.Equal(2, services.Select(static symbol => symbol.SymbolId).Distinct().Count());
        Assert.Equal(
            ["global::Company.Legacy.PaymentService", "global::Company.Payments.PaymentService"],
            services.Select(static symbol => symbol.FullyQualifiedName).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["Company.Legacy", "Company.Payments"],
            services.Select(static symbol => symbol.Namespace).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Extract_PopulatesIdentityFieldsWithoutASemanticModelAndNeverClaimsExactResolution()
    {
        const string source = """
            namespace Acme.Payments;
            class PaymentsService
            {
                string AuthorizePayment(string orderId, decimal amount) => orderId;
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/PaymentsService.cs", source);
        var method = Assert.Single(extraction.Symbols, static symbol => symbol.Name == "AuthorizePayment");

        Assert.Equal("Acme.Payments", method.Namespace);
        Assert.Equal("global::Acme.Payments.PaymentsService", method.ContainingType);
        Assert.Equal(["global::System.String", "global::System.Decimal"], method.ParameterTypes.ToArray());
        Assert.All(extraction.Symbols, static symbol =>
            Assert.Equal(FactResolution.Syntactic, symbol.Header.Resolution));
    }

    private static string[] Methods(string source) =>
        SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source).Symbols
            .Where(static symbol => symbol.SymbolKind == "method")
            .Select(static symbol => symbol.SymbolId.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
