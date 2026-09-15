using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.Publication.Certification;

namespace Csharp2Md.Core.Publication;

internal sealed class PackagePublicationException : InvalidOperationException
{
    internal PackagePublicationException(string cause, Exception? innerException = null) : base($"publication: '{cause}'.", innerException) { }
}

internal static class PackagePublication
{
    internal static CommittedPackage Publish(PackagePlan plan, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        using var @lock = AcquireLock(output);
        var staging = Path.Combine(output, $".staging-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(staging);
            WriteAll(staging, plan.Artifacts);
            EnsureValid(staging);
            var certification = JourneyCertifier.Certify(staging);
            if (certification.Journeys.Any(journey => journey.Status == JourneyCertificationStatus.Failed)) throw new PackagePublicationException("journey-certification");
            Write(staging, "certification.json", CanonicalJson.Write(certification));
            EnsureValid(staging);
            var generation = Path.Combine(output, "generations", plan.PackageDigest);
            Directory.CreateDirectory(Path.GetDirectoryName(generation)!);
            if (Directory.Exists(generation)) Directory.Delete(generation, recursive: true);
            Directory.Move(staging, generation);
            MaterializeRoot(generation, output);
            CleanupGenerations(output, generation);
            return new CommittedPackage(output, plan.PackageDigest, certification);
        }
        catch (PackagePublicationException) { TryDelete(staging); throw; }
        catch (Exception exception) { TryDelete(staging); throw new PackagePublicationException("staging-or-validation", exception); }
    }

    internal static PackageValidationReport Validate(string packageDirectory) => PackageValidator.Validate(packageDirectory);

    private static FileStream AcquireLock(string output) => new(Path.Combine(output, "package.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private static void EnsureValid(string directory)
    {
        var report = PackageValidator.Validate(directory);
        if (!report.Succeeded) throw new PackagePublicationException(report.Failures[0].Cause);
    }
    private static void WriteAll(string directory, IEnumerable<PlannedArtifact> artifacts)
    {
        foreach (var artifact in artifacts) Write(directory, artifact.Path.Value, artifact.Payload);
    }
    private static void Write(string directory, string relative, ImmutableArray<byte> bytes)
    {
        var path = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes.ToArray());
    }
    private static void MaterializeRoot(string generation, string output)
    {
        foreach (var source in Directory.GetFiles(generation, "*", SearchOption.AllDirectories).Where(path => !string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.Ordinal)))
        {
            var relative = Path.GetRelativePath(generation, source);
            var destination = Path.Combine(output, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
        var replacement = Path.Combine(output, $".manifest-{Guid.NewGuid():N}.json");
        File.Copy(Path.Combine(generation, "manifest.json"), replacement);
        File.Move(replacement, Path.Combine(output, "manifest.json"), overwrite: true);
    }
    private static void CleanupGenerations(string output, string current)
    {
        var generations = Path.Combine(output, "generations");
        foreach (var directory in Directory.GetDirectories(generations).Where(directory => !string.Equals(directory, current, StringComparison.Ordinal))) TryDelete(directory);
    }
    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch (IOException) { } }
}
