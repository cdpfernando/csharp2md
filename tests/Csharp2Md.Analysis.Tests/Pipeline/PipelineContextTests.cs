using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineContextTests
{
    [Fact]
    [Trait("Requirement", "CDC-25")]
    [Trait("Requirement", "CDC-34")]
    public void Constructor_DefaultsAuthorizedRootAndConfigurationDocumentsToEmpty()
    {
        var context = new PipelineContext(new SwallowingSession(), "alpha.sln");

        Assert.Equal(string.Empty, context.AuthorizedRoot);
        Assert.Empty(context.ConfigurationDocuments);
        Assert.Empty(context.CSharpDocuments);
    }

    [Fact]
    [Trait("Requirement", "CDC-25")]
    [Trait("Requirement", "CDC-34")]
    public void AuthorizedRootAndConfigurationDocuments_RoundTripAssignedValues()
    {
        var context = new PipelineContext(new SwallowingSession(), "alpha.sln");
        var document = Document.Create(
            ProjectId.Create(SolutionId.Create(WorkspaceIdentity.Create("default"), "alpha.sln"), "App/App.csproj"),
            "App/appsettings.json");

        context.AuthorizedRoot = "authorized-root";
        context.ConfigurationDocuments = [document];

        Assert.Equal("authorized-root", context.AuthorizedRoot);
        Assert.Equal(document, Assert.Single(context.ConfigurationDocuments));
    }

    private sealed class SwallowingSession : IStoreSession
    {
        public void Stage(FactualSnapshot snapshot)
        {
        }

        public CommittedPublication Commit() => new("unused", []);

        public void Abort()
        {
        }
    }
}
