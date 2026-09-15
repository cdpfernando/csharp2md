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

    internal void BeginJourney()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _opened.Clear();
        _bytes = 0;
    }

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
        var solutions = ImmutableArray.CreateBuilder<SolutionCertification>();
        foreach (var solution in reader.Manifest.Solutions.OrderBy(entry => entry.Id.Value, StringComparer.Ordinal))
        {
            var journeys = ImmutableArray.CreateBuilder<JourneyCertification>();
            reader.BeginJourney();
            journeys.Add(CertifyLocate(reader, solution));
            reader.BeginJourney();
            journeys.Add(GraphJourneyCertifier.CertifyFlow(reader, solution));
            reader.BeginJourney();
            journeys.Add(GraphJourneyCertifier.CertifyImpact(reader, solution));
            reader.BeginJourney();
            journeys.Add(CertifyEvidence(reader, solution));
            solutions.Add(new SolutionCertification(solution.Id, journeys.ToImmutable()));
        }

        return new PackageCertification(solutions.ToImmutable());
    }

    private static JourneyCertification CertifyLocate(MeasuredPackageReader reader, SolutionManifestEntry solution)
    {
        if (solution.Roots.IsDefaultOrEmpty)
        {
            return NotApplicable(JourneyKind.Locate, "no-root");
        }

        var root = solution.Roots[0];
        try
        {
            reader.OpenArtifact("manifest.json");
            reader.OpenArtifact(Entry(solution, JourneyKind.Locate));
            reader.OpenArtifact(root.MarkdownPath);
            return Budget(JourneyKind.Locate, reader.Measurement, root.DisplayName.StartsWith("component:", StringComparison.Ordinal) ? 5 : 8, 12_000);
        }
        catch (FileNotFoundException)
        {
            return Failed(JourneyKind.Locate, "missing-terminal");
        }
    }

    private static JourneyCertification CertifyEvidence(MeasuredPackageReader reader, SolutionManifestEntry solution)
    {
        if (solution.Indexes.All(index => index.Kind != NavigationIndexKind.Evidence))
        {
            return NotApplicable(JourneyKind.EvidenceDisposition, "no-evidence-index");
        }

        try
        {
            reader.OpenArtifact("manifest.json");
            reader.OpenArtifact(Entry(solution, JourneyKind.EvidenceDisposition));
            return Budget(JourneyKind.EvidenceDisposition, reader.Measurement, 12, 25_000);
        }
        catch (FileNotFoundException)
        {
            return Failed(JourneyKind.EvidenceDisposition, "missing-terminal");
        }
    }

    internal static string Entry(SolutionManifestEntry solution, JourneyKind kind)
    {
        var journey = solution.Journeys.Single(candidate => candidate.Kind == kind);
        return solution.Indexes.Single(index => index.Kind == journey.EntryIndex).EntryPath;
    }

    private static JourneyCertification Budget(JourneyKind kind, JourneyMeasurement measurement, int reads, long tokens) =>
        measurement.Reads > reads ? Failed(kind, $"reads-exceeded:{measurement.Reads}>{reads}") :
        measurement.Tokens > tokens ? Failed(kind, $"tokens-exceeded:{measurement.Tokens}>{tokens}") :
        new JourneyCertification(kind, JourneyCertificationStatus.Passed, $"reads:{measurement.Reads};bytes:{measurement.Bytes};tokens:{measurement.Tokens}");

    private static JourneyCertification NotApplicable(JourneyKind kind, string reason) => new(kind, JourneyCertificationStatus.NotApplicable, $"not_applicable:{reason}");
    private static JourneyCertification Failed(JourneyKind kind, string detail) => new(kind, JourneyCertificationStatus.Failed, detail);
}
