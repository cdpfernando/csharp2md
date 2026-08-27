using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;

namespace Csharp2Md.Storage.Tests.Validation;

public sealed class ProjectionValidatorTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    [Trait("Requirement", "RP-42")]
    public void Validate_MissingArtifactKey_AbortsNamingTheKey()
    {
        const string missing = "facts/absent.json";
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(EmptyView(), CitationFragment(missing, 0)));

        Assert.Equal("projection-key", exception.Gate);
        Assert.Equal(missing, exception.Detail);
        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-43")]
    public void Validate_OrdinalOutOfRange_AbortsNamingKeyAndOrdinal()
    {
        const string key = "coverage.json";
        const int ordinal = 1;
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(EmptyView(), CitationFragment(key, ordinal)));

        Assert.Equal("projection-ordinal", exception.Gate);
        Assert.Contains(key, exception.Detail, StringComparison.Ordinal);
        Assert.Contains(ordinal.ToString(), exception.Detail, StringComparison.Ordinal);
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
        Assert.Contains(ordinal.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-41")]
    [Trait("Requirement", "RP-43")]
    public void Validate_NegativeOrdinal_AbortsNamingKeyAndOrdinal()
    {
        const string key = "coverage.json";
        const int ordinal = -1;
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(EmptyView(), CitationFragment(key, ordinal)));

        Assert.Equal("projection-ordinal", exception.Gate);
        Assert.Contains(key, exception.Detail, StringComparison.Ordinal);
        Assert.Contains(ordinal.ToString(), exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-41")]
    public void Validate_InRangeCitation_DoesNotThrow()
    {
        ProjectionValidator.Validate(EmptyView(), CitationFragment("coverage.json", 0));
    }

    [Theory]
    [Trait("Requirement", "RP-49")]
    [InlineData("C:/windows/system32")]
    [InlineData(@"C:\src\acme")]
    public void Validate_WindowsAbsolutePathInProjection_AbortsNamingTheArtifact(string absolutePath)
    {
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(EmptyView(), JsonNoteFragment(absolutePath)));

        Assert.Equal("absolute-path", exception.Gate);
        Assert.Equal("catalogs/notes.json", exception.Detail);
    }

    [Theory]
    [Trait("Requirement", "RP-49")]
    [InlineData("/home/runner/work/repo")]
    [InlineData("/opt/app")]
    [InlineData("/Users/me/src")]
    public void Validate_UnixAbsolutePathInProjection_AbortsNamingTheArtifact(string absolutePath)
    {
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(EmptyView(), JsonNoteFragment(absolutePath)));

        Assert.Equal("absolute-path", exception.Gate);
        Assert.Equal("catalogs/notes.json", exception.Detail);
    }

    [Fact]
    [Trait("Requirement", "RP-49")]
    public void Validate_WindowsAbsolutePathInMarkdown_AbortsNamingTheArtifact()
    {
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(
                EmptyView(),
                TextFragment("retrieval.md", "See C:/windows/system32 for details.")));

        Assert.Equal("absolute-path", exception.Gate);
        Assert.Equal("retrieval.md", exception.Detail);
    }

    [Fact]
    [Trait("Requirement", "RP-49")]
    public void Validate_RelativePathInProjection_DoesNotAbort()
    {
        ProjectionValidator.Validate(EmptyView(), JsonNoteFragment("src/Acme.Orders/Program.cs"));
    }

    private static PublishedPackageView EmptyView() =>
        PublishedPackageView.From(
            PackageValidator.Validate(DomainMapper.ToWire(FactualSnapshot.Empty, Context)).Document);

    private static ImmutableArray<StagedFragment> CitationFragment(string artifactKey, int ordinal)
    {
        var json = $$"""{"artifact_key":"{{artifactKey}}","ordinal":{{ordinal}}}""";
        return [new StagedFragment(ArtifactRole.Payload, "projections/cite.json", Encoding.UTF8.GetBytes(json).ToImmutableArray())];
    }

    private static ImmutableArray<StagedFragment> JsonNoteFragment(string note)
    {
        var json = $$"""{"note":{{System.Text.Json.JsonSerializer.Serialize(note)}}}""";
        return TextFragment("catalogs/notes.json", json);
    }

    private static ImmutableArray<StagedFragment> TextFragment(string key, string text) =>
        [new StagedFragment(ArtifactRole.Payload, key, Encoding.UTF8.GetBytes(text).ToImmutableArray())];
}
