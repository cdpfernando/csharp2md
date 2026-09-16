using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;

namespace Csharp2Md.Core.Publication.Certification;

internal static class GraphJourneyCertifier
{
    internal static JourneyCertification CertifyFlow(MeasuredPackageReader reader, SolutionManifestEntry solution)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(solution);
        reader.OpenArtifact("manifest.json");
        var outgoing = ReadIndex(reader, solution, NavigationIndexKind.Outgoing);
        ReadIndex(reader, solution, NavigationIndexKind.Contracts);
        ReadIndex(reader, solution, NavigationIndexKind.Persistence);
        var categories = outgoing.Entries.SelectMany(entry => entry.Categories).ToHashSet();
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
        reader.OpenArtifact("manifest.json");
        var incoming = ReadIndex(reader, solution, NavigationIndexKind.Incoming);
        if (!incoming.Entries.SelectMany(entry => entry.Categories)
            .Any(category => category is DependencyCategory.Http or DependencyCategory.Grpc or DependencyCategory.Messaging or DependencyCategory.Contract or DependencyCategory.Persistence))
        {
            return NotApplicable(JourneyKind.ReverseImpact, "no-impact-root");
        }
        var measures = ReadIndex(reader, solution, NavigationIndexKind.Measures);
        if (measures.Entries.IsDefaultOrEmpty) return NotApplicable(JourneyKind.ReverseImpact, "no-impact-root");
        if (measures.Entries.All(entry => !entry.HasReachableSet)) return Failed(JourneyKind.ReverseImpact, "missing-terminal:reachable-set");
        return Budget(JourneyKind.ReverseImpact, reader.Measurement);
    }

    private static NavigationIndexData ReadIndex(MeasuredPackageReader reader, SolutionManifestEntry solution, NavigationIndexKind kind)
    {
        var path = solution.Indexes.Single(index => index.Kind == kind).EntryPath;
        return CanonicalJson.Read<NavigationIndexData>(reader.OpenArtifact(path).AsSpan());
    }

    private static JourneyCertification Budget(JourneyKind kind, JourneyMeasurement measurement) =>
        measurement.Reads > 32 ? Failed(kind, $"reads-exceeded:{measurement.Reads}>32") :
        measurement.Tokens > 125_000 ? Failed(kind, $"tokens-exceeded:{measurement.Tokens}>125000") :
        new JourneyCertification(kind, JourneyCertificationStatus.Passed, $"reads:{measurement.Reads};bytes:{measurement.Bytes};tokens:{measurement.Tokens}");

    private static JourneyCertification NotApplicable(JourneyKind kind, string reason) => new(kind, JourneyCertificationStatus.NotApplicable, $"not_applicable:{reason}");
    private static JourneyCertification Failed(JourneyKind kind, string detail) => new(kind, JourneyCertificationStatus.Failed, detail);
}
