namespace Csharp2Md.Analysis.Tests;

public sealed class AnalysisRequestTests
{
    [Fact]
    [Trait("Requirement", "ENG-29")]
    public void Create_SinglePath_IsAcceptedAsSupplied()
    {
        ImmutableArray<string> paths = ["alpha.sln"];

        var request = AnalysisRequest.Create(paths);

        Assert.Equal(paths, request.SolutionPaths);
    }

    [Fact]
    [Trait("Requirement", "ENG-29")]
    public void Create_TwoDistinctPaths_AreAcceptedAsSupplied()
    {
        ImmutableArray<string> paths = [@".\a.sln", @".\b.sln"];

        var request = AnalysisRequest.Create(paths);

        Assert.Equal(paths, request.SolutionPaths);
    }

    [Fact]
    [Trait("Requirement", "ENG-30")]
    public void Create_EmptyArray_IsRejectedNamingTheMissingInput()
    {
        var exception = Assert.Throws<ArgumentException>(() => AnalysisRequest.Create([]));

        Assert.Equal("solutionPaths", exception.ParamName);
        Assert.Contains("one or more solutionPaths", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-30")]
    public void Create_DefaultArray_IsRejectedNamingTheMissingInput()
    {
        var exception = Assert.Throws<ArgumentException>(() => AnalysisRequest.Create(default));

        Assert.Equal("solutionPaths", exception.ParamName);
        Assert.Contains("one or more solutionPaths", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-31")]
    public void Create_RelativeAndAbsoluteFormOfTheSamePath_IsRejectedNamingTheDuplicate()
    {
        var relative = @".\a.sln";
        var absolute = Path.GetFullPath(relative);

        var exception = Assert.Throws<ArgumentException>(
            () => AnalysisRequest.Create([relative, absolute]));

        Assert.Equal("solutionPaths", exception.ParamName);
        Assert.Contains(absolute, exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
