using Csharp2Md.Core;

namespace Csharp2Md.Core.Tests.Surface;

public sealed class KnowledgeEngineFacadeTests
{
    [Fact]
    [Trait("Requirement", "PKG-01")]
    [Trait("Requirement", "PUB-03")]
    public void KnowledgeEngine_ExposesAnalyzeAsyncAndValidateWithApprovedShapes()
    {
        var analyze = typeof(KnowledgeEngine).GetMethod(nameof(KnowledgeEngine.AnalyzeAsync));
        var validate = typeof(KnowledgeEngine).GetMethod(nameof(KnowledgeEngine.Validate));

        Assert.NotNull(analyze);
        Assert.Equal(typeof(Task<AnalyzeResult>), analyze!.ReturnType);
        var analyzeParameters = analyze.GetParameters();
        Assert.Equal(2, analyzeParameters.Length);
        Assert.Equal(typeof(AnalyzeRequest), analyzeParameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), analyzeParameters[1].ParameterType);
        Assert.True(analyzeParameters[1].HasDefaultValue);

        Assert.NotNull(validate);
        Assert.Equal(typeof(PackageValidationResult), validate!.ReturnType);
        Assert.Equal(typeof(ValidateRequest), Assert.Single(validate.GetParameters()).ParameterType);
    }

    [Fact]
    [Trait("Requirement", "PKG-01")]
    public void PublicApi_DoesNotExposeInternalGraphsPassesShardsOrStaging()
    {
        var leaked = typeof(KnowledgeEngine).Assembly.GetExportedTypes()
            .Select(type => type.Name)
            .Where(name => name is "FactualGraph" or "PackagePlan" or "PlannedArtifact"
                or "SolutionAnalyzer" or "PackageBuilder" or "PackagePublication"
                or "ShardPacker" or "PackageStaging" or "CommittedPackage")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(leaked.Length == 0, "Public API leaked internal type(s): " + string.Join(", ", leaked));
    }

    [Fact]
    [Trait("Requirement", "PUB-04")]
    [Trait("Requirement", "PUB-08")]
    public async Task AnalyzeAsync_EmptySolutionPaths_ReturnsStructuredDiagnosticAndDoesNotStage()
    {
        using var output = TempOutputRoot.Create();
        var engine = new KnowledgeEngine();

        var result = await engine.AnalyzeAsync(
            new AnalyzeRequest(ImmutableArray<string>.Empty, output.DirectoryPath));

        Assert.False(result.Committed);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("invalid-request", diagnostic.Code);
        Assert.Equal("invocation", diagnostic.Stage);
        Assert.Equal("solution-paths-required", diagnostic.Cause);
        Assert.Null(diagnostic.Project);
        Assert.Null(diagnostic.Variant);
        Assert.Null(diagnostic.Family);
        Assert.Empty(Directory.GetFileSystemEntries(output.DirectoryPath));
    }

    [Fact]
    [Trait("Requirement", "PUB-04")]
    [Trait("Requirement", "PUB-08")]
    public async Task AnalyzeAsync_MissingOutputDirectory_ReturnsStructuredDiagnosticAndDoesNotAnalyze()
    {
        var engine = new KnowledgeEngine();
        var missingSolution = Path.Combine(Path.GetTempPath(), "csharp2md-missing", "Missing.sln");

        var result = await engine.AnalyzeAsync(
            new AnalyzeRequest(ImmutableArray.Create(missingSolution), "   "));

        Assert.False(result.Committed);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("invalid-request", diagnostic.Code);
        Assert.Equal("invocation", diagnostic.Stage);
        Assert.Equal("output-directory-required", diagnostic.Cause);
        Assert.False(Directory.Exists(Path.GetDirectoryName(missingSolution)!));
    }

    [Fact]
    [Trait("Requirement", "PUB-08")]
    public async Task AnalyzeAsync_WhitespaceSolutionPath_FailsBeforeAnalysisWithCause()
    {
        using var output = TempOutputRoot.Create();
        var engine = new KnowledgeEngine();

        var result = await engine.AnalyzeAsync(
            new AnalyzeRequest(ImmutableArray.Create("  "), output.DirectoryPath));

        Assert.False(result.Committed);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Code));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Stage));
        Assert.Equal("solution-paths-required", diagnostic.Cause);
        Assert.Empty(Directory.GetFileSystemEntries(output.DirectoryPath));
    }

    [Fact]
    [Trait("Requirement", "PUB-08")]
    public async Task AnalyzeAsync_CancelledToken_ThrowsOperationCanceledExceptionNotADiagnosticResult()
    {
        using var output = TempOutputRoot.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var engine = new KnowledgeEngine();
        var request = new AnalyzeRequest(
            ImmutableArray.Create(Path.Combine(output.DirectoryPath, "Acme.sln")),
            output.DirectoryPath);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.AnalyzeAsync(request, cts.Token));
        Assert.Empty(Directory.GetFileSystemEntries(output.DirectoryPath));
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    [Trait("Requirement", "PUB-08")]
    public void Validate_MissingPackageDirectory_ReturnsStructuredDiagnostic()
    {
        var engine = new KnowledgeEngine();

        var result = engine.Validate(new ValidateRequest(""));

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("invalid-request", diagnostic.Code);
        Assert.Equal("invocation", diagnostic.Stage);
        Assert.Equal("package-directory-required", diagnostic.Cause);
    }
}
