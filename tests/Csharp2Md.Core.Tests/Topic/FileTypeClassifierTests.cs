using Csharp2Md.Core.Topic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Tests.Topic;

/// <summary>
/// Every test here parses source with <see cref="CSharpSyntaxTree.ParseText(string, Microsoft.CodeAnalysis.CSharp.CSharpParseOptions?, string, System.Text.Encoding?, System.Threading.CancellationToken)"/>
/// only — no test in this file constructs a <c>CSharpCompilation</c> or a <c>SemanticModel</c>,
/// which is what proves classification never consults one (WIKI-13).
/// </summary>
public sealed class FileTypeClassifierTests
{
    private static BaseTypeDeclarationSyntax ParseFirstType(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().First();

    [Fact]
    public void Classify_IndexDocument_ReturnsIndex()
    {
        var result = FileTypeClassifier.Classify(
            isIndexDocument: true, "Acme.Orders/index.md", titleType: null, out var warnings);

        Assert.Equal(FileType.Index, result);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Classify_ProgramCs_ReturnsConfiguration()
    {
        var type = ParseFirstType("public static class Program { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/Program.cs", type, out _);

        Assert.Equal(FileType.Configuration, result);
    }

    [Fact]
    public void Classify_StartupCs_ReturnsConfiguration()
    {
        var type = ParseFirstType("public static class Startup { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/Startup.cs", type, out _);

        Assert.Equal(FileType.Configuration, result);
    }

    [Fact]
    public void Classify_EnumDeclaration_ReturnsEnum()
    {
        var type = ParseFirstType("public enum OrderStatus { Pending }");

        var result = FileTypeClassifier.Classify(false, "Acme.Shared.Contracts/OrderStatus.cs", type, out _);

        Assert.Equal(FileType.Enum, result);
    }

    [Fact]
    public void Classify_InterfaceDeclaration_ReturnsInterface()
    {
        var type = ParseFirstType("public interface IEventBus { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Shared.Contracts/IEventBus.cs", type, out _);

        Assert.Equal(FileType.Interface, result);
    }

    [Fact]
    public void Classify_BaseTypeEndsInController_ReturnsController()
    {
        var type = ParseFirstType("public class OrdersController : ControllerBase { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/Api/OrdersController.cs", type, out _);

        Assert.Equal(FileType.Controller, result);
    }

    [Fact]
    public void Classify_ImplementsIIntegrationEventHandler_ReturnsHandler()
    {
        var type = ParseFirstType(
            "public class OrderPlacedEventHandler : IIntegrationEventHandler<OrderPlaced> { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/Events/OrderPlacedEventHandler.cs", type, out _);

        Assert.Equal(FileType.Handler, result);
    }

    [Fact]
    public void Classify_InheritsDbContext_ReturnsDataAccess()
    {
        var type = ParseFirstType("public class OrderDbContext : DbContext { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/Data/OrderDbContext.cs", type, out _);

        Assert.Equal(FileType.DataAccess, result);
    }

    // No interface, no base type at all — proves the own-name fallback the fixture's OrderService
    // and PaymentsService both depend on (PaymentsService's real base name doesn't end in Service).
    [Fact]
    public void Classify_OwnNameEndsInServiceWithNoBaseList_ReturnsService()
    {
        var type = ParseFirstType("public sealed class OrderService { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/OrderService.cs", type, out _);

        Assert.Equal(FileType.Service, result);
    }

    [Fact]
    public void Classify_ImplementsIOperationFilter_ReturnsFilter()
    {
        var type = ParseFirstType("public class SwaggerOperationDefaultsFilter : IOperationFilter { }");

        var result = FileTypeClassifier.Classify(
            false, "Acme.Orders/Api/SwaggerOperationDefaultsFilter.cs", type, out _);

        Assert.Equal(FileType.Filter, result);
    }

    [Fact]
    public void Classify_StaticClassWithExtensionMethod_ReturnsExtension()
    {
        const string source = """
            public static class ServiceCollectionExtensions
            {
                public static IServiceCollection AddScoped<TService>(this IServiceCollection services) => services;
            }
            """;
        var type = ParseFirstType(source);

        var result = FileTypeClassifier.Classify(
            false, "Acme.Orders/Hosting/ServiceCollectionExtensions.cs", type, out _);

        Assert.Equal(FileType.Extension, result);
    }

    [Fact]
    public void Classify_NoRuleMatches_ReturnsClass()
    {
        var type = ParseFirstType("public sealed class PaymentsClient : Grpc.Core.ClientBase { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/PaymentsGrpcClient.cs", type, out _);

        Assert.Equal(FileType.Class, result);
    }

    [Fact]
    public void Classify_NoTitleType_ReturnsClass()
    {
        var result = FileTypeClassifier.Classify(
            false, "Acme.Orders/Properties/AssemblyInfo.cs", titleType: null, out var warnings);

        Assert.Equal(FileType.Class, result);
        Assert.Empty(warnings);
    }

    // Declaration-kind rules (position 4, interface) precede name/base-type rules (position 5,
    // controller) — an interface named *Controller must still classify as interface, not controller.
    [Fact]
    public void Classify_InterfaceNamedLikeAController_DeclarationKindRuleWinsOverNameRule()
    {
        var type = ParseFirstType("public interface IFooController { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/IFooController.cs", type, out var warnings);

        Assert.Equal(FileType.Interface, result);
        Assert.Single(warnings);
        Assert.Contains("interface", warnings[0], StringComparison.Ordinal);
        Assert.Contains("controller", warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_TypeMatchingTwoRules_ReturnsFirstInTableOrderWithWarningNamingBoth()
    {
        var type = ParseFirstType("public class FooController : IOperationFilter { }");

        var result = FileTypeClassifier.Classify(false, "Acme.Orders/FooController.cs", type, out var warnings);

        Assert.Equal(FileType.Controller, result);
        Assert.Single(warnings);
        Assert.Contains("Acme.Orders/FooController.cs", warnings[0], StringComparison.Ordinal);
        Assert.Contains("controller", warnings[0], StringComparison.Ordinal);
        Assert.Contains("filter", warnings[0], StringComparison.Ordinal);
    }
}
