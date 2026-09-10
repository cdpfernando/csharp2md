using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Pipeline;

/// <summary>
/// Computes the four mandatory coverage metrics (GCPC-002, GCPC-003) and the run-certification status
/// (GCPC-001, GCPC-006..GCPC-010) from what earlier stages have already published to the accumulator --
/// never from a classifier pass's transient internal state, so every denominator member stays
/// individually enumerable from the same publication (GCPC-003). Replaces
/// <see cref="ValidationAndCoverageStub"/> at pipeline index 4.
/// </summary>
internal sealed class ValidationAndCoverageStage : IPipelineStage
{
    private const string ControllerBaseTypeName = "Microsoft.AspNetCore.Mvc.ControllerBase";
    private const string IntegrationEventHandlerTypeName = "IIntegrationEventHandler";
    private const string TargetTypeKey = "target-type";

    public string Name => "Validation and Coverage";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = context.Accumulator.ToSnapshot();

        var entryPointCoverage = ComputeEntryPointCoverage(snapshot);
        var linkedCallCoverage = ComputeLinkedCallCoverage(snapshot);

        // T28 replaces these two with real computation over the contract and persistence populations.
        var contractCoverage = CoverageMetric.NotApplicable(
            "Contract coverage is not yet computed by this pipeline stage.");
        var persistenceCoverage = CoverageMetric.NotApplicable(
            "Persistence coverage is not yet computed by this pipeline stage.");

        context.Accumulator.SetCoverage(
            new CoverageReport(entryPointCoverage, linkedCallCoverage, contractCoverage, persistenceCoverage));

        return StubStages.ZeroResult();
    }

    /// <summary>
    /// entry_point_coverage: the denominator is every reachable, non-constructor callable declared on a
    /// mechanically-recognized controller or handler type (the same recognition <c>EntryPointPass</c>
    /// applies before it decides whether entry capability is actually provable) -- "framework candidates
    /// mechanically recognizable in the analyzed variants" per quality-and-security.md. The numerator is
    /// the published <see cref="EntryPoint"/> facts; the unknowns are denominator members that did not
    /// become one (an undetermined owning component, published instead as an unresolved record by
    /// <c>EntryPointPass</c>).
    /// </summary>
    private static CoverageMetric ComputeEntryPointCoverage(FactualSnapshot snapshot)
    {
        var symbols = snapshot.Facts.OfType<Symbol>().ToArray();
        var types = symbols
            .Where(static symbol => string.Equals(ReadField(symbol.Signature.Value, "kind"), "namedtype", StringComparison.Ordinal))
            .ToArray();
        var methods = symbols
            .Where(static symbol =>
                string.Equals(ReadField(symbol.Signature.Value, "kind"), "method", StringComparison.Ordinal)
                && symbol.Facets.Facets.Contains(SymbolFacet.Callable)
                && ReadField(symbol.Signature.Value, "metadata") is not (".ctor" or ".cctor"))
            .ToArray();

        var observationsByOwner = snapshot.Observations.ToLookup(static o => o.Identity.Owner.Id.Value, StringComparer.Ordinal);

        var controllerTypes = new HashSet<string>(StringComparer.Ordinal);
        var handlerTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            var bases = observationsByOwner[type.Reference.Id.Value]
                .Where(static o => o.Identity.Kind is ObservationKind.BaseType)
                .ToArray();
            if (bases.Length == 0)
            {
                continue;
            }

            if (bases.Any(static o => PayloadContains(o, ControllerBaseTypeName))
                || methods.Any(method => IsDeclaredOn(method, type) && HasRouteDeclaration(observationsByOwner, method)))
            {
                controllerTypes.Add(type.Reference.Id.Value);
            }

            if (bases.Any(static o => PayloadContains(o, IntegrationEventHandlerTypeName))
                || methods.Any(method => IsDeclaredOn(method, type) && IsHandleAsync(method)))
            {
                handlerTypes.Add(type.Reference.Id.Value);
            }
        }

        var denominator = 0;
        foreach (var method in methods)
        {
            if (!method.Facets.Facets.Contains(SymbolFacet.ExternallyReachable))
            {
                continue;
            }

            var declaringType = types.FirstOrDefault(type => IsDeclaredOn(method, type));
            if (declaringType is null)
            {
                continue;
            }

            var isControllerAction = controllerTypes.Contains(declaringType.Reference.Id.Value);
            var isHandler = handlerTypes.Contains(declaringType.Reference.Id.Value) && IsHandleAsync(method);
            if (isControllerAction || isHandler)
            {
                denominator++;
            }
        }

        var numerator = snapshot.Facts.OfType<EntryPoint>().Count();
        var unknowns = Math.Max(0, denominator - numerator);

        return denominator == 0
            ? CoverageMetric.NotApplicable("No framework-recognizable entry-point candidates were found in the analyzed variants.")
            : CoverageMetric.Evaluated(numerator, denominator, exclusions: 0, unknowns);
    }

    /// <summary>
    /// linked_call_coverage: the denominator is every recognized invocation occurrence (an
    /// <see cref="ObservationKind.Invocation"/> or <see cref="ObservationKind.ObjectCreation"/>
    /// observation) -- "in-solution invocations semantically bindable to a destination" per
    /// quality-and-security.md. The numerator is occurrences backing a confirmed <c>invokes</c> relation;
    /// the unknowns are occurrences backing a candidate, unresolved or open-frontier record instead, each
    /// counted once even when an occurrence carries both an unresolved record and an open frontier
    /// (GCPC-014). Whatever is left over (occurrences with none of the four) is a justified exclusion --
    /// a framework call or a duplicate edge, neither of which leaves any other published trace.
    /// </summary>
    private static CoverageMetric ComputeLinkedCallCoverage(FactualSnapshot snapshot)
    {
        var denominatorSet = snapshot.Observations
            .Where(static o => o.Identity.Kind is ObservationKind.Invocation or ObservationKind.ObjectCreation)
            .Select(static o => o.Identity)
            .ToHashSet();

        var confirmedOccurrences = snapshot.ConfirmedRelations
            .Where(static r => r.Kind is RelationKind.Invokes)
            .SelectMany(static r => r.DerivedFrom.DerivedFrom)
            .Where(denominatorSet.Contains)
            .ToHashSet();

        var unknownOccurrences = snapshot.Candidates
            .Where(static c => c.Kind is RelationKind.Invokes)
            .SelectMany(static c => c.DerivedFrom.DerivedFrom)
            .Concat(snapshot.Unresolved
                .Where(static u => u.Kind is RelationKind.Invokes)
                .SelectMany(static u => u.Available.DerivedFrom))
            .Concat(snapshot.Frontiers.Select(static f => f.Occurrence))
            .Where(denominatorSet.Contains)
            .Where(id => !confirmedOccurrences.Contains(id))
            .ToHashSet();

        var denominator = denominatorSet.Count;
        var numerator = confirmedOccurrences.Count;
        var unknowns = unknownOccurrences.Count;
        var exclusions = Math.Max(0, denominator - numerator - unknowns);

        return denominator == 0
            ? CoverageMetric.NotApplicable("No in-solution invocation occurrences were recognized.")
            : CoverageMetric.Evaluated(numerator, denominator, exclusions, unknowns);
    }

    private static bool IsHandleAsync(Symbol method) =>
        string.Equals(ReadField(method.Signature.Value, "metadata"), "HandleAsync", StringComparison.Ordinal);

    private static bool HasRouteDeclaration(ILookup<string, Observation> observationsByOwner, Symbol method) =>
        observationsByOwner[method.Reference.Id.Value].Any(static o => o.Identity.Kind is ObservationKind.RouteDeclaration);

    private static bool IsDeclaredOn(Symbol method, Symbol type)
    {
        var container = ReadField(method.Signature.Value, "container");
        if (container is null)
        {
            return false;
        }

        var typeName = ReadField(type.Signature.Value, "type");
        if (string.Equals(container, typeName, StringComparison.Ordinal))
        {
            return true;
        }

        var typeContainer = ReadField(type.Signature.Value, "container");
        var typeMetadata = ReadField(type.Signature.Value, "metadata");
        return typeContainer is not null
            && typeMetadata is not null
            && string.Equals(container, typeContainer + "." + typeMetadata, StringComparison.Ordinal);
    }

    private static bool PayloadContains(Observation observation, string needle)
    {
        foreach (var entry in observation.Identity.Payload.Entries)
        {
            if (string.Equals(entry.Key, TargetTypeKey, StringComparison.Ordinal)
                && entry.Value.Value.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadField(string identity, string key)
    {
        var marker = ";" + key + "=";
        var start = identity.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = identity.IndexOf(';', start);
        var encoded = end < 0 ? identity[start..] : identity[start..end];
        return encoded.Length == 0 || encoded == "-" ? null : Uri.UnescapeDataString(encoded);
    }
}
