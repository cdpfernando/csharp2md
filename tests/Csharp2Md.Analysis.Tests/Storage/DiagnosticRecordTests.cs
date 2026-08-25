using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class DiagnosticRecordTests
{
    [Fact]
    [Trait("Requirement", "ROSE-56")]
    public void Constructed_WithCodeMessageAndNullIdentity_ExposesTheThreeFields()
    {
        var record = new DiagnosticRecord("missing-project", "Acme.DoesNotExist was listed and is absent.", identityOrKey: null);

        Assert.Equal("missing-project", record.Code);
        Assert.Equal("Acme.DoesNotExist was listed and is absent.", record.Message);
        Assert.Null(record.IdentityOrKey);
    }

    [Fact]
    [Trait("Requirement", "ROSE-56")]
    public void Constructed_WithRelativePath_ExposesIdentityOrKey()
    {
        var record = new DiagnosticRecord(
            "unsupported-document",
            "Non-C# document inventoried without a C# extractor.",
            "src/Acme.Orders/appsettings.json");

        Assert.Equal("src/Acme.Orders/appsettings.json", record.IdentityOrKey);
        Assert.False(Path.IsPathRooted(record.IdentityOrKey));
    }

    [Fact]
    [Trait("Requirement", "ROSE-56")]
    public void Constructed_WithFactId_ExposesIdentityOrKey()
    {
        const string factId = "id1:project;workspace=default;solution=Acme.Orders.slnx;path=Acme.Broken/Acme.Broken.csproj";

        var record = new DiagnosticRecord("unresolvable-sdk", "The project SDK could not be resolved.", factId);

        Assert.Equal(factId, record.IdentityOrKey);
    }

    [Theory]
    [Trait("Requirement", "ROSE-56")]
    [InlineData(@"C:\src\Acme.sln")]
    [InlineData(@"D:\workspace\secret.env")]
    public void Constructed_WithDrivePrefixedIdentity_ThrowsArgumentException(string identityOrKey)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new DiagnosticRecord("symlink-escape", "A symlink escaped the authorized root.", identityOrKey));

        Assert.Equal("identityOrKey", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "ROSE-56")]
    public void Constructed_WithRootedIdentity_ThrowsArgumentException()
    {
        var rooted = Path.GetFullPath("Acme.Orders.slnx");
        Assert.True(Path.IsPathRooted(rooted), "The fixture path must be rooted so the rejection is the spec rule, not the test setup.");

        var exception = Assert.Throws<ArgumentException>(
            () => new DiagnosticRecord("msbuild-open-failed", "MSBuildWorkspace could not open the solution.", rooted));

        Assert.Equal("identityOrKey", exception.ParamName);
    }
}
