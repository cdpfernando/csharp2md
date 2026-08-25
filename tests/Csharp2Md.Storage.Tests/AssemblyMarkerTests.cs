using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    [Trait("Requirement", "ENG-01")]
    public void AssemblyMarker_IdentifiesTheStorageAssembly()
    {
        Assert.Equal("Csharp2Md.Storage", AssemblyMarker.AssemblyName);
        Assert.Equal(AssemblyMarker.AssemblyName, typeof(AssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    [Trait("Requirement", "ENG-01")]
    public void RepoRoot_ResolvesToTheDirectoryContainingTheSolutionFile() =>
        Assert.True(File.Exists(Path.Combine(StorageTestPaths.RepoRoot, "csharp2md.slnx")));
}
