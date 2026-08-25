using Csharp2Md.Analysis;

namespace Csharp2Md.Analysis.Tests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    [Trait("Requirement", "ENG-01")]
    public void AssemblyMarker_IdentifiesTheAnalysisAssembly()
    {
        Assert.Equal("Csharp2Md.Analysis", AssemblyMarker.AssemblyName);
        Assert.Equal(AssemblyMarker.AssemblyName, typeof(AssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    [Trait("Requirement", "ENG-01")]
    public void RepoRoot_ResolvesToTheDirectoryContainingTheSolutionFile() =>
        Assert.True(File.Exists(Path.Combine(AnalysisTestPaths.RepoRoot, "csharp2md.slnx")));
}
