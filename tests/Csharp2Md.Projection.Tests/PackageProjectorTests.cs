using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests;

public sealed class PackageProjectorTests
{
    [Fact]
    [Trait("Requirement", "RP-01")]
    public void IPackageProjector_IsPublicOnStorageAssembly()
    {
        Assert.True(typeof(IPackageProjector).IsPublic);
        Assert.Equal("Csharp2Md.Storage", typeof(IPackageProjector).Assembly.GetName().Name);
    }

    [Fact]
    [Trait("Requirement", "RP-01")]
    public void IPackageProjector_Project_TakesViewAndSourceReader()
    {
        var method = typeof(IPackageProjector).GetMethod(nameof(IPackageProjector.Project));
        Assert.NotNull(method);
        Assert.Equal(typeof(ImmutableArray<StagedFragment>), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(PublishedPackageView), parameters[0].ParameterType);
        Assert.Equal(typeof(ISourceDocumentReader), parameters[1].ParameterType);
    }

    [Fact]
    [Trait("Requirement", "RP-01")]
    public void PackageProjector_ImplementsIPackageProjector()
    {
        Assert.Contains(typeof(IPackageProjector), typeof(PackageProjector).GetInterfaces());
        Assert.True(typeof(PackageProjector).IsSealed);
    }

    [Fact]
    [Trait("Requirement", "RP-01")]
    public void PackageProjector_Project_ReturnsEmptyFragmentArray()
    {
        IPackageProjector projector = new PackageProjector();

        var fragments = projector.Project(EmptyView(), new EmptyReader());

        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.StartsWith("markdown/", StringComparison.Ordinal));
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == "retrieval.md");
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == "AGENTS.md");
    }

    private static PublishedPackageView EmptyView()
    {
        var versions = Csharp2Md.Domain.Registry.TaxonomyVersions.Initial;
        var zero = new CoverageMetricDto(0, 0, 0, 0, []);
        return PublishedPackageView.From(
            new WireDocument(
                new ManifestEnvelope(
                    versions.SchemaVersion,
                    versions.TaxonomyVersion,
                    versions.ObservationSchemaVersion,
                    "s-test",
                    "Acme.sln",
                    []),
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                ImmutableDictionary<string, ImmutableArray<ObservationDto>>.Empty,
                ImmutableDictionary<string, ImmutableArray<ConfirmedRelationDto>>.Empty,
                [],
                [],
                [],
                [],
                new CoverageEnvelope(zero, zero, zero, zero),
                new RunCertificationEnvelope("not_evaluated"),
                new DiagnosticsEnvelope([]),
                new MeasurementsEnvelope([])));
    }

    private sealed class EmptyReader : ISourceDocumentReader
    {
        public ImmutableArray<DocumentId> Documents => [];

        public bool TryRead(DocumentId document, out ImmutableArray<byte> bytes)
        {
            bytes = default;
            return false;
        }
    }
}
