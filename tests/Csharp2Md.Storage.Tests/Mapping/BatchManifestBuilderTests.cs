using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class BatchManifestBuilderTests
{
    [Fact]
    [Trait("Requirement", "MSC-03")]
    public void From_EachSolutionEntry_CarriesIdentityFileNamePackageDirectoryAndStatus()
    {
        var orders = Committed("Zeta.slnx");
        var envelope = BatchManifestBuilder.From(View(orders), []);

        var entry = Assert.Single(envelope.Solutions);
        Assert.Equal(orders.Identity.Value, entry.Identity);
        Assert.Equal("Zeta.slnx", entry.SolutionFileName);
        Assert.Equal(BatchManifestBuilder.PackageDirectoryName(orders.Identity.Value), entry.PackageDirectory);
        Assert.StartsWith("s-", entry.PackageDirectory, StringComparison.Ordinal);
        Assert.Equal(32, entry.PackageDirectory.Length - 2);
        Assert.Equal("committed", entry.Status);
        Assert.Null(entry.FailingStage);
    }

    [Fact]
    [Trait("Requirement", "MSC-03")]
    [Trait("Requirement", "MSC-09")]
    public void From_UnpublishedEntry_CarriesFailingStage()
    {
        var envelope = BatchManifestBuilder.From(
            View(Unpublished("Payments.slnx", "Semantics")),
            []);

        var entry = Assert.Single(envelope.Solutions);
        Assert.Equal("unpublished", entry.Status);
        Assert.Equal("Semantics", entry.FailingStage);
    }

    [Fact]
    [Trait("Requirement", "MSC-04")]
    public void From_OrdersSolutionsByIdentityAndArtifactsByCanonicalKey()
    {
        var zeta = Committed("Zeta.slnx");
        var alpha = Committed("Alpha.slnx");
        var fragments = ImmutableArray.Create(
            Payload("composition/shared-contracts.json", 1),
            Payload("composition/cross-solution-relations.json", 2));

        var envelope = BatchManifestBuilder.From(View(zeta, alpha), fragments);

        Assert.Equal(
            [alpha.Identity.Value, zeta.Identity.Value],
            envelope.Solutions.Select(static entry => entry.Identity));
        Assert.True(
            StringComparer.Ordinal.Compare(alpha.Identity.Value, zeta.Identity.Value) < 0);
        Assert.Equal(
            [
                "composition/cross-solution-relations.json",
                "composition/shared-contracts.json",
            ],
            envelope.Artifacts.Select(static entry => entry.CanonicalKey));
        Assert.Equal("payload", envelope.Artifacts[0].Role);
        Assert.Equal(2, envelope.Artifacts[0].Count);
        Assert.Equal(1, envelope.Artifacts[1].Count);
    }

    [Fact]
    [Trait("Requirement", "MSC-10")]
    public void From_AnyUnpublished_EmitsIncompleteWithSolutionUnpublishedReason()
    {
        var envelope = BatchManifestBuilder.From(
            View(Committed("Orders.slnx"), Unpublished("Payments.slnx", "Inventory")),
            []);

        Assert.False(envelope.Complete);
        Assert.Equal("solution-unpublished", envelope.IncompleteScopeReason);

        var json = Encoding.UTF8.GetString(CanonicalJson.Write(envelope).AsSpan());
        Assert.Contains("\"complete\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"incomplete_scope_reason\": \"solution-unpublished\"", json, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-11")]
    public void From_FullyCommitted_EmitsCompleteAndOmitsIncompleteScopeReasonKey()
    {
        var envelope = BatchManifestBuilder.From(
            View(Committed("Orders.slnx"), Committed("Payments.slnx")),
            []);

        Assert.True(envelope.Complete);
        Assert.Null(envelope.IncompleteScopeReason);

        var json = Encoding.UTF8.GetString(CanonicalJson.Write(envelope).AsSpan());
        Assert.Contains("\"complete\": true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("incomplete_scope_reason", json, StringComparison.Ordinal);
    }

    private static BatchView View(params BatchSolutionRecord[] records) =>
        new([.. records], []);

    private static BatchSolutionRecord Committed(string fileName) =>
        new(
            SolutionId.Create(WorkspaceIdentity.Create("default"), fileName),
            fileName,
            PublicationStatus.Committed,
            null);

    private static BatchSolutionRecord Unpublished(string fileName, string failingStage) =>
        new(
            SolutionId.Create(WorkspaceIdentity.Create("default"), fileName),
            fileName,
            PublicationStatus.Unpublished,
            failingStage);

    private static StagedFragment Payload(string canonicalKey, int count)
    {
        var items = string.Join(",", Enumerable.Range(0, count).Select(static _ => "{}"));
        return new StagedFragment(
            ArtifactRole.Payload,
            canonicalKey,
            Encoding.UTF8.GetBytes("[" + items + "]").ToImmutableArray());
    }
}
