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
    /// Reads <paramref name="packageDirectory"/> and rebuilds its contribution. The facts a compound family
    /// (components, deployment units, contracts, boundary operations, external systems) cites are never
    /// sharded regardless of the ceiling in force when the package was published (<see cref="LayoutPlanner"/>
    /// only ever shards flat record-array families), so re-planning with the unsplit default here yields
    /// citations identical to whatever the original publication assigned -- a fresh <see cref="LayoutPlan"/>
    /// is safe to build from the re-hydrated facts alone, with no need to recover the original ceiling.
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
        var view = PublishedPackageView.From(document);
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
