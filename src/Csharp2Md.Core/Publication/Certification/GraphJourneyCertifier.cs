using Csharp2Md.Core.PackageBuilding;

namespace Csharp2Md.Core.Publication.Certification;

internal static class GraphJourneyCertifier
{
    internal static JourneyCertification CertifyFlow(MeasuredPackageReader reader, SolutionManifestEntry solution)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(solution);
        var dependencies = ReadDependencies(reader, solution);
        if (dependencies.IsDefaultOrEmpty) return NotApplicable(JourneyKind.FollowFlow, "no-causal-root");
        var categories = dependencies.Select(dependency => dependency.Category).ToHashSet();
        if (!categories.Overlaps([DependencyCategory.Http, DependencyCategory.Grpc, DependencyCategory.Messaging, DependencyCategory.Contract, DependencyCategory.Persistence]))
        {
            return NotApplicable(JourneyKind.FollowFlow, "no-causal-root");
        }
        var missing = new[]
        {
            (DependencyCategory.Contract, "contracts"),
            (DependencyCategory.Persistence, "persistence"),
        }.Where(required => !categories.Contains(required.Item1)).Select(required => required.Item2).ToArray();
        if (missing.Length > 0) return Failed(JourneyKind.FollowFlow, $"missing-terminal:{string.Join(',', missing)}");
        if (!categories.Overlaps([DependencyCategory.Http, DependencyCategory.Grpc, DependencyCategory.Messaging])) return Failed(JourneyKind.FollowFlow, "missing-terminal:external-effects");
        return Budget(JourneyKind.FollowFlow, reader.Measurement);
    }

    internal static JourneyCertification CertifyImpact(MeasuredPackageReader reader, SolutionManifestEntry solution)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(solution);
        var dependencies = ReadDependencies(reader, solution);
        if (dependencies.IsDefaultOrEmpty
            || !dependencies.Any(dependency => dependency.Category is DependencyCategory.Http or DependencyCategory.Grpc or DependencyCategory.Messaging or DependencyCategory.Contract or DependencyCategory.Persistence))
        {
            return NotApplicable(JourneyKind.ReverseImpact, "no-impact-root");
        }
        reader.BeginJourney();
        reader.OpenArtifact("manifest.json");
        reader.OpenArtifact(JourneyCertifier.Entry(solution, JourneyKind.ReverseImpact));
        var measuresPath = solution.Indexes.Single(index => index.Kind == NavigationIndexKind.Measures).EntryPath;
        var measures = CanonicalJson.Read<ImmutableArray<ScopeMeasures>>(reader.OpenArtifact(measuresPath).AsSpan());
        if (measures.IsDefaultOrEmpty) return NotApplicable(JourneyKind.ReverseImpact, "no-impact-root");
        if (measures.All(measure => measure.ReverseImpact.IsDefaultOrEmpty)) return Failed(JourneyKind.ReverseImpact, "missing-terminal:reachable-set");
        return Budget(JourneyKind.ReverseImpact, reader.Measurement);
    }

    private static ImmutableArray<AggregatedDependency> ReadDependencies(MeasuredPackageReader reader, SolutionManifestEntry solution)
    {
        reader.OpenArtifact("manifest.json");
        reader.OpenArtifact(JourneyCertifier.Entry(solution, JourneyKind.FollowFlow));
        foreach (var index in solution.Indexes.Where(index => index.Kind is NavigationIndexKind.Contracts or NavigationIndexKind.Persistence))
        {
            reader.OpenArtifact(index.EntryPath);
        }

        return CanonicalJson.Read<ImmutableArray<AggregatedDependency>>(
            reader.OpenArtifact(JourneyCertifier.Entry(solution, JourneyKind.FollowFlow)).AsSpan());
    }

    private static JourneyCertification Budget(JourneyKind kind, JourneyMeasurement measurement) =>
        measurement.Reads > 32 ? Failed(kind, $"reads-exceeded:{measurement.Reads}>32") :
        measurement.Tokens > 125_000 ? Failed(kind, $"tokens-exceeded:{measurement.Tokens}>125000") :
        new JourneyCertification(kind, JourneyCertificationStatus.Passed, $"reads:{measurement.Reads};bytes:{measurement.Bytes};tokens:{measurement.Tokens}");

    private static JourneyCertification NotApplicable(JourneyKind kind, string reason) => new(kind, JourneyCertificationStatus.NotApplicable, $"not_applicable:{reason}");
    private static JourneyCertification Failed(JourneyKind kind, string detail) => new(kind, JourneyCertificationStatus.Failed, detail);
}
