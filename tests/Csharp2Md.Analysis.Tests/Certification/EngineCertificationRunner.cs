using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Fixtures;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Certification;

/// <summary>
/// GCPC-077, GCPC-080: compares real classifier output, produced by actually analyzing the fixture
/// solutions, against the ground truth authored in T53's labeled corpora. <see cref="Resolve"/> never
/// reads a label's <see cref="LabeledCorpusEntry.Expected"/> value -- it only inspects the published
/// facts and relations a real <c>analyze</c> run produced, so the comparison this runner performs can
/// actually disagree with the label, which is the whole point of measuring precision and recall against
/// independent ground truth rather than restating classifier output back at itself.
///
/// Not product surface (D-03): nothing under <c>src/</c> references this type or the labeled corpora.
/// </summary>
internal static class EngineCertificationRunner
{
    public static async Task<AreaCertificationResult> RunAsync(CertifiedArea area, CancellationToken cancellationToken = default)
    {
        var entries = LabeledCorpusReader.Read(area);

        // Labeled items can cite either fixture tree (persistence and one contract label reuse
        // SyntheticSolution, since CertificationCorpus carries no persistence shape and no genuinely
        // cross-project contract shape) -- analyze each distinct solution the entries actually need
        // exactly once, keyed by solution path.
        var publicationsBySolution = new Dictionary<string, CommittedPublication>(StringComparer.Ordinal);
        foreach (var solutionPath in entries.Select(entry => SolutionPathFor(entry.SourceFile)).Distinct(StringComparer.Ordinal))
        {
            publicationsBySolution[solutionPath] = await AnalyzeAsync(solutionPath, cancellationToken);
        }

        var measurements = entries
            .Select(entry => new LabelMeasurement(
                entry,
                Resolve(area, entry.Id, publicationsBySolution[SolutionPathFor(entry.SourceFile)])))
            .ToImmutableArray();

        return new AreaCertificationResult(area, measurements);
    }

    internal static readonly string SyntheticAcmeOrdersSolutionPath = Path.Combine(
        AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");

    /// <summary>Which fixture solution a labeled item's <c>sourceFile</c> belongs to, by its second path segment.</summary>
    private static string SolutionPathFor(string sourceFile)
    {
        var root = sourceFile.Split('/', StringSplitOptions.None) is [_, var segment, ..] ? segment : null;
        return root switch
        {
            "CertificationCorpus" => CertificationCorpusPaths.SolutionPath,
            "SyntheticSolution" => SyntheticAcmeOrdersSolutionPath,
            _ => throw new InvalidOperationException($"No fixture solution is registered for source file '{sourceFile}'."),
        };
    }

    internal static async Task<CommittedPublication> AnalyzeAsync(string solutionPath, CancellationToken cancellationToken)
    {
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            cancellationToken);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return publication;
    }

    private static ExpectedState Resolve(CertifiedArea area, string labelId, CommittedPublication publication) => area switch
    {
        CertifiedArea.EntryPoint => ResolveEntryPoint(labelId, publication),
        CertifiedArea.LinkedCall => ResolveLinkedCall(labelId, publication),
        CertifiedArea.Contract => ResolveContract(labelId, publication),
        CertifiedArea.Persistence => ResolvePersistence(labelId, publication),
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, null),
    };

    private static ExpectedState ResolveEntryPoint(string labelId, CommittedPublication publication)
    {
        var architecture = ReadShard<ArchitectureFactsShard>(publication, "facts/architecture.json");
        return labelId switch
        {
            "entry-getwidget" => HasEntryPoint(architecture, "GetWidget", "WidgetsController"),
            "entry-index" => HasEntryPoint(architecture, "Index", "WidgetsController"),
            "entry-changeuriplaceholder" => HasEntryPoint(architecture, "ChangeUriPlaceholder", "WidgetsController"),
            "entry-orderqueries-getorderstatus" => HasEntryPoint(architecture, "GetOrderStatus", "Certification.Queries"),
            _ => throw UnknownLabel(labelId),
        };
    }

    private static ExpectedState HasEntryPoint(ArchitectureFactsShard architecture, string symbol, string context) =>
        architecture.EntryPoints.Any(entry =>
            entry.Symbol.Id.Contains(symbol, StringComparison.Ordinal)
            && entry.Symbol.Id.Contains(context, StringComparison.Ordinal))
            ? ExpectedState.Present
            : ExpectedState.Absent;

    private static ExpectedState ResolveLinkedCall(string labelId, CommittedPublication publication)
    {
        var confirmed = ReadOptionalArray<ConfirmedRelationDto>(publication, "relations/confirmed/invokes.json");
        var candidates = ReadOptionalArray<CandidateLinkDto>(publication, "relations/candidates.json");
        var unresolved = ReadOptionalArray<UnresolvedRecordDto>(publication, "relations/unresolved.json");

        return labelId switch
        {
            "call-getwidget-changeuriplaceholder" => ResolveInvokes(confirmed, candidates, unresolved, "ChangeUriPlaceholder", null),
            "call-getorderstatus-ok-notfound" => ResolveInvokes(confirmed, candidates, unresolved, "NotFound", null),
            "call-getorderstatus-iorderqueries" => ResolveInvokes(confirmed, candidates, unresolved, "GetOrderStatus", "Certification.Queries"),
            _ => throw UnknownLabel(labelId),
        };
    }

    private static ExpectedState ResolveInvokes(
        ImmutableArray<ConfirmedRelationDto> confirmed,
        ImmutableArray<CandidateLinkDto> candidates,
        ImmutableArray<UnresolvedRecordDto> unresolved,
        string targetSymbol,
        string? targetContext)
    {
        bool Matches(string id) =>
            id.Contains(targetSymbol, StringComparison.Ordinal)
            && (targetContext is null || id.Contains(targetContext, StringComparison.Ordinal));

        if (confirmed.Any(relation => relation.Kind == "invokes" && Matches(relation.Target.Id)))
        {
            return ExpectedState.Present;
        }

        if (candidates.Any(link => link.Kind == "invokes" && Matches(link.ProposedTarget.Id))
            || unresolved.Any(record => record.Kind == "invokes" && Matches(record.Source.Id)))
        {
            return ExpectedState.Unresolved;
        }

        return ExpectedState.Absent;
    }

    private static ExpectedState ResolveContract(string labelId, CommittedPublication publication)
    {
        var contracts = ReadOptionalContracts(publication);
        var messageOperations = ReadOptionalArray<ObservationDto>(publication, "observations/message-operation.json");

        var eventTypeName = labelId switch
        {
            "contract-orderplaced" => "OrderPlaced",
            "contract-ordershipped" => "OrderShipped",
            "contract-receipt-not-merged" => "Receipt",
            _ => throw UnknownLabel(labelId),
        };

        if (contracts.Any(contract => contract.Proof.Value.Contains(eventTypeName, StringComparison.Ordinal)))
        {
            return ExpectedState.Present;
        }

        var isRecognizedMessageOperation = messageOperations.Any(observation =>
            observation.Identity.Payload.Any(entry =>
                entry.Key == "type-argument" && entry.Value.Value.Contains(eventTypeName, StringComparison.Ordinal)));

        return isRecognizedMessageOperation ? ExpectedState.Unresolved : ExpectedState.Absent;
    }

    private static ExpectedState ResolvePersistence(string labelId, CommittedPublication publication)
    {
        var accessesData = ReadOptionalArray<ConfirmedRelationDto>(publication, "relations/confirmed/accesses-data.json");
        var unresolved = ReadOptionalArray<UnresolvedRecordDto>(publication, "relations/unresolved.json");

        var symbol = labelId switch
        {
            "persist-selectorder" => "SelectOrder",
            "persist-insertorder" => "InsertOrder",
            "persist-updateorderstatus" => "UpdateOrderStatus",
            "persist-rebuildtotals" => "RebuildTotals",
            "persist-deletearchived" => "DeleteArchived",
            "persist-selectallfrom" => "SelectAllFrom",
            "persist-connection" => "Connection",
            _ => throw UnknownLabel(labelId),
        };

        if (accessesData.Any(relation =>
            relation.Kind == "accesses-data" && relation.Source.Id.Contains(symbol, StringComparison.Ordinal)))
        {
            return ExpectedState.Present;
        }

        if (unresolved.Any(record => record.Source.Id.Contains(symbol, StringComparison.Ordinal)))
        {
            return ExpectedState.Unresolved;
        }

        return ExpectedState.Absent;
    }

    private static InvalidOperationException UnknownLabel(string labelId) =>
        new($"No classifier-output resolver is registered for labeled item '{labelId}'.");

    /// <summary>
    /// "facts/contract.json" is only planned when at least one Contract fact exists (F6-adjacent
    /// behavior for optional families) -- a solution with zero contracts, like the certification
    /// corpus today, publishes no such artifact at all, so a plain <see cref="ReadShard{T}"/> would
    /// wrongly throw instead of reporting "no contracts".
    /// </summary>
    private static ImmutableArray<ContractDto> ReadOptionalContracts(CommittedPublication publication)
    {
        var fragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == "facts/contract.json");
        return fragment is null
            ? []
            : CanonicalJson.Read<ContractFactsShard>(fragment.Payload.AsSpan()).Contracts;
    }

    private static T ReadShard<T>(CommittedPublication publication, string canonicalKey) =>
        CanonicalJson.Read<T>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == canonicalKey).Payload.AsSpan());

    /// <summary>
    /// Reads a flat record-array family, merging its shards back into one array when the derived
    /// ceiling (T52) split it -- a plain unsplit-key lookup silently finds nothing once a family
    /// shards, which is exactly the bug this helper avoids. Mirrors
    /// <c>CertificationCorpusEntryPointTests.ReadShardedArray</c>.
    /// </summary>
    private static ImmutableArray<T> ReadOptionalArray<T>(CommittedPublication publication, string canonicalKey)
    {
        var stem = canonicalKey.EndsWith(".json", StringComparison.Ordinal)
            ? canonicalKey[..^".json".Length]
            : canonicalKey;
        var shardKeys = publication.ArtifactsInPublicationOrder
            .Select(static artifact => artifact.CanonicalKey)
            .Where(key => key == canonicalKey
                || (key.StartsWith(stem + ".", StringComparison.Ordinal) && key.EndsWith(".json", StringComparison.Ordinal)))
            .OrderBy(static key => key, StringComparer.Ordinal);

        var records = ImmutableArray.CreateBuilder<T>();
        foreach (var key in shardKeys)
        {
            var fragment = publication.ArtifactsInPublicationOrder.Single(artifact => artifact.CanonicalKey == key);
            records.AddRange(CanonicalJson.Read<ImmutableArray<T>>(fragment.Payload.AsSpan()));
        }

        return records.ToImmutable();
    }
}

/// <summary>One labeled item compared against the classifier output actually observed for it.</summary>
internal sealed record LabelMeasurement(LabeledCorpusEntry Entry, ExpectedState Actual)
{
    /// <summary>The label and the observed classifier output agree on all three states, not only on presence.</summary>
    public bool IsExactMatch => Actual == Entry.Expected;

    /// <summary>The classifier correctly confirmed a construct ground truth says must be confirmed.</summary>
    public bool IsTruePositive => Entry.Expected == ExpectedState.Present && Actual == ExpectedState.Present;

    /// <summary>Ground truth says the classifier must confirm this, but it did not (a recall miss).</summary>
    public bool IsFalseNegative => Entry.Expected == ExpectedState.Present && Actual != ExpectedState.Present;

    /// <summary>Ground truth says the classifier must not confirm this, but it did (a precision miss).</summary>
    public bool IsFalsePositive => Entry.Expected != ExpectedState.Present && Actual == ExpectedState.Present;
}

/// <summary>
/// Precision and recall for one certified area, computed from the label files' expected states against
/// the classifier output <see cref="EngineCertificationRunner.Resolve"/> actually observed -- never read
/// back from anything the classifier itself published (GCPC-077, GCPC-080).
/// </summary>
internal sealed record AreaCertificationResult(CertifiedArea Area, ImmutableArray<LabelMeasurement> Measurements)
{
    public int TruePositives => Measurements.Count(static m => m.IsTruePositive);

    public int FalsePositives => Measurements.Count(static m => m.IsFalsePositive);

    public int FalseNegatives => Measurements.Count(static m => m.IsFalseNegative);

    /// <summary>Of everything the classifier confirmed among labeled items, the share ground truth agrees with.</summary>
    public double Precision => TruePositives + FalsePositives == 0
        ? 1.0
        : (double)TruePositives / (TruePositives + FalsePositives);

    /// <summary>Of everything ground truth says must be confirmed, the share the classifier actually confirmed.</summary>
    public double Recall => TruePositives + FalseNegatives == 0
        ? 1.0
        : (double)TruePositives / (TruePositives + FalseNegatives);

    /// <summary>Every item whose classifier output disagreed with ground truth on confirmation (GCPC-079).</summary>
    public ImmutableArray<LabeledCorpusEntry> FailingItems => Measurements
        .Where(static m => m.IsFalsePositive || m.IsFalseNegative)
        .Select(static m => m.Entry)
        .ToImmutableArray();
}
