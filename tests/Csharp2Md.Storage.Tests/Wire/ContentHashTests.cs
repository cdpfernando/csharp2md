using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Wire;

public sealed class ContentHashTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    [Trait("Requirement", "STOR-42")]
    public void TwoWrites_OfTheSameRecord_ProduceIdenticalContentHashes()
    {
        var snapshot = Snapshot(Solution.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln")));

        var first = DomainMapper.ToWire(snapshot, Context).Solutions[0].ContentSha256;
        var second = DomainMapper.ToWire(snapshot, Context).Solutions[0].ContentSha256;

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    [Trait("Requirement", "STOR-28")]
    [Trait("Requirement", "STOR-42")]
    public void ContentHash_CoversCanonicalBytesWithContentSha256Omitted()
    {
        var snapshot = Snapshot(Solution.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln")));
        var dto = DomainMapper.ToWire(snapshot, Context).Solutions[0];

        var omitted = Encoding.UTF8.GetString(CanonicalJson.WriteOmittingContentSha256(dto).AsSpan());
        Assert.DoesNotContain("\"content_sha256\"", omitted, StringComparison.Ordinal);

        var computed = CanonicalJson.PayloadContentSha256(dto);
        Assert.Equal(dto.ContentSha256, computed);

        var tampered = new string('0', 64);
        Assert.NotEqual(tampered, computed);
    }

    private static FactualSnapshot Snapshot(IFact fact) =>
        new([fact], [], [], [], [], []);
}
