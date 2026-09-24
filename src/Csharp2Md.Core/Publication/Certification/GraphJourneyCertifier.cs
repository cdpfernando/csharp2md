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
        var contracts = ReadIndex(reader, solution, NavigationIndexKind.Contracts);
        var persistence = ReadIndex(reader, solution, NavigationIndexKind.Persistence);
        var categories = outgoing.Entries.SelectMany(entry => entry.Categories).ToHashSet();
        // Persistence is a terminal of the flow, never its root: a solution whose only causal facts are
        // persistence has no flow to follow, so the journey is not applicable rather than incomplete.
        if (!categories.Overlaps([DependencyCategory.Http, DependencyCategory.Grpc, DependencyCategory.Messaging, DependencyCategory.Contract]))
        {
            return NotApplicable(JourneyKind.FollowFlow, "no-causal-root");
        }
        // A terminal the retained graph records but its index cannot reach is a navigation defect of the
        // package, so it fails first and is never masked by another terminal the corpus simply lacks.
        var unreachable = new[]
        {
            (DependencyCategory.Contract, contracts, "contracts"),
            (DependencyCategory.Persistence, persistence, "persistence"),
        }.Where(terminal => categories.Contains(terminal.Item1) && !Reaches(terminal.Item2, terminal.Item1))
            .Select(terminal => terminal.Item3).ToArray();
        if (unreachable.Length > 0) return Failed(JourneyKind.FollowFlow, $"missing-terminal:{string.Join(',', unreachable)}");
        // A terminal the retained graph never records is a fact the corpus does not contain, not a defect of
        // the package: the journey has nothing to navigate to, so it is not applicable for that terminal.
        var absent = new List<string>(3);
        if (!categories.Contains(DependencyCategory.Contract)) absent.Add("contracts");
        if (!categories.Contains(DependencyCategory.Persistence)) absent.Add("persistence");
        if (!categories.Overlaps([DependencyCategory.Http, DependencyCategory.Grpc, DependencyCategory.Messaging])) absent.Add("external-effects");
        if (absent.Count > 0) return NotApplicable(JourneyKind.FollowFlow, $"absent-terminal:{string.Join(',', absent)}");
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

    private static bool Reaches(NavigationIndexData index, DependencyCategory category) =>
        !index.Entries.IsDefaultOrEmpty
        && index.Entries.Any(entry => !entry.Categories.IsDefaultOrEmpty && entry.Categories.Contains(category) && !entry.Ordinals.IsDefaultOrEmpty);

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
