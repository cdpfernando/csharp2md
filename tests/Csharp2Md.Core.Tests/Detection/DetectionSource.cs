using Csharp2Md.Core;
using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Detection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Detection;

internal static class DetectionSource
{
    public static DocumentDetectionContext DocumentContext(
        string source,
        ConfigIndex? index = null,
        bool withSemantics = false,
        string documentPath = "Orders/Gateway.cs",
        string serviceName = "Acme.Orders")
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        SemanticModel? model = withSemantics
            ? CSharpCompilation.Create("Detection", [tree], TestCompilation.PlatformReferences).GetSemanticModel(tree)
            : null;

        return new DocumentDetectionContext(
            new ServiceName(serviceName),
            documentPath,
            tree,
            model,
            index ?? DetectionTestContext.EmptyConfigIndex);
    }
}
