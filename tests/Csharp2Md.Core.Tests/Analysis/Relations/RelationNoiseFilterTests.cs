using Csharp2Md.Core.Analysis.Relations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Analysis.Relations;

public sealed class RelationNoiseFilterTests
{
    private const string Source = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public sealed class ServiceCollection { }
        }

        namespace Acme.Payments
        {
            public sealed class PaymentsService { }
        }

        namespace Acme.Orders
        {
            public sealed class OrderService { }
        }
        """;

    private static readonly Compilation TheCompilation = CSharpCompilation.Create(
        "RelationNoiseFilterTests",
        [CSharpSyntaxTree.ParseText(Source)],
        TestCompilation.PlatformReferences,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static TheoryData<string> DenylistedSimpleNames => new()
    {
        "List",
        "Dictionary",
        "HashSet",
        "Queue",
        "Stack",
        "StringBuilder",
        "Guid",
        "Uri",
        "TimeSpan",
        "DateTime",
        "DateTimeOffset",
        "Task",
        "CancellationTokenSource",
        "InvalidOperationException",
        "ArgumentNullException",
    };

    [Theory]
    [MemberData(nameof(DenylistedSimpleNames))]
    public void IsLikelyFrameworkType_DenylistedOrExceptionSuffixedNames_ReturnsTrue(string simpleName) =>
        Assert.True(RelationNoiseFilter.IsLikelyFrameworkType(simpleName));

    [Theory]
    [InlineData("PaymentsService")]
    [InlineData("OrderRepository")]
    public void IsLikelyFrameworkType_ApplicationShapedNames_ReturnsFalse(string simpleName) =>
        Assert.False(RelationNoiseFilter.IsLikelyFrameworkType(simpleName));

    public static TheoryData<string> FrameworkNamespacedMetadataNames => new()
    {
        "System.Guid",
        "System.Uri",
        "System.Collections.Generic.List`1",
        "Microsoft.Extensions.DependencyInjection.ServiceCollection",
    };

    [Theory]
    [MemberData(nameof(FrameworkNamespacedMetadataNames))]
    public void IsFrameworkType_SystemOrMicrosoftExtensionsNamespacedType_ReturnsTrue(string metadataName)
    {
        var symbol = TheCompilation.GetTypeByMetadataName(metadataName);

        Assert.NotNull(symbol);
        Assert.True(RelationNoiseFilter.IsFrameworkType(symbol!));
    }

    public static TheoryData<string> ApplicationNamespacedMetadataNames => new()
    {
        "Acme.Payments.PaymentsService",
        "Acme.Orders.OrderService",
    };

    [Theory]
    [MemberData(nameof(ApplicationNamespacedMetadataNames))]
    public void IsFrameworkType_ApplicationNamespacedType_ReturnsFalse(string metadataName)
    {
        var symbol = TheCompilation.GetTypeByMetadataName(metadataName);

        Assert.NotNull(symbol);
        Assert.False(RelationNoiseFilter.IsFrameworkType(symbol!));
    }
}
