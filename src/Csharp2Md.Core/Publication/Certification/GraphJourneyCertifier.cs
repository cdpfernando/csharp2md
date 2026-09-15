using Csharp2Md.Core.PackageBuilding;

namespace Csharp2Md.Core.Publication.Certification;

internal static class GraphJourneyCertifier
{
    internal static ImmutableArray<JourneyCertification> Certify(MeasuredPackageReader reader) =>
    [
        CertifyFlow(reader),
        CertifyImpact(reader),
    ];

    private static JourneyCertification CertifyFlow(MeasuredPackageReader reader)
    {
        var dependencies = ReadDependencies(reader, JourneyKind.FollowFlow);
        if (dependencies.IsDefaultOrEmpty) return NotApplicable(JourneyKind.FollowFlow, "no-causal-root");
        var categories = dependencies.Select(dependency => dependency.Category).ToHashSet();
        var missing = new[]
        {
            (DependencyCategory.Contract, "contracts"),
            (DependencyCategory.Persistence, "persistence"),
        }.Where(required => !categories.Contains(required.Item1)).Select(required => required.Item2).ToArray();
        if (missing.Length > 0) return Failed(JourneyKind.FollowFlow, $"missing-terminal:{string.Join(',', missing)}");
        if (!categories.Overlaps([DependencyCategory.Http, DependencyCategory.Grpc, DependencyCategory.Messaging])) return Failed(JourneyKind.FollowFlow, "missing-terminal:external-effects");
        return Budget(JourneyKind.FollowFlow, reader.Measurement);
    }

    private static JourneyCertification CertifyImpact(MeasuredPackageReader reader)
    {
        reader.OpenArtifact("manifest.json");
        reader.OpenArtifact(Entry(reader.Manifest, JourneyKind.ReverseImpact));
        var measuresPath = reader.Manifest.Indexes.Single(index => index.Name == "measures").Path;
        var measures = CanonicalJson.Read<ImmutableArray<ScopeMeasures>>(reader.OpenArtifact(measuresPath).AsSpan());
        if (measures.IsDefaultOrEmpty) return NotApplicable(JourneyKind.ReverseImpact, "no-impact-root");
        if (measures.All(measure => measure.ReverseImpact.IsDefaultOrEmpty)) return Failed(JourneyKind.ReverseImpact, "missing-terminal:reachable-set");
        return Budget(JourneyKind.ReverseImpact, reader.Measurement);
    }

    private static ImmutableArray<AggregatedDependency> ReadDependencies(MeasuredPackageReader reader, JourneyKind kind)
    {
        reader.OpenArtifact("manifest.json");
        reader.OpenArtifact(Entry(reader.Manifest, kind));
        foreach (var index in reader.Manifest.Indexes.Where(index => index.Name is "contracts" or "persistence")) reader.OpenArtifact(index.Path);
        return CanonicalJson.Read<ImmutableArray<AggregatedDependency>>(reader.OpenArtifact(Entry(reader.Manifest, JourneyKind.FollowFlow)).AsSpan());
    }

    private static string Entry(PackageManifest manifest, JourneyKind kind) => manifest.Journeys.Single(journey => journey.Kind == kind).EntryPath;
    private static JourneyCertification Budget(JourneyKind kind, JourneyMeasurement measurement) => measurement.Reads > 32 ? Failed(kind, $"reads-exceeded:{measurement.Reads}>32") : measurement.Tokens > 125_000 ? Failed(kind, $"tokens-exceeded:{measurement.Tokens}>125000") : new JourneyCertification(kind, JourneyCertificationStatus.Passed, $"reads:{measurement.Reads};bytes:{measurement.Bytes};tokens:{measurement.Tokens}");
    private static JourneyCertification NotApplicable(JourneyKind kind, string reason) => new(kind, JourneyCertificationStatus.NotApplicable, $"not_applicable:{reason}");
    private static JourneyCertification Failed(JourneyKind kind, string detail) => new(kind, JourneyCertificationStatus.Failed, detail);
}
