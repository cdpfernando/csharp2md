using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Reading;

internal static class PackageDirectoryWriter
{
    public static void Write(string packageDirectory, WireDocument document) =>
        Write(packageDirectory, PackagePublisher.ToPublicationOrder(document));

    public static void Write(
        string packageDirectory,
        WireDocument document,
        LayoutPlan plan,
        ImmutableArray<StagedFragment> projections = default) =>
        Write(packageDirectory, PackagePublisher.ToPublicationOrder(document, plan, projections));

    private static void Write(string packageDirectory, ImmutableArray<StagedFragment> fragments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        Directory.CreateDirectory(packageDirectory);
        foreach (var fragment in fragments)
        {
            var destination = Path.Combine(
                packageDirectory,
                fragment.CanonicalKey.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(destination);
            ArgumentException.ThrowIfNullOrEmpty(directory);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(destination, [.. fragment.Payload]);
        }
    }
}
