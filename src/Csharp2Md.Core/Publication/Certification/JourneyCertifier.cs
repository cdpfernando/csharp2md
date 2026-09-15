using Csharp2Md.Core.PackageBuilding;

namespace Csharp2Md.Core.Publication.Certification;

internal sealed record JourneyMeasurement(int Reads, long Bytes, long Tokens)
{
    internal static JourneyMeasurement Empty { get; } = new(0, 0, 0);
}

internal sealed class MeasuredPackageReader : IDisposable
{
    private readonly PackageReader _reader;
    private readonly HashSet<string> _opened = new(StringComparer.Ordinal);
    private long _bytes;
    private bool _disposed;

    private MeasuredPackageReader(PackageReader reader) => _reader = reader;

    internal PackageManifest Manifest => _reader.Manifest;
    internal JourneyMeasurement Measurement => new(_opened.Count, _bytes, (long)Math.Ceiling(_bytes / PackageManifest.TokenDivisorValue));

    internal static MeasuredPackageReader Open(string packageDirectory) => new(PackageReader.Open(packageDirectory));

    internal ImmutableArray<byte> OpenArtifact(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bytes = _reader.ReadArtifact(path);
        if (_opened.Add(path))
        {
            _bytes += bytes.Length;
        }
        return bytes;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
    }
}

internal static class JourneyCertifier
{
    internal static PackageCertification Certify(string packageDirectory)
    {
        using var reader = MeasuredPackageReader.Open(packageDirectory);
        return Certify(reader);
    }

    internal static PackageCertification Certify(MeasuredPackageReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var journeys = ImmutableArray.CreateBuilder<JourneyCertification>();
        journeys.Add(CertifyLocate(reader));
        journeys.Add(CertifyEvidence(reader));
        journeys.AddRange(GraphJourneyCertifier.Certify(reader));
        return new PackageCertification(journeys.ToImmutable());
    }

    private static JourneyCertification CertifyLocate(MeasuredPackageReader reader)
    {
        if (reader.Manifest.Roots.IsDefaultOrEmpty)
        {
            return NotApplicable(JourneyKind.Locate, "no-root");
        }

        var root = reader.Manifest.Roots[0];
        try
        {
            reader.OpenArtifact("manifest.json");
            reader.OpenArtifact(Entry(reader.Manifest, JourneyKind.Locate));
            reader.OpenArtifact(root.MarkdownPath);
            return Budget(JourneyKind.Locate, reader.Measurement, root.DisplayName.StartsWith("component:", StringComparison.Ordinal) ? 5 : 8, 12_000);
        }
        catch (FileNotFoundException)
        {
            return Failed(JourneyKind.Locate, "missing-terminal");
        }
    }

    private static JourneyCertification CertifyEvidence(MeasuredPackageReader reader)
    {
        if (reader.Manifest.Indexes.All(index => index.Name != "evidence"))
        {
            return NotApplicable(JourneyKind.EvidenceDisposition, "no-evidence-index");
        }

        try
        {
            reader.OpenArtifact("manifest.json");
            reader.OpenArtifact(Entry(reader.Manifest, JourneyKind.EvidenceDisposition));
            return Budget(JourneyKind.EvidenceDisposition, reader.Measurement, 12, 25_000);
        }
        catch (FileNotFoundException)
        {
            return Failed(JourneyKind.EvidenceDisposition, "missing-terminal");
        }
    }

    private static string Entry(PackageManifest manifest, JourneyKind kind) => manifest.Journeys.Single(journey => journey.Kind == kind).EntryPath;

    private static JourneyCertification Budget(JourneyKind kind, JourneyMeasurement measurement, int reads, long tokens) =>
        measurement.Reads > reads ? Failed(kind, $"reads-exceeded:{measurement.Reads}>{reads}") :
        measurement.Tokens > tokens ? Failed(kind, $"tokens-exceeded:{measurement.Tokens}>{tokens}") :
        new JourneyCertification(kind, JourneyCertificationStatus.Passed, $"reads:{measurement.Reads};bytes:{measurement.Bytes};tokens:{measurement.Tokens}");

    private static JourneyCertification NotApplicable(JourneyKind kind, string reason) => new(kind, JourneyCertificationStatus.NotApplicable, $"not_applicable:{reason}");
    private static JourneyCertification Failed(JourneyKind kind, string detail) => new(kind, JourneyCertificationStatus.Failed, detail);
}
