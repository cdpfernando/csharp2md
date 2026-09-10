using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal sealed record PublicationOutcome(
    ImmutableArray<StagedFragment> Fragments,
    SolutionContribution? Contribution);

internal static class PublicationPipeline
{
    internal static PublicationOutcome Publish(
        FactualSnapshot snapshot,
        ManifestContext context,
        SolutionCoordinate coordinate,
        string packageDirectory,
        IPackageProjector? projector,
        IBatchComposer? composer,
        ISourceDocumentReader source,
        Func<WireDocument, PublishedPackageView>? createView = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        var document = DomainMapper.ToWire(snapshot, context);
        var report = PackageValidator.Validate(document);
        // An effectively unbounded ceiling: activating the derived, real ceiling for every live analysis
        // is a later task's own change (it touches a wide swath of existing Analysis-layer expectations
        // that this batch's own scope, Storage and Projection, is not meant to move). LayoutPlanner's
        // adaptive sharding itself is fully implemented and tested against an explicit ceiling.
        var plan = LayoutPlanner.Plan(report.Document, int.MaxValue);
        var projections = ImmutableArray<StagedFragment>.Empty;
        SolutionContribution? contribution = null;
        if (projector is not null || composer is not null)
        {
            var view = createView is null
                ? PublishedPackageView.From(report.Document, plan)
                : createView(report.Document);
            if (projector is not null)
            {
                try
                {
                    projections = projector.Project(view, source);
                }
                catch (PublicationRejectedException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new PublicationRejectedException("projection", exception.Message, exception);
                }

                ProjectionValidator.Validate(view, projections);
            }

            if (composer is not null)
            {
                contribution = composer.Contribute(view, coordinate, packageDirectory);
            }
        }

        var fragments = PackagePublisher.ToPublicationOrder(report.Document, plan, projections);
        ValidateManifestCardinality(fragments);
        return new PublicationOutcome(fragments, contribution);
    }

    /// <summary>
    /// Proves the manifest this publication is about to write agrees with the bytes it is about to write,
    /// before any of them reach disk (GCPC-061/GCPC-062) -- an abort here leaves the prior package
    /// untouched, since nothing has been written yet.
    /// </summary>
    private static void ValidateManifestCardinality(ImmutableArray<StagedFragment> fragments)
    {
        var manifestFragment = fragments.Single(static fragment => fragment.CanonicalKey == PackagePublisher.ManifestKey);
        var manifest = PackageValidator.ReadPayloadOrThrow<ManifestEnvelope>(
            manifestFragment.Payload.AsSpan(), PackagePublisher.ManifestKey);

        var artifactsByKey = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal);
        var deferredKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fragment in fragments)
        {
            if (fragment.Role != ArtifactRole.Payload)
            {
                continue;
            }

            if (fragment.IsDeferred)
            {
                deferredKeys.Add(fragment.CanonicalKey);
            }
            else
            {
                artifactsByKey[fragment.CanonicalKey] = fragment.Payload;
            }
        }

        PackageValidator.ValidatePublishedManifest(manifest, artifactsByKey, deferredKeys);
    }
}
