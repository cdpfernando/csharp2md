using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Tests;

internal static class TestCompilation
{
    /// <summary>
    /// Every assembly the test host already trusts, so test sources can reference BCL types
    /// (<c>HttpClient</c>, <c>IDisposable</c>) and get a genuinely resolved semantic model.
    /// </summary>
    public static IReadOnlyList<MetadataReference> PlatformReferences { get; } =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToList();
}
