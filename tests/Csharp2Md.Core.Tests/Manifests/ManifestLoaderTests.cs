using Csharp2Md.Core.Manifests;

namespace Csharp2Md.Core.Tests.Manifests;

public sealed class ManifestLoaderTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose() => File.Delete(_tempFile);

    [Fact]
    public void Load_ValidManifestWithSingleEntry_ReturnsSuccessWithParsedPath()
    {
        File.WriteAllText(_tempFile, """{ "services": [ { "path": "services/*" } ] }""");

        var result = ManifestLoader.Load(_tempFile);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Manifest!.Services);
        Assert.Equal("services/*", entry.Path);
    }

    [Fact]
    public void Load_ValidManifestWithOptionalFields_ParsesNameAndProjects()
    {
        File.WriteAllText(
            _tempFile,
            """
            {
              "services": [
                { "path": "services/orders", "name": "Orders", "projects": ["Orders.Api.csproj"] }
              ]
            }
            """);

        var result = ManifestLoader.Load(_tempFile);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Manifest!.Services);
        Assert.Equal("Orders", entry.Name);
        Assert.Equal(["Orders.Api.csproj"], entry.Projects);
    }

    [Fact]
    public void Load_MultipleEntries_ReturnsAllInOrder()
    {
        File.WriteAllText(
            _tempFile,
            """{ "services": [ { "path": "services/a" }, { "path": "services/b" } ] }""");

        var result = ManifestLoader.Load(_tempFile);

        Assert.True(result.IsSuccess);
        Assert.Equal(["services/a", "services/b"], result.Manifest!.Services.Select(s => s.Path));
    }

    [Fact]
    public void Load_MissingFile_ReturnsFileMissingError()
    {
        File.Delete(_tempFile);

        var result = ManifestLoader.Load(_tempFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.FileMissing, result.Error!.Value.Code);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedJsonError()
    {
        File.WriteAllText(_tempFile, "{ not valid json ]");

        var result = ManifestLoader.Load(_tempFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.MalformedJson, result.Error!.Value.Code);
    }

    [Fact]
    public void Load_EmptyServicesArray_ReturnsZeroEntriesError()
    {
        File.WriteAllText(_tempFile, """{ "services": [] }""");

        var result = ManifestLoader.Load(_tempFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.ZeroEntries, result.Error!.Value.Code);
    }

    [Fact]
    public void Load_JsonLiteralNull_ReturnsZeroEntriesError()
    {
        File.WriteAllText(_tempFile, "null");

        var result = ManifestLoader.Load(_tempFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.ZeroEntries, result.Error!.Value.Code);
    }

    [Fact]
    public void Load_NullServicesProperty_ReturnsZeroEntriesErrorInsteadOfThrowing()
    {
        File.WriteAllText(_tempFile, """{ "services": null }""");

        var result = ManifestLoader.Load(_tempFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.ZeroEntries, result.Error!.Value.Code);
    }

    [Fact]
    public void Load_MissingServicesKey_ReturnsZeroEntriesErrorInsteadOfThrowing()
    {
        File.WriteAllText(_tempFile, "{}");

        var result = ManifestLoader.Load(_tempFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.ZeroEntries, result.Error!.Value.Code);
    }
}
