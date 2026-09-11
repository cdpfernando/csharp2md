using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage;

/// <summary>
/// Lets <c>compose</c> work over already-published packages with no solution present (GCPC-068):
/// re-hydrates one package through <see cref="FactualPackageReader"/> (AD-025 -- the same manifest-driven
/// re-hydration <c>validate</c> uses) and rebuilds the <see cref="SolutionContribution"/> a live
/// <c>analyze</c> would have produced for it, via the caller-supplied <see cref="IBatchComposer"/>
/// unchanged (design.md: <c>compose</c> reuses <c>BatchComposer</c> wholesale; only the contribution
/// source changes). <see cref="Csharp2Md.Storage"/> defines <see cref="IBatchComposer"/> but never
/// references its concrete <c>Csharp2Md.Projection</c> implementation, so the composer is a parameter, not
/// a dependency this type owns.
/// </summary>
public static class ContributionReader
{
    /// <summary>
    /// Reads <paramref name="packageDirectory"/> and rebuilds its contribution. F1 (GCPC-038/039) gave
    /// the compound fact families the same adaptive ceiling-driven sharding flat record-array families
    /// already had, so a fact's citation now depends on the ceiling the original publication planned
    /// with -- re-planning with the unsplit default here would assign a different (unsplit) citation to
    /// the same fact and desync <c>compose</c>'s output from what a live <c>analyze</c> produced. The
    /// published provenance carries that exact ceiling (GCPC-058), so re-planning with it reproduces
    /// the identical shard assignment (GCPC-042: same input, same ceiling, same shards) and therefore
    /// the identical citations.
    /// </summary>
    public static SolutionContribution Read(string packageDirectory, IBatchComposer composer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentNullException.ThrowIfNull(composer);

        var result = FactualPackageReader.Read(packageDirectory);

        var manifestBytes = File.ReadAllBytes(Path.Combine(packageDirectory, PackagePublisher.ManifestKey));
        var manifest = PackageValidator.ReadPayloadOrThrow<ManifestEnvelope>(manifestBytes, PackagePublisher.ManifestKey);

        var context = new ManifestContext(manifest.SolutionKey, manifest.SolutionFileName);
        var document = DomainMapper.ToWire(result.Snapshot, context);
        var ceilingBytes = manifest.Provenance is { ArtifactCeilingBytes: > 0 } provenance
            ? provenance.ArtifactCeilingBytes
            : CeilingCalculator.Derive().CeilingBytes;
        var view = PublishedPackageView.From(document, LayoutPlanner.Plan(document, ceilingBytes));
        var coordinate = SolutionCoordinate.For(manifest.SolutionFileName);

        // The bare directory name (e.g. "s-<hash>"), matching exactly what a live analyze run's own
        // PublicationPipeline.Publish passes as packageDirectory (FilesystemTransactionalStore.Session.Commit
        // passes Path.GetFileName(_childPath), never the full path) -- so a SolutionContribution built here
        // is byte-identical to one a live publish would have produced, whatever absolute path this package
        // directory happens to live at when compose is invoked.
        var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageDirectory)));

        return composer.Contribute(view, coordinate, directoryName);
    }
}
