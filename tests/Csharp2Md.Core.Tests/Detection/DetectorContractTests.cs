using Csharp2Md.Core;
using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Detection;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Detection;

public sealed class DetectorContractTests
{
    // AD-002/AD-004: detectors degrade with the renderer, so the context must accept a missing model.
    [Fact]
    public void DocumentDetectionContext_AcceptsANullSemanticModel()
    {
        var context = new DocumentDetectionContext(
            new ServiceName("Acme.Orders"),
            "Orders/OrderService.cs",
            CSharpSyntaxTree.ParseText("class C { }"),
            SemanticModel: null,
            DetectionTestContext.EmptyConfigIndex);

        Assert.Null(context.SemanticModel);
    }

    // AD-004: the two contracts are separate because the signals operate at different granularities.
    [Fact]
    public void TheTwoDetectorContracts_AreIndependentInterfaces()
    {
        Assert.False(typeof(IDocumentDependencyDetector).IsAssignableFrom(typeof(IProjectDependencyDetector)));
        Assert.False(typeof(IProjectDependencyDetector).IsAssignableFrom(typeof(IDocumentDependencyDetector)));
    }
}

internal static class DetectionTestContext
{
    public static ConfigIndex EmptyConfigIndex => new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static ConfigIndex ConfigIndexWith(params (string Name, string Value)[] entries) => new(
        entries.ToDictionary(e => e.Name, e => e.Value, StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
