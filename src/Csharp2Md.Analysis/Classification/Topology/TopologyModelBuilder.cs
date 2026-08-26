using Csharp2Md.Analysis.Classification;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Classification.Topology;

internal static class TopologyModelBuilder
{
    private const string OutputKindKey = "output-kind";
    private const string ProjectReferenceKey = "project-reference";
    private const string ApplicationKind = "application";
    private const string LibraryKind = "library";
    private const string PathMarker = ";path=";

    public static TopologyModel Build(ClassifierContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var projects = context.FactsByType<Project>()
            .ToDictionary(static project => project.Reference.Id.Value, StringComparer.Ordinal);
        var byPath = new Dictionary<string, Project>(StringComparer.Ordinal);
        foreach (var project in projects.Values)
        {
            var path = LogicalPath(project.Id);
            if (path is not null)
            {
                byPath[path] = project;
            }
        }

        var outputKind = new Dictionary<string, Observation>(StringComparer.Ordinal);
        var edges = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var edgeEvidence = new Dictionary<(string From, string To), ObservationIdentity>();

        foreach (var observation in context.ObservationsByKind(ObservationKind.Configuration))
        {
            if (!projects.TryGetValue(observation.Identity.Owner.Id.Value, out var owner)
                || LogicalPath(owner.Id) is not { } ownerPath)
            {
                continue;
            }

            var kind = PayloadReader.Value(observation, OutputKindKey);
            if (kind is ApplicationKind or LibraryKind)
            {
                outputKind[ownerPath] = observation;
            }

            var referenced = PayloadReader.Value(observation, ProjectReferenceKey);
            if (referenced is null || !byPath.ContainsKey(referenced))
            {
                continue;
            }

            if (!edges.TryGetValue(ownerPath, out var targets))
            {
                targets = new SortedSet<string>(StringComparer.Ordinal);
                edges[ownerPath] = targets;
            }

            if (targets.Add(referenced))
            {
                edgeEvidence[(ownerPath, referenced)] = observation.Identity;
            }
        }

        if (outputKind.Count == 0)
        {
            return new TopologyModel([], [], [], [], new TopologyCoverage(0, 0, 0));
        }

        var applications = outputKind
            .Where(static pair => PayloadReader.Value(pair.Value, OutputKindKey) == ApplicationKind)
            .Select(static pair => pair.Key)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var libraries = outputKind
            .Where(static pair => PayloadReader.Value(pair.Value, OutputKindKey) == LibraryKind)
            .Select(static pair => pair.Key)
            .ToArray();

        var reaching = ComputeReach(applications, edges);
        return Group(
            applications,
            libraries,
            byPath,
            outputKind,
            edges,
            edgeEvidence,
            reaching);
    }

    private static Dictionary<string, SortedSet<string>> ComputeReach(
        IReadOnlyList<string> applications,
        IReadOnlyDictionary<string, SortedSet<string>> edges)
    {
        var reaching = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var application in applications)
        {
            Walk(application, application, edges, reaching, []);
        }

        return reaching;
    }

    private static void Walk(
        string node,
        string application,
        IReadOnlyDictionary<string, SortedSet<string>> edges,
        Dictionary<string, SortedSet<string>> reaching,
        HashSet<string> stack)
    {
        if (!stack.Add(node))
        {
            return;
        }

        if (!reaching.TryGetValue(node, out var apps))
        {
            apps = new SortedSet<string>(StringComparer.Ordinal);
            reaching[node] = apps;
        }

        apps.Add(application);
        if (edges.TryGetValue(node, out var targets))
        {
            foreach (var target in targets)
            {
                Walk(target, application, edges, reaching, stack);
            }
        }

        stack.Remove(node);
    }

    private static TopologyModel Group(
        IReadOnlyList<string> applications,
        IReadOnlyList<string> libraries,
        IReadOnlyDictionary<string, Project> byPath,
        IReadOnlyDictionary<string, Observation> outputKind,
        IReadOnlyDictionary<string, SortedSet<string>> edges,
        IReadOnlyDictionary<(string From, string To), ObservationIdentity> edgeEvidence,
        IReadOnlyDictionary<string, SortedSet<string>> reaching)
    {
        var privateUse = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var groups = new List<ComponentGroup>();
        var unreached = new List<UnreachedComponent>();
        foreach (var application in applications)
        {
            privateUse[application] = [];
        }

        foreach (var library in libraries.OrderBy(static path => path, StringComparer.Ordinal))
        {
            reaching.TryGetValue(library, out var apps);
            var count = apps?.Count ?? 0;
            if (count == 1)
            {
                privateUse[apps!.Min!].Add(library);
                continue;
            }

            var evidence = count == 0 ? GroupingEvidence.Unreached : GroupingEvidence.Shared;
            groups.Add(new ComponentGroup(library, evidence, [byPath[library].Id]));
            if (count == 0)
            {
                unreached.Add(new UnreachedComponent(library, Chain(outputKind[library].Identity)));
            }
        }

        foreach (var application in applications)
        {
            var members = new List<ProjectId> { byPath[application].Id };
            members.AddRange(
                privateUse[application]
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .Select(path => byPath[path].Id));
            groups.Add(new ComponentGroup(
                application,
                GroupingEvidence.Deployable,
                [.. members.OrderBy(static id => id.Value, StringComparer.Ordinal)]));
        }

        var orderedGroups = groups
            .OrderBy(static group => group.ComponentName, StringComparer.Ordinal)
            .ToImmutableArray();
        var deployments = applications
            .Select(path => new DeploymentNode(path, byPath[path].Id))
            .OrderBy(static node => node.Name, StringComparer.Ordinal)
            .ToImmutableArray();
        var inclusions = BuildInclusions(
            orderedGroups,
            applications,
            privateUse,
            reaching,
            outputKind,
            edges,
            edgeEvidence)
            .OrderBy(static edge => edge.ComponentName, StringComparer.Ordinal)
            .ThenBy(static edge => edge.DeploymentName, StringComparer.Ordinal)
            .ToImmutableArray();
        var orderedUnreached = unreached
            .OrderBy(static node => node.ComponentName, StringComparer.Ordinal)
            .ToImmutableArray();
        var grouped = orderedGroups.SelectMany(static group => group.Projects).Distinct().Count();
        return new TopologyModel(
            orderedGroups,
            deployments,
            inclusions,
            orderedUnreached,
            new TopologyCoverage(grouped, applications.Count, orderedUnreached.Length));
    }

    private static List<InclusionEdge> BuildInclusions(
        ImmutableArray<ComponentGroup> groups,
        IReadOnlyList<string> applications,
        IReadOnlyDictionary<string, List<string>> privateUse,
        IReadOnlyDictionary<string, SortedSet<string>> reaching,
        IReadOnlyDictionary<string, Observation> outputKind,
        IReadOnlyDictionary<string, SortedSet<string>> edges,
        IReadOnlyDictionary<(string From, string To), ObservationIdentity> edgeEvidence)
    {
        var inclusions = new List<InclusionEdge>();
        foreach (var group in groups)
        {
            if (group.Evidence is GroupingEvidence.Unreached)
            {
                continue;
            }

            IEnumerable<string> reachingApps = group.Evidence is GroupingEvidence.Deployable
                ? [group.ComponentName]
                : reaching.TryGetValue(group.ComponentName, out var apps)
                    ? apps
                    : [];

            foreach (var application in reachingApps.OrderBy(static path => path, StringComparer.Ordinal))
            {
                if (!applications.Contains(application, StringComparer.Ordinal))
                {
                    continue;
                }

                var identities = ProveReach(
                    application,
                    group,
                    privateUse,
                    outputKind,
                    edges,
                    edgeEvidence);
                inclusions.Add(new InclusionEdge(group.ComponentName, application, EvidenceChain.Create(identities)));
            }
        }

        return inclusions;
    }

    private static List<ObservationIdentity> ProveReach(
        string application,
        ComponentGroup group,
        IReadOnlyDictionary<string, List<string>> privateUse,
        IReadOnlyDictionary<string, Observation> outputKind,
        IReadOnlyDictionary<string, SortedSet<string>> edges,
        IReadOnlyDictionary<(string From, string To), ObservationIdentity> edgeEvidence)
    {
        var identities = new List<ObservationIdentity> { outputKind[application].Identity };
        var targets = new HashSet<string>(StringComparer.Ordinal) { group.ComponentName };
        if (privateUse.TryGetValue(application, out var grouped)
            && string.Equals(group.ComponentName, application, StringComparison.Ordinal))
        {
            foreach (var library in grouped)
            {
                targets.Add(library);
            }
        }

        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { application };
        var queue = new Queue<string>();
        queue.Enqueue(application);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!edges.TryGetValue(node, out var nexts))
            {
                continue;
            }

            foreach (var next in nexts)
            {
                if (!seen.Add(next))
                {
                    continue;
                }

                parent[next] = node;
                queue.Enqueue(next);
            }
        }

        foreach (var target in targets)
        {
            var node = target;
            while (parent.TryGetValue(node, out var from))
            {
                if (edgeEvidence.TryGetValue((from, node), out var identity))
                {
                    identities.Add(identity);
                }

                node = from;
            }
        }

        return identities;
    }

    private static EvidenceChain Chain(ObservationIdentity identity) => EvidenceChain.Create([identity]);

    private static string? LogicalPath(ProjectId projectId)
    {
        var id = projectId.Value;
        var start = id.IndexOf(PathMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += PathMarker.Length;
        var end = id.IndexOf(';', start);
        var encoded = end < 0 ? id[start..] : id[start..end];
        return string.IsNullOrWhiteSpace(encoded) ? null : Uri.UnescapeDataString(encoded);
    }
}
