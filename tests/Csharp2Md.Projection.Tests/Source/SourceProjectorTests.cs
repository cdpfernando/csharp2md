using System.Text;
using Csharp2Md.Projection.Source;

namespace Csharp2Md.Projection.Tests.Source;

public sealed class SourceProjectorTests
{
    [Fact]
    [Trait("Requirement", "RP-07")]
    public void Project_InventoriedDocuments_EmitsExactlyOneArtifactEach()
    {
        var (view, reader, documents) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", "class Program;"u8.ToArray()),
            ("Acme.Payments/Acme.Payments.csproj", "Acme.Payments/Program.cs", "class Payments;"u8.ToArray()));

        var fragments = SourceProjector.Project(view, reader);

        Assert.Equal(documents.Length, fragments.Length);
        Assert.All(fragments, fragment => Assert.StartsWith("source/", fragment.CanonicalKey, StringComparison.Ordinal));
        Assert.Equal(fragments.Length, fragments.Select(fragment => fragment.CanonicalKey).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    [Trait("Requirement", "RP-08")]
    public void Project_ArtifactKeys_ContainNoAbsoluteOrCloneDependentSegment()
    {
        var cloneRoot = OperatingSystem.IsWindows() ? @"D:\clones\acme" : "/tmp/clones/acme";
        var (view, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", "class Program;"u8.ToArray()));

        var fragments = SourceProjector.Project(view, reader);

        var key = Assert.Single(fragments).CanonicalKey;
        Assert.False(Path.IsPathRooted(key), key);
        Assert.DoesNotContain(cloneRoot, key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":", key, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", key, StringComparison.Ordinal);
        Assert.StartsWith("source/", key, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-08")]
    public void Project_SharedRelativePathInDifferentProjects_ProducesDistinctKeys()
    {
        const string relative = "src/Program.cs";
        var (view, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", relative, "orders"u8.ToArray()),
            ("Acme.Payments/Acme.Payments.csproj", relative, "payments"u8.ToArray()));

        var fragments = SourceProjector.Project(view, reader);

        Assert.Equal(2, fragments.Length);
        Assert.All(fragments, fragment => Assert.Contains(relative, fragment.CanonicalKey, StringComparison.Ordinal));
        Assert.NotEqual(fragments[0].CanonicalKey, fragments[1].CanonicalKey);
        Assert.Contains(fragments, fragment => fragment.CanonicalKey.Contains("acme.orders", StringComparison.Ordinal));
        Assert.Contains(fragments, fragment => fragment.CanonicalKey.Contains("acme.payments", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void Project_NonUtf8Bytes_PassThroughUnchanged()
    {
        var opaque = new byte[] { 0x00, 0xFF, 0xFE, 0x80, 0x7F };
        var (view, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/blob.bin", opaque));

        var fragments = SourceProjector.Project(view, reader);

        var fragment = Assert.Single(fragments);
        Assert.True(fragment.IsDeferred);
        Assert.True(opaque.AsSpan().SequenceEqual(fragment.ReadPayload().AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void Project_ReadableDocument_DefersPayloadUntilRead()
    {
        var utf8 = Encoding.UTF8.GetBytes("namespace Acme;");
        var (view, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Code.cs", utf8));

        var fragment = Assert.Single(SourceProjector.Project(view, reader));

        Assert.True(fragment.IsDeferred);
        Assert.True(fragment.Payload.IsDefaultOrEmpty);
        Assert.True(utf8.AsSpan().SequenceEqual(fragment.ReadPayload().AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    [Trait("Requirement", "RP-08")]
    public void PackageProjector_ComposesSourceProjectorInCanonicalOrder()
    {
        var (view, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Zeta/Zeta.csproj", "Zeta/z.cs", "z"u8.ToArray()),
            ("Alpha/Alpha.csproj", "Alpha/a.cs", "a"u8.ToArray()));

        var composed = new PackageProjector().Project(view, reader);
        var direct = SourceProjector.Project(view, reader);
        var ordered = view.Document.Documents
            .OrderBy(static dto => dto.Identity.Id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            direct.Select(fragment => fragment.CanonicalKey),
            composed.Select(fragment => fragment.CanonicalKey).Take(direct.Length));
        Assert.True(composed.Length >= ordered.Length);
        for (var i = 0; i < ordered.Length; i++)
        {
            Assert.Contains(ordered[i].RelativePath, composed[i].CanonicalKey, StringComparison.Ordinal);
        }
    }
}
