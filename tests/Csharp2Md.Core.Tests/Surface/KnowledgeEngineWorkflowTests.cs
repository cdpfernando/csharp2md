using Csharp2Md.Core;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.Surface;

public sealed class KnowledgeEngineWorkflowTests : IClassFixture<KnowledgeEngineWorkflowFixture>
{
    private readonly KnowledgeEngineWorkflowFixture _fixture;

    public KnowledgeEngineWorkflowTests(KnowledgeEngineWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Requirement", "PUB-04")]
    public void AnalyzeAsync_SingleSolution_ReturnsCommittedOnlyAfterPublication()
    {
        Assert.True(_fixture.SingleResult.Committed, Diagnostics(_fixture.SingleResult));
        Assert.Empty(_fixture.SingleResult.Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "PUB-04")]
    public void AnalyzeAsync_CommittedResult_HasAnImmediatelyValidRootManifest()
    {
        Assert.True(File.Exists(Path.Combine(_fixture.SingleOutput, "manifest.json")));
        Assert.True(new KnowledgeEngine().Validate(new ValidateRequest(_fixture.SingleOutput)).Succeeded);
    }

    [Fact]
    [Trait("Requirement", "PKG-08")]
    public void AnalyzeAsync_DefaultPolicy_RecordsTestsAsExcluded()
    {
        using var reader = PackageReader.Open(_fixture.SingleOutput);
        Assert.False(reader.Manifest.IncludeTests);
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    public void AnalyzeAsync_MultipleSolutions_UsesOneAtomicCommit()
    {
        Assert.True(_fixture.MultiResult.Committed, Diagnostics(_fixture.MultiResult));
        Assert.Single(Directory.GetDirectories(Path.Combine(_fixture.MultiOutput, "generations")));
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    public void AnalyzeAsync_MultipleSolutions_GroupsBothSolutionsInOneManifest()
    {
        using var reader = PackageReader.Open(_fixture.MultiOutput);
        Assert.Equal(2, reader.Manifest.Solutions.Length);
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    public void AnalyzeAsync_MultipleSolutions_AssignsDistinctSolutionIdentities()
    {
        using var reader = PackageReader.Open(_fixture.MultiOutput);
        Assert.Equal(2, reader.Manifest.Solutions.Select(solution => solution.Id).Distinct().Count());
        Assert.Equal(2, reader.Manifest.Solutions.Select(solution => solution.LogicalRelativePath).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    public void AnalyzeAsync_MultipleSolutions_KeepsRootGraphsDisjoint()
    {
        using var reader = PackageReader.Open(_fixture.MultiOutput);
        var solutions = reader.Manifest.Solutions;
        Assert.All(solutions, solution => Assert.NotEqual(0, solution.Roots.Count));
        Assert.Empty(RootNames(reader, solutions[0]).Intersect(RootNames(reader, solutions[1]), StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "PKG-08")]
    public void AnalyzeAsync_ExplicitIncludeTestsPolicy_ReachesTheManifest()
    {
        using var reader = PackageReader.Open(_fixture.MultiOutput);
        Assert.True(reader.Manifest.IncludeTests);
    }

    [Fact]
    [Trait("Requirement", "PUB-04")]
    public void AnalyzeAsync_CommittedResult_HasFourNonFailingCertificationsPerSolution()
    {
        using var reader = PackageReader.Open(_fixture.MultiOutput);
        var certification = CanonicalJson.Read<PackageCertification>(reader.ReadArtifact("certification.json").AsSpan());

        Assert.Equal(2, certification.Solutions.Length);
        Assert.All(certification.Solutions, solution =>
        {
            Assert.Equal(4, solution.Journeys.Length);
            Assert.DoesNotContain(solution.Journeys, journey => journey.Status == JourneyCertificationStatus.Failed);
        });
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    public void Validate_CommittedPackage_UsesThePublicationInterpretation()
    {
        var result = new KnowledgeEngine().Validate(new ValidateRequest(_fixture.SingleOutput));

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics.Select(Format)));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    public void Validate_WhenSourceSolutionIsOffline_ReadsOnlyThePackage()
    {
        var source = _fixture.FirstSolutionDirectory;
        var offline = source + ".offline";
        Directory.Move(source, offline);
        try
        {
            var result = new KnowledgeEngine().Validate(new ValidateRequest(_fixture.SingleOutput));
            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics.Select(Format)));
        }
        finally
        {
            Directory.Move(offline, source);
        }
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    public void Validate_DoesNotMutateTheCommittedPackage()
    {
        var before = Snapshot(_fixture.SingleOutput);

        var result = new KnowledgeEngine().Validate(new ValidateRequest(_fixture.SingleOutput));

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics.Select(Format)));
        Assert.Equal(before, Snapshot(_fixture.SingleOutput));
    }

    [Fact]
    [Trait("Requirement", "PUB-08")]
    public async Task AnalyzeAsync_MissingSolution_ReturnsStructuredAnalysisFailureWithoutCommit()
    {
        using var output = TempOutputRoot.Create();
        var missing = Path.Combine(_fixture.Root, "missing", "Missing.slnx");

        var result = await new KnowledgeEngine().AnalyzeAsync(new AnalyzeRequest([missing], output.DirectoryPath));

        Assert.False(result.Committed);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("analysis-failed", diagnostic.Code);
        Assert.Equal("analysis", diagnostic.Stage);
        Assert.Equal("solution-not-found", diagnostic.Cause);
        Assert.False(File.Exists(Path.Combine(output.DirectoryPath, "manifest.json")));
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    [Trait("Requirement", "PUB-08")]
    public async Task AnalyzeAsync_DuplicateSolutionInput_ReturnsStructuredNonSuccess()
    {
        using var output = TempOutputRoot.Create();
        var solution = _fixture.FirstSolutionPath;

        var result = await new KnowledgeEngine().AnalyzeAsync(new AnalyzeRequest([solution, solution], output.DirectoryPath));

        Assert.False(result.Committed);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("analysis-failed", diagnostic.Code);
        Assert.Equal("analysis", diagnostic.Stage);
        Assert.False(File.Exists(Path.Combine(output.DirectoryPath, "manifest.json")));
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    [Trait("Requirement", "PUB-08")]
    public void Validate_MissingPackage_ReturnsStructuredCorruptionDiagnostic()
    {
        var missing = Path.Combine(_fixture.Root, "missing-package");

        var result = new KnowledgeEngine().Validate(new ValidateRequest(missing));

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("package-corruption", diagnostic.Code);
        Assert.Equal("validation", diagnostic.Stage);
        Assert.Equal("missing-artifact", diagnostic.Cause);
        Assert.Equal("manifest", diagnostic.Family);
        Assert.Equal("manifest.json", diagnostic.Artifact);
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    [Trait("Requirement", "PUB-08")]
    public void Validate_CorruptRootManifest_ReturnsStructuredNonSuccess()
    {
        using var package = TempOutputRoot.Create();
        File.WriteAllText(Path.Combine(package.DirectoryPath, "manifest.json"), "{not-json");

        var result = new KnowledgeEngine().Validate(new ValidateRequest(package.DirectoryPath));

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("package-corruption", diagnostic.Code);
        Assert.Equal("validation", diagnostic.Stage);
        Assert.Equal("invalid-package", diagnostic.Cause);
        Assert.Equal("manifest.json", diagnostic.Artifact);
    }

    private static IEnumerable<string> RootNames(PackageReader reader, SolutionManifestEntry solution) =>
        CanonicalJson.Read<RootsIndexData>(reader.ReadArtifact(solution.Roots.EntryPath).AsSpan()).Roots.Select(root => root.DisplayName);

    private static string Diagnostics(AnalyzeResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(Format));

    private static string Format(EngineDiagnostic diagnostic) =>
        $"{diagnostic.Code}:{diagnostic.Stage}:{diagnostic.Cause}";

    private static string[] Snapshot(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(root, path) + ":" + Convert.ToHexString(File.ReadAllBytes(path)))
            .ToArray();
}

public sealed class KnowledgeEngineWorkflowFixture : IAsyncLifetime
{
    public string Root { get; } = TempPath.Unique("csharp2md-engine-");
    public string FirstSolutionDirectory => Path.Combine(Root, "One");
    public string FirstSolutionPath => Path.Combine(FirstSolutionDirectory, "One.slnx");
    public string SecondSolutionPath => Path.Combine(Root, "Two", "Two.slnx");
    public string SingleOutput => Path.Combine(Root, "single-package");
    public string MultiOutput => Path.Combine(Root, "multi-package");
    public AnalyzeResult SingleResult { get; private set; } = null!;
    public AnalyzeResult MultiResult { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Root);
        CreateSolution("One");
        CreateSolution("Two");
        var engine = new KnowledgeEngine();
        SingleResult = await engine.AnalyzeAsync(new AnalyzeRequest([FirstSolutionPath], SingleOutput));
        MultiResult = await engine.AnalyzeAsync(new AnalyzeRequest(
            [FirstSolutionPath, SecondSolutionPath],
            MultiOutput,
            includeTests: true));
    }

    public Task DisposeAsync()
    {
        TempPath.TryDelete(Root);
        return Task.CompletedTask;
    }

    private void CreateSolution(string name)
    {
        var directory = Path.Combine(Root, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, $"{name}.slnx"),
            $"<Solution><Project Path=\"{name}.csproj\" /></Solution>");
        File.WriteAllText(
            Path.Combine(directory, $"{name}.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            $"Console.WriteLine(\"{name}\");");
    }
}
