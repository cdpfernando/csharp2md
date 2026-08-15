namespace Csharp2Md.Core.Graph;

/// <summary>
/// Turns the signals accumulated in Stage 2 into service-to-service edges, correlating messaging
/// half-edges into directed publisher-to-subscriber edges (P2-14) and retaining the unpaired ones
/// rather than dropping them (P2-15).
/// </summary>
/// <remarks>
/// <para>
/// <b>AD-005.</b> A non-messaging signal's target is its already-resolved <c>RawTarget</c> wrapped
/// as a <see cref="ServiceName"/>; it is deliberately <b>not</b> cross-matched against the
/// <c>ServiceCatalog</c>. No name/address to catalog matching rule exists in spec.md or design.md,
/// and none is mechanically possible today: no data model stores a service's own network address,
/// so a hard-coded URL has nothing to match against. This is the same treatment P2-15 already
/// requires for an unpaired messaging signal, where the topic name stands in as the target.
/// </para>
/// <para>
/// Because that decision leaves nothing for a catalog to do here, the <c>ServiceCatalog</c>
/// parameter in design.md's <c>Build(signals, catalog)</c> sketch is omitted rather than accepted
/// and ignored — a parameter no code reads is dead API surface that implies a correlation step
/// this component does not perform.
/// </para>
/// </remarks>
public static class GraphBuilder
{
    public static DependencyGraph Build(IReadOnlyList<DependencySignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var edges = new List<DependencyEdge>();
        var messaging = new List<DependencySignal>();

        foreach (var signal in signals)
        {
            if (signal.Kind is DependencyKind.Messaging)
            {
                messaging.Add(signal);
            }
            else
            {
                edges.Add(PassThrough(signal));
            }
        }

        edges.AddRange(Correlate(messaging));

        return new DependencyGraph(Merge(edges));
    }

    /// <summary>
    /// A non-messaging signal is already a whole edge: the detector knew both ends at detection
    /// time. Target and classification pass through untouched.
    /// </summary>
    private static DependencyEdge PassThrough(DependencySignal signal) =>
        new(signal.SourceService,
            signal.TargetService ?? new ServiceName(signal.RawTarget),
            signal.Communication,
            signal.Resolution,
            [signal.Location]);

    private static List<DependencyEdge> Correlate(List<DependencySignal> messaging)
    {
        var edges = new List<DependencyEdge>();

        foreach (var topic in messaging.GroupBy(signal => signal.RawTarget, StringComparer.Ordinal))
        {
            var publishers = topic.Where(signal => signal.Role is MessagingRole.Publish).ToList();
            var subscribers = topic.Where(signal => signal.Role is MessagingRole.Subscribe).ToList();

            foreach (var signal in topic)
            {
                List<DependencySignal> pool = signal.Role switch
                {
                    MessagingRole.Publish => subscribers,
                    MessagingRole.Subscribe => publishers,
                    _ => [],
                };

                // P2-14 pairs a publish in one service with a subscribe in *another*, so a service
                // that publishes and subscribes to the same topic is not its own counterpart.
                var counterparts = pool
                    .Where(counterpart => counterpart.SourceService != signal.SourceService)
                    .ToList();

                if (counterparts.Count == 0)
                {
                    // P2-15: retained, never dropped. The topic name stands in as the target and the
                    // signal's own Unresolved classification carries over unchanged.
                    edges.Add(new DependencyEdge(
                        signal.SourceService,
                        new ServiceName(signal.RawTarget),
                        signal.Communication,
                        signal.Resolution,
                        [signal.Location]));
                    continue;
                }

                // Only the publish half emits the edge. The subscribe half sees the same pairing
                // from the other side and would emit a duplicate of it.
                if (signal.Role is not MessagingRole.Publish)
                {
                    continue;
                }

                edges.AddRange(counterparts.Select(subscriber => new DependencyEdge(
                    signal.SourceService,
                    subscriber.SourceService,
                    CommunicationType.PubSubEvento,
                    // Spec-precision gap: P2-07/08/09 classify how a *logical name resolved against
                    // config*, and a correlated pub/sub edge never went through config — it was
                    // matched on topic name. NotApplicable, for the same reason T18 uses it for a
                    // compile-time reference. Unresolved would be wrong: this edge is resolved.
                    ResolutionKind.NotApplicable,
                    [signal.Location, subscriber.Location])));
            }
        }

        return edges;
    }

    /// <summary>
    /// Collapses edges that describe the same relationship into one, aggregating their evidence,
    /// and orders the result so downstream artifacts (dependencies.json, the Mermaid diagram) are
    /// byte-stable across runs rather than dependent on document visit order.
    /// </summary>
    private static IReadOnlyList<DependencyEdge> Merge(List<DependencyEdge> edges) =>
        edges
            .GroupBy(edge => (edge.Source, edge.Target, edge.Communication, edge.Resolution))
            .Select(group => new DependencyEdge(
                group.Key.Source,
                group.Key.Target,
                group.Key.Communication,
                group.Key.Resolution,
                group.SelectMany(edge => edge.Evidence).Distinct().ToList()))
            .OrderBy(edge => edge.Source.Value, StringComparer.Ordinal)
            .ThenBy(edge => edge.Target.Value, StringComparer.Ordinal)
            .ThenBy(edge => edge.Communication)
            .ThenBy(edge => edge.Resolution)
            .ToList();
}
