using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Validation;

public sealed class PackageValidatorTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    private const string StructuralArtifactKey = "facts/structural.json";

    [Fact]
    [Trait("Requirement", "STOR-25")]
    public void ReadPayloadOrThrow_UnknownProperty_AbortsSchemaNamingArtifactKey()
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Write(ValidSolutionDto()).AsSpan());
        var mutated = json.Replace("\"identity\"", "\"unknown_field\":true,\"identity\"", StringComparison.Ordinal);

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.ReadPayloadOrThrow<SolutionDto>(Encoding.UTF8.GetBytes(mutated), StructuralArtifactKey));

        Assert.Equal("schema", exception.Gate);
        Assert.Contains(StructuralArtifactKey, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-25")]
    public void ReadPayloadOrThrow_TypeMismatch_AbortsSchemaNamingArtifactKey()
    {
        var dto = ValidSolutionDto();
        var json = Encoding.UTF8.GetString(CanonicalJson.Write(dto).AsSpan());
        var mutated = json.Replace($"\"{dto.ContentSha256}\"", "true", StringComparison.Ordinal);

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.ReadPayloadOrThrow<SolutionDto>(Encoding.UTF8.GetBytes(mutated), StructuralArtifactKey));

        Assert.Equal("schema", exception.Gate);
        Assert.Contains(StructuralArtifactKey, exception.Detail, StringComparison.Ordinal);
    }

    private static SolutionDto ValidSolutionDto()
    {
        var snapshot = new FactualSnapshot(
            [Solution.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln"))],
            [],
            [],
            [],
            [],
            []);
        return DomainMapper.ToWire(snapshot, Context).Solutions[0];
    }
}
