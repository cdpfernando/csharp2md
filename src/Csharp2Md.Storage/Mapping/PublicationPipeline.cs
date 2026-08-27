using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Validation;

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
        var projections = ImmutableArray<StagedFragment>.Empty;
        SolutionContribution? contribution = null;
        if (projector is not null || composer is not null)
        {
            var view = createView is null
                ? PublishedPackageView.From(report.Document)
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

        return new PublicationOutcome(
            PackagePublisher.ToPublicationOrder(report.Document, projections),
            contribution);
    }
}
