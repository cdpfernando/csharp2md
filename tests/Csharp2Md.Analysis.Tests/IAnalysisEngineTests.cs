using Csharp2Md.Analysis;

namespace Csharp2Md.Analysis.Tests;

public sealed class IAnalysisEngineTests
{
    [Fact]
    [Trait("Requirement", "ENG-10")]
    public void AnalysisAssembly_ExposesExactlyOnePublicAnalysisInterface_NamedIAnalysisEngine()
    {
        var analysisInterfaces = typeof(AssemblyMarker).Assembly
            .GetExportedTypes()
            .Where(type => type.IsInterface)
            .Where(type => type.GetMethods().Any(method => method.Name == "AnalyzeAsync"))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(
            analysisInterfaces.Length == 1 && analysisInterfaces[0] == "Csharp2Md.Analysis.IAnalysisEngine",
            $"Expected exactly one public analysis interface named IAnalysisEngine, but found: {FormatNames(analysisInterfaces)}.");
    }

    [Fact]
    [Trait("Requirement", "ENG-17")]
    public void AnalyzeAsync_AcceptsAnalysisRequestAndCancellationToken()
    {
        var engine = typeof(AssemblyMarker).Assembly
            .GetExportedTypes()
            .Single(type => type.IsInterface && type.Name == "IAnalysisEngine");

        var method = engine.GetMethod("AnalyzeAsync");
        Assert.True(method is not null, "IAnalysisEngine must declare AnalyzeAsync.");

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(AnalysisRequest), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);

        Assert.True(method.ReturnType.IsGenericType);
        Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(AnalysisResult), method.ReturnType.GetGenericArguments()[0]);
    }

    private static string FormatNames(IReadOnlyList<string?> names) =>
        names.Count == 0 ? "<none>" : string.Join(", ", names);
}
