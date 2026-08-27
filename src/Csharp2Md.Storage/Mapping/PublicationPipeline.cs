using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Validation;

namespace Csharp2Md.Storage.Mapping;

internal static class PublicationPipeline
{
    internal static ImmutableArray<StagedFragment> Publish(FactualSnapshot snapshot, ManifestContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);

        var document = DomainMapper.ToWire(snapshot, context);
        var report = PackageValidator.Validate(document);
        return PackagePublisher.ToPublicationOrder(report.Document);
    }
}
