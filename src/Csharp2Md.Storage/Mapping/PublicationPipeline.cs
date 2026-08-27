using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Validation;

namespace Csharp2Md.Storage.Mapping;

internal static class PublicationPipeline
{
    internal static ImmutableArray<StagedFragment> Publish(
        FactualSnapshot snapshot,
        ManifestContext context,
        IPackageProjector? projector,
        ISourceDocumentReader source)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        var document = DomainMapper.ToWire(snapshot, context);
        var report = PackageValidator.Validate(document);
        var projections = ImmutableArray<StagedFragment>.Empty;
        if (projector is not null)
        {
            var view = PublishedPackageView.From(report.Document);
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
        }

        return PackagePublisher.ToPublicationOrder(report.Document, projections);
    }
}
