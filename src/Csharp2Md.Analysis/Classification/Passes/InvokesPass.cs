using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class InvokesPass : IClassifierPass
{
    private static readonly FacetBinding EmptyFacets =
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    internal static ClassifierIdentity Identity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.invokes", 1);

    public string Name => "Invokes";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken) =>
        Execute(context, cancellationToken, out _);

    /// <summary>
    /// Same as <see cref="Execute(ClassifierContext, CancellationToken)"/>, additionally exposing the
    /// per-occurrence <see cref="InvocationDispositionLedger"/> (GCPC-011) built while classifying, for
    /// tests that need to prove every recognized invocation occurrence carries exactly one disposition.
    /// </summary>
    internal ClassifierPassResult Execute(
        ClassifierContext context, CancellationToken cancellationToken, out InvocationDispositionLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        ledger = new InvocationDispositionLedger();

        var symbols = context.FactsByType<Symbol>();
        var symbolsById = symbols.ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var symbolsBySig = symbols
            .GroupBy(static symbol => symbol.Signature.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var symbolByProjectAndSig = symbols
            .GroupBy(
                static symbol => symbol.OwningProject.Value + "\u001f" + symbol.Signature.Value,
                StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var fallbackByProject = symbols
            .GroupBy(static symbol => symbol.OwningProject.Value, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(static symbol => symbol.Signature.Value, StringComparer.Ordinal).First().Reference.Id.Value,
                StringComparer.Ordinal);

        var confirmed = new HashSet<(RelationKind Kind, string Source, string Target)>();
        var candidateKeys = new HashSet<(RelationKind Kind, string Source, string Target)>();
        var frontiered = new HashSet<ObservationIdentity>();
        var relationCount = 0;
        var candidateCount = 0;
        var unresolvedCount = 0;

        var observations = context.ObservationsByKind(ObservationKind.Invocation)
            .Concat(context.ObservationsByKind(ObservationKind.ObjectCreation))
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Identity.Kind)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal);

        foreach (var observation in observations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evidence = EvidenceChain.Create([observation.Identity]);
            if (!symbolsById.TryGetValue(observation.Identity.Owner.Id.Value, out var owner)
                || !owner.Facets.Facets.Contains(SymbolFacet.Callable))
            {
                // GCPC-011: the occurrence's own owner is not a recognized callable, so the occurrence
                // is unresolved rather than silently vanishing.
                unresolvedCount += AddUnresolved(
                    context,
                    observation.Identity.Owner,
                    UnresolvedCause.InsufficientEvidence,
                    evidence);
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Unresolved));
                continue;
            }

            if (fallbackByProject.TryGetValue(owner.OwningProject.Value, out var fallbackId)
                && string.Equals(owner.Reference.Id.Value, fallbackId, StringComparison.Ordinal))
            {
                unresolvedCount += AddUnresolved(
                    context,
                    owner.Reference,
                    UnresolvedCause.InsufficientEvidence,
                    evidence);
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Unresolved));
                continue;
            }

            var targetSignature = AlwaysWhenBindableWalker.TryExtractTargetSignature(observation.Diagnostic.Message);
            if (targetSignature is null)
            {
                if (string.Equals(observation.Diagnostic.Code, "unbound", StringComparison.Ordinal))
                {
                    unresolvedCount += AddUnresolved(
                        context,
                        owner.Reference,
                        UnresolvedCause.NoCandidateFound,
                        evidence);
                    AddFrontier(context, frontiered, observation.Identity);
                }
                else
                {
                    // GCPC-011: bound, but no target signature could be extracted at all -- previously
                    // silent; now unresolved instead of dropped.
                    unresolvedCount += AddUnresolved(
                        context,
                        owner.Reference,
                        UnresolvedCause.InsufficientEvidence,
                        evidence);
                }

                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Unresolved));
                continue;
            }

            if (IsReflectionDispatch(targetSignature) || IsDelegateInvoke(targetSignature))
            {
                unresolvedCount += AddUnresolved(
                    context,
                    owner.Reference,
                    UnresolvedCause.NoCandidateFound,
                    evidence);
                AddFrontier(context, frontiered, observation.Identity);
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Unresolved));
                continue;
            }

            if (IsFrameworkSignature(targetSignature))
            {
                // GCPC-016: a binding outside the analyzed solution scope is a counted exclusion, not a
                // per-occurrence diagnostic and not silence.
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationExclusionCategory.ExternalFrameworkCallable));
                continue;
            }

            var matches = ResolveTargets(owner, targetSignature, symbolsBySig, symbolByProjectAndSig);
            if (matches.Length == 0)
            {
                if (IsTypeParameterContainer(targetSignature))
                {
                    var named = ConcreteCallablesNamed(symbols, ReadField(targetSignature, "metadata"));
                    if (named.Length > 0)
                    {
                        candidateCount += AddCandidates(context, candidateKeys, owner, named, evidence);
                        AddFrontier(context, frontiered, observation.Identity);
                        ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Candidate));
                        continue;
                    }
                }

                unresolvedCount += AddUnresolved(
                    context,
                    owner.Reference,
                    UnresolvedCause.NoCandidateFound,
                    evidence);
                AddFrontier(context, frontiered, observation.Identity);
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Unresolved));
                continue;
            }

            if (matches.Length > 1 && matches.All(static match => !match.Facets.Facets.Contains(SymbolFacet.Abstract)))
            {
                candidateCount += AddCandidates(context, candidateKeys, owner, matches, evidence);
                AddFrontier(context, frontiered, observation.Identity);
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Candidate));
                continue;
            }

            var target = matches[0];
            if (target.Facets.Facets.Contains(SymbolFacet.Abstract))
            {
                var implementors = ConcreteImplementors(symbols, target);
                if (implementors.Length == 0)
                {
                    unresolvedCount += AddUnresolved(
                        context,
                        owner.Reference,
                        UnresolvedCause.NoCandidateFound,
                        evidence);
                    AddFrontier(context, frontiered, observation.Identity);
                    ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Unresolved));
                    continue;
                }

                candidateCount += AddCandidates(context, candidateKeys, owner, implementors, evidence);
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Candidate));
                continue;
            }

            if (!target.Facets.Facets.Contains(SymbolFacet.Callable)
                || context.AnalysisVariants.IsDefaultOrEmpty)
            {
                // GCPC-011: a bound target that is not itself a recognized callable (or no analysis
                // variant to attach evidence to) cannot be confirmed, so it is unresolved instead of
                // silently dropped.
                unresolvedCount += AddUnresolved(
                    context,
                    owner.Reference,
                    UnresolvedCause.InsufficientEvidence,
                    evidence);
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Unresolved));
                continue;
            }

            var key = (RelationKind.Invokes, owner.Reference.Id.Value, target.Reference.Id.Value);
            if (!confirmed.Add(key))
            {
                // GCPC-016: a duplicate occurrence of an already-confirmed edge is a counted exclusion,
                // not silence -- the edge itself was already published for an earlier occurrence.
                ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationExclusionCategory.DuplicateEdge));
                continue;
            }

            context.Accumulator.AddRelation(
                ConfirmedRelation.Create(
                    RelationKind.Invokes,
                    owner.Reference,
                    target.Reference,
                    EmptyFacets,
                    evidence,
                    Identity,
                    context.AnalysisVariants,
                    EvidenceMethod.Semantic,
                    sourceFact: owner));
            relationCount++;
            ledger.Add(InvocationDisposition.Create(observation.Identity, InvocationDispositionKind.Confirmed));
        }

        return new ClassifierPassResult(0, relationCount, candidateCount, unresolvedCount);
    }

    private static Symbol[] ResolveTargets(
        Symbol owner,
        string targetSignature,
        IReadOnlyDictionary<string, Symbol[]> symbolsBySig,
        IReadOnlyDictionary<string, Symbol> symbolByProjectAndSig)
    {
        var projectKey = owner.OwningProject.Value + "\u001f" + targetSignature;
        if (symbolByProjectAndSig.TryGetValue(projectKey, out var sameProject))
        {
            return [sameProject];
        }

        return symbolsBySig.TryGetValue(targetSignature, out var matches) ? matches : [];
    }

    private static int AddCandidates(
        ClassifierContext context,
        HashSet<(RelationKind Kind, string Source, string Target)> keys,
        Symbol owner,
        IEnumerable<Symbol> targets,
        EvidenceChain evidence)
    {
        var count = 0;
        foreach (var target in targets.OrderBy(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal))
        {
            var key = (RelationKind.Invokes, owner.Reference.Id.Value, target.Reference.Id.Value);
            if (!keys.Add(key))
            {
                continue;
            }

            context.Accumulator.AddCandidate(
                CandidateLink.Create(RelationKind.Invokes, owner.Reference, target.Reference, evidence));
            count++;
        }

        return count;
    }

    private static int AddUnresolved(
        ClassifierContext context,
        FactReference source,
        UnresolvedCause cause,
        EvidenceChain evidence)
    {
        context.Accumulator.AddUnresolved(UnresolvedRecord.Create(RelationKind.Invokes, source, cause, evidence));
        return 1;
    }

    private static void AddFrontier(
        ClassifierContext context,
        HashSet<ObservationIdentity> frontiered,
        ObservationIdentity occurrence)
    {
        if (!frontiered.Add(occurrence))
        {
            return;
        }

        context.Accumulator.AddOpenFrontier(
            OpenFrontier.Create(occurrence, FrontierCause.FurtherContinuationObserved));
    }

    private static Symbol[] ConcreteImplementors(ImmutableArray<Symbol> symbols, Symbol abstractTarget)
    {
        var metadata = ReadField(abstractTarget.Signature.Value, "metadata");
        var parameters = ReadField(abstractTarget.Signature.Value, "parameters");
        var arity = ReadField(abstractTarget.Signature.Value, "arity");
        return [.. symbols
            .Where(candidate =>
                candidate.Facets.Facets.Contains(SymbolFacet.Callable)
                && !candidate.Facets.Facets.Contains(SymbolFacet.Abstract)
                && !candidate.Reference.Equals(abstractTarget.Reference)
                && string.Equals(ReadField(candidate.Signature.Value, "metadata"), metadata, StringComparison.Ordinal)
                && string.Equals(ReadField(candidate.Signature.Value, "parameters"), parameters, StringComparison.Ordinal)
                && string.Equals(ReadField(candidate.Signature.Value, "arity"), arity, StringComparison.Ordinal))
            .OrderBy(static candidate => candidate.Reference.Id.Value, StringComparer.Ordinal)];
    }

    private static Symbol[] ConcreteCallablesNamed(ImmutableArray<Symbol> symbols, string? metadata) =>
        metadata is null
            ? []
            : [.. symbols
                .Where(candidate =>
                    candidate.Facets.Facets.Contains(SymbolFacet.Callable)
                    && !candidate.Facets.Facets.Contains(SymbolFacet.Abstract)
                    && string.Equals(ReadField(candidate.Signature.Value, "metadata"), metadata, StringComparison.Ordinal))
                .OrderBy(static candidate => candidate.Reference.Id.Value, StringComparer.Ordinal)];

    private static bool IsFrameworkSignature(string signature)
    {
        var container = ReadField(signature, "container");
        return container is not null
            && (container.StartsWith("global::System.", StringComparison.Ordinal)
                || container.StartsWith("global::Microsoft.", StringComparison.Ordinal))
            && !IsReflectionDispatch(signature)
            && !IsDelegateInvoke(signature);
    }

    private static bool IsReflectionDispatch(string signature)
    {
        var container = ReadField(signature, "container");
        return container is not null
            && container.StartsWith("global::System.Reflection", StringComparison.Ordinal);
    }

    private static bool IsDelegateInvoke(string signature)
    {
        if (!string.Equals(ReadField(signature, "metadata"), "Invoke", StringComparison.Ordinal))
        {
            return false;
        }

        var container = ReadField(signature, "container");
        return container is not null && IsDelegateContainer(container);
    }

    private static bool IsDelegateContainer(string container) =>
        container.StartsWith("global::System.Action", StringComparison.Ordinal)
        || container.StartsWith("global::System.Func", StringComparison.Ordinal)
        || container.StartsWith("global::System.Predicate", StringComparison.Ordinal)
        || container.StartsWith("global::System.EventHandler", StringComparison.Ordinal)
        || container.StartsWith("global::System.Comparison", StringComparison.Ordinal)
        || container.StartsWith("global::System.Converter", StringComparison.Ordinal);

    private static bool IsTypeParameterContainer(string signature)
    {
        var container = ReadField(signature, "container");
        return container is not null
            && !container.StartsWith("global::", StringComparison.Ordinal)
            && !container.Contains('.', StringComparison.Ordinal);
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
