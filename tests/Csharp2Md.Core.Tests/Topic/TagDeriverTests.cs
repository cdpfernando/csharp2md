using Csharp2Md.Core.Topic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Tests.Topic;

public sealed class TagDeriverTests
{
    private static BaseTypeDeclarationSyntax FirstType(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().First();

    [Theory]
    [InlineData("class C { void M() { var b = WebApplication.CreateBuilder(new string[0]); } }")]
    [InlineData("class C { void M() { var b = WebHost.CreateDefaultBuilder(new string[0]); } }")]
    [InlineData("class C { void M(IWebHostBuilder builder) { } }")]
    public void Derive_BootstrappingPatterns_FiresBootstrapping(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.Contains("bootstrapping", tags);
    }

    [Theory]
    [InlineData("AddScoped")]
    [InlineData("AddSingleton")]
    [InlineData("AddTransient")]
    [InlineData("AddHostedService")]
    public void Derive_DependencyInjectionMethods_FiresDependencyInjection(string methodName)
    {
        var tree = CSharpSyntaxTree.ParseText(
            $"class C {{ void M(IServiceCollection services) {{ services.{methodName}<C>(); }} }}");

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.Contains("dependency-injection", tags);
    }

    [Fact]
    public void Derive_EventBusIdentifier_FiresEventDriven()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { IEventBus? Bus; }");

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.Contains("event-driven", tags);
    }

    // WIKI-06 fixture note: IEventBus declares Subscribe<TEvent>, not SubscribeAsync — the bare form
    // must fire, not just the Async-suffixed one.
    [Fact]
    public void Derive_BareSubscribeMember_FiresEventDriven()
    {
        var tree = CSharpSyntaxTree.ParseText(
            "interface IBus { void Subscribe<TEvent>(System.Action<TEvent> handler); }");

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.Contains("event-driven", tags);
    }

    [Theory]
    [InlineData("class OrderDbContext { DbContext Inner; }")]
    [InlineData("class OrderRepository { }")]
    public void Derive_PersistencePatterns_FiresPersistence(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.Contains("persistence", tags);
    }

    // Guards the exact-match design for DbContext/DbSet/IQueryable: OrdersController.cs in the
    // fixture references Data.OrderDbContext (a field type), which must NOT fire persistence even
    // though "OrderDbContext" ends with "DbContext" — only an exact identifier match does.
    [Fact]
    public void Derive_IdentifierEndingInDbContextButNotExactMatch_DoesNotFirePersistence()
    {
        var tree = CSharpSyntaxTree.ParseText("class OrdersController { OrderDbContext Data; }");

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.DoesNotContain("persistence", tags);
    }

    [Theory]
    [InlineData("class C { async System.Threading.Tasks.Task M() { } }")]
    [InlineData("class C { System.Threading.Tasks.Task M() { return null; } }")]
    [InlineData("class C { System.Threading.Tasks.Task M() { return Await(); } async System.Threading.Tasks.Task<int> Await() { await System.Threading.Tasks.Task.Delay(1); return 1; } }")]
    public void Derive_AsyncPatterns_FiresAsyncPatterns(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.Contains("async-patterns", tags);
    }

    [Fact]
    public void Derive_TitleTypeBaseEndsInController_FiresApiEndpoint()
    {
        var type = FirstType("class OrdersController : ControllerBase { }");
        var tree = CSharpSyntaxTree.ParseText("class OrdersController : ControllerBase { }");

        var tags = TagDeriver.Derive(tree, type);

        Assert.Contains("api-endpoint", tags);
    }

    [Fact]
    public void Derive_DocumentMatchingSeveralRules_ReturnsAllSortedWithNoDuplicates()
    {
        const string source = """
            class OrderService
            {
                async System.Threading.Tasks.Task PlaceOrderAsync(IEventBus bus)
                {
                    await bus.PublishAsync(1);
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.Equal(["async-patterns", "event-driven"], tags);
    }

    [Fact]
    public void Derive_DocumentMatchingNoRule_ReturnsEmptyListNotNull()
    {
        var tree = CSharpSyntaxTree.ParseText("public sealed record OrderPlaced(System.Guid OrderId);");

        var tags = TagDeriver.Derive(tree, titleType: null);

        Assert.NotNull(tags);
        Assert.Empty(tags);
    }
}
