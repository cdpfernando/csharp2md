using Csharp2Md.Core.Loading;
using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Tests.Loading;

public sealed class SolutionLoaderClassificationTests
{
    [Fact]
    public async Task ClassifyAsync_ProjectWithUnsupportedLanguage_IsClassifiedUnsupportedForCompilation()
    {
        using var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            name: "NoCompilationFactory",
            assemblyName: "NoCompilationFactory",
            language: "Csharp2Md.Test.UnregisteredLanguage");

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        var project = solution.GetProject(projectInfo.Id)!;

        var result = await SolutionLoader.ClassifyAsync(project, [], CancellationToken.None);

        Assert.Equal(ProjectLoadStatus.UnsupportedForCompilation, result.Status);
    }
}
