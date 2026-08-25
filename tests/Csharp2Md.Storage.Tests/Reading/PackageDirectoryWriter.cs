using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Reading;

internal static class PackageDirectoryWriter
{
    public static void Write(string packageDirectory, WireDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentNullException.ThrowIfNull(document);

        Directory.CreateDirectory(packageDirectory);
        foreach (var fragment in PackagePublisher.ToPublicationOrder(document))
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
