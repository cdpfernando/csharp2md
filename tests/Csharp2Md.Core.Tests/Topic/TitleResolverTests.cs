using Csharp2Md.Core.Topic;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Topic;

public sealed class TitleResolverTests
{
    // Tier 1 shape: OrderService.cs — file-scoped namespace, one type whose name equals the file
    // name. Also the fixture's only exercise of FileScopedNamespaceDeclarationSyntax in this suite.
    [Fact]
    public void Resolve_TypeNameMatchesFileName_ReturnsThatTypeAsTierOne()
    {
        const string source = """
            namespace Acme.Orders;

            public sealed class OrderService
            {
                public void PlaceOrder() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);

        var resolution = TitleResolver.Resolve(tree, "Acme.Orders/OrderService.cs", "Acme.Orders");

        Assert.Equal("OrderService", resolution.Title);
        Assert.NotNull(resolution.Type);
        Assert.Equal("OrderService", resolution.Type!.Identifier.ValueText);
        Assert.Null(resolution.Warning);
    }

    // Tier 2 shape: PaymentsGrpcClient.cs — a foreign-namespace stand-in (Grpc.Core.ClientBase)
    // declared before the project's own type (Acme.Orders.PaymentsClient), block namespaces, file
    // name matching neither. Also the fixture's only exercise of block NamespaceDeclarationSyntax.
    [Fact]
    public void Resolve_ForeignNamespaceTypeDeclaredFirst_ReturnsProjectNamespaceTypeAsTierTwo()
    {
        const string source = """
            namespace Grpc.Core
            {
                public abstract class ClientBase { }
            }

            namespace Acme.Orders
            {
                public sealed class PaymentsClient : Grpc.Core.ClientBase { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);

        var resolution = TitleResolver.Resolve(tree, "Acme.Orders/PaymentsGrpcClient.cs", "Acme.Orders");

        Assert.Equal("PaymentsClient", resolution.Title);
        Assert.Equal("PaymentsClient", resolution.Type!.Identifier.ValueText);
        Assert.Null(resolution.Warning);
    }

    // Tier 3 shape: Events.cs — two types sharing one namespace, file name matching neither. The
    // root namespace passed here deliberately does not match either type's namespace, so tier 2's
    // "starts with root namespace" condition genuinely fails and the source-order fallback (tier 3)
    // is what resolves the title — isolating tier 3 from tier 2's otherwise-identical outcome when
    // the root namespace equals the declared namespace (spec.md's worked example).
    [Fact]
    public void Resolve_SameNamespaceTypesNeitherMatchingFileName_ReturnsFirstInSourceOrderAsTierThree()
    {
        const string source = """
            namespace Acme.Shared.Contracts;

            public sealed record OrderPlaced(System.Guid OrderId);

            public sealed record PaymentProcessed(System.Guid PaymentId);
            """;
        var tree = CSharpSyntaxTree.ParseText(source);

        var resolution = TitleResolver.Resolve(tree, "Acme.Shared.Contracts/Events.cs", "Other.Project");

        Assert.Equal("OrderPlaced", resolution.Title);
        Assert.Equal("OrderPlaced", resolution.Type!.Identifier.ValueText);
        Assert.Null(resolution.Warning);
    }

    // Tier 4 shape: AssemblyInfo.cs — no type declared at all.
    [Fact]
    public void Resolve_NoTopLevelType_FallsBackToFileNameWithWarning()
    {
        const string source = """
            using System.Reflection;

            [assembly: AssemblyMetadata("Fixture", "csharp2md")]
            """;
        var tree = CSharpSyntaxTree.ParseText(source);

        var resolution = TitleResolver.Resolve(tree, "Acme.Orders/Properties/AssemblyInfo.cs", "Acme.Orders");

        Assert.Equal("AssemblyInfo", resolution.Title);
        Assert.Null(resolution.Type);
        Assert.False(string.IsNullOrEmpty(resolution.Warning));
        Assert.Contains("AssemblyInfo.cs", resolution.Warning, StringComparison.Ordinal);
    }
}
