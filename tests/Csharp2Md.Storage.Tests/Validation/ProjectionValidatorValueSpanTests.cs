using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;

namespace Csharp2Md.Storage.Tests.Validation;

public sealed class ProjectionValidatorValueSpanTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");
    private const string SourceKey = "source/acme.orders/Program.cs";
    private const string LocatorName = "source/acme.orders/Program.cs:1:1-1:80";
    private const string Page = "pages/entry-points.md";
    private const string MissingValue = "NOT-A-PAYLOAD-VALUE";

    [Fact]
    [Trait("Requirement", "RP-44")]
    public void Validate_MarkdownValueMismatch_AbortsNamingPageAndValue()
    {
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(EmptyView(), ValueFragment(MissingValue)));

        Assert.Equal("projection-value", exception.Gate);
        Assert.Contains(Page, exception.Detail, StringComparison.Ordinal);
        Assert.Contains(MissingValue, exception.Detail, StringComparison.Ordinal);
        Assert.Contains(Page, exception.Message, StringComparison.Ordinal);
        Assert.Contains(MissingValue, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-44")]
    public void Validate_MarkdownValuePresentInPayload_DoesNotThrow()
    {
        ProjectionValidator.Validate(EmptyView(), ValueFragment("numerator"));
    }

    [Fact]
    [Trait("Requirement", "RP-45")]
    public void Validate_LocatorSpanOutsideSource_AbortsNamingTheLocator()
    {
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(EmptyView(), SourceAndSpan(endColumn: 80)));

        Assert.Equal("projection-span", exception.Gate);
        Assert.Contains(LocatorName, exception.Detail, StringComparison.Ordinal);
        Assert.Contains(LocatorName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-45")]
    public void Validate_LocatorSpanInsideSource_DoesNotThrow()
    {
        ProjectionValidator.Validate(EmptyView(), SourceAndSpan(endColumn: 5));
    }

    [Fact]
    [Trait("Requirement", "RP-45")]
    public void Validate_LocatorSpanPastLastLine_AbortsNamingTheLocator()
    {
        var json = "{\"locator\":\"" + LocatorName + "\",\"artifact_key\":\"" + SourceKey
            + "\",\"ordinal\":0,\"span\":{\"start_line\":9,\"start_column\":1,\"end_line\":9,\"end_column\":1}}";
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(EmptyView(), SourceWithCitation(json)));

        Assert.Equal("projection-span", exception.Gate);
        Assert.Contains(LocatorName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-44")]
    public void Validate_ValueMismatch_NamesPageFromEmbeddedField()
    {
        const string otherPage = "pages/components.md";
        var json = "{\"page\":\"" + otherPage + "\",\"value\":\"" + MissingValue
            + "\",\"artifact_key\":\"coverage.json\",\"ordinal\":0}";
        var exception = Assert.Throws<PublicationRejectedException>(
            () => ProjectionValidator.Validate(
                EmptyView(),
                [new StagedFragment(
                    ArtifactRole.Payload,
                    "pages/ignored.md",
                    Encoding.UTF8.GetBytes(json).ToImmutableArray())]));

        Assert.Equal("projection-value", exception.Gate);
        Assert.Contains(otherPage, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pages/ignored.md", exception.Detail, StringComparison.Ordinal);
    }

    private static PublishedPackageView EmptyView() =>
        PublishedPackageView.From(
            PackageValidator.Validate(DomainMapper.ToWire(FactualSnapshot.Empty, Context)).Document);

    private static ImmutableArray<StagedFragment> ValueFragment(string value)
    {
        var json = "{\"page\":\"" + Page + "\",\"value\":\"" + value
            + "\",\"artifact_key\":\"coverage.json\",\"ordinal\":0}";
        return [new StagedFragment(
            ArtifactRole.Payload,
            Page,
            Encoding.UTF8.GetBytes(json).ToImmutableArray())];
    }

    private static ImmutableArray<StagedFragment> SourceAndSpan(int endColumn)
    {
        var json = "{\"locator\":\"" + LocatorName + "\",\"artifact_key\":\"" + SourceKey
            + "\",\"ordinal\":0,\"span\":{\"start_line\":1,\"start_column\":1,\"end_line\":1,\"end_column\":"
            + endColumn.ToString() + "}}";
        return SourceWithCitation(json);
    }

    private static ImmutableArray<StagedFragment> SourceWithCitation(string citationJson) =>
        [
            new StagedFragment(
                ArtifactRole.Payload,
                SourceKey,
                Encoding.UTF8.GetBytes("hello").ToImmutableArray()),
            new StagedFragment(
                ArtifactRole.Payload,
                "projections/locator.json",
                Encoding.UTF8.GetBytes(citationJson).ToImmutableArray()),
        ];
}
