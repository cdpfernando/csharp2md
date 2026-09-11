using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

public static class DomainMapper
{
    public static WireDocument ToWire(FactualSnapshot snapshot, ManifestContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);

        var solutions = ImmutableArray.CreateBuilder<SolutionDto>();
        var projects = ImmutableArray.CreateBuilder<ProjectDto>();
        var documents = ImmutableArray.CreateBuilder<DocumentDto>();
        var symbols = ImmutableArray.CreateBuilder<SymbolDto>();
        var components = ImmutableArray.CreateBuilder<ComponentDto>();
        var deploymentUnits = ImmutableArray.CreateBuilder<DeploymentUnitDto>();
        var entryPoints = ImmutableArray.CreateBuilder<EntryPointDto>();
        var boundaryOperations = ImmutableArray.CreateBuilder<BoundaryOperationDto>();
        var externalSystems = ImmutableArray.CreateBuilder<ExternalSystemDto>();
        var contracts = ImmutableArray.CreateBuilder<ContractDto>();
        var contractBindings = ImmutableArray.CreateBuilder<ContractBindingDto>();
        var contractRevisions = ImmutableArray.CreateBuilder<ContractRevisionDto>();
        var dataStores = ImmutableArray.CreateBuilder<DataStoreDto>();
        var dataObjects = ImmutableArray.CreateBuilder<DataObjectDto>();
        var dataFields = ImmutableArray.CreateBuilder<DataFieldDto>();
        var dataOperations = ImmutableArray.CreateBuilder<DataOperationDto>();
        var configurationBindings = ImmutableArray.CreateBuilder<ConfigurationBindingDto>();

        foreach (var fact in snapshot.Facts)
        {
            switch (fact)
            {
                case Solution solution:
                    solutions.Add(WireFactMapping.ToDto(solution));
                    break;
                case Project project:
                    projects.Add(WireFactMapping.ToDto(project));
                    break;
                case Document document:
                    documents.Add(WireFactMapping.ToDto(document));
                    break;
                case Symbol symbol:
                    symbols.Add(WireFactMapping.ToDto(symbol));
                    break;
                case Component component:
                    components.Add(WireFactMapping.ToDto(component));
                    break;
                case DeploymentUnit deploymentUnit:
                    deploymentUnits.Add(WireFactMapping.ToDto(deploymentUnit));
                    break;
                case EntryPoint entryPoint:
                    entryPoints.Add(WireFactMapping.ToDto(entryPoint));
                    break;
                case BoundaryOperation boundaryOperation:
                    boundaryOperations.Add(WireFactMapping.ToDto(boundaryOperation));
                    break;
                case ExternalSystem externalSystem:
                    externalSystems.Add(WireFactMapping.ToDto(externalSystem));
                    break;
                case Contract contract:
                    contracts.Add(WireFactMapping.ToDto(contract));
                    break;
                case ContractBinding contractBinding:
                    contractBindings.Add(WireFactMapping.ToDto(contractBinding));
                    break;
                case ContractRevision contractRevision:
                    contractRevisions.Add(WireFactMapping.ToDto(contractRevision));
                    break;
                case DataStore dataStore:
                    dataStores.Add(WireFactMapping.ToDto(dataStore));
                    break;
                case DataObject dataObject:
                    dataObjects.Add(WireFactMapping.ToDto(dataObject));
                    break;
                case DataField dataField:
                    dataFields.Add(WireFactMapping.ToDto(dataField));
                    break;
                case DataOperation dataOperation:
                    dataOperations.Add(WireFactMapping.ToDto(dataOperation));
                    break;
                case ConfigurationBinding configurationBinding:
                    configurationBindings.Add(WireFactMapping.ToDto(configurationBinding));
                    break;
                default:
                    throw new NotSupportedException($"Fact type '{fact.GetType().Name}' has no wire mapping.");
            }
        }

        var observationDtos = snapshot.Observations
            .GroupBy(observation => WireObservationMapping.WireName(observation.Identity.Kind))
            .ToImmutableDictionary(
                group => group.Key,
                group => Ordered(
                    group.Select(WireObservationMapping.ToDto),
                    static dto => $"{dto.Identity.Owner.Id}:{dto.Identity.Kind}:{dto.Identity.OccurrenceOrdinal}:{dto.ContentSha256}"));

        var confirmedDtos = snapshot.ConfirmedRelations
            .GroupBy(relation => WireRelationMapping.WireName(relation.Kind))
            .ToImmutableDictionary(
                group => group.Key,
                group => Ordered(
                    group.Select(WireRelationMapping.ToDto),
                    static dto => $"{dto.Kind}:{dto.Source.Id}:{dto.Target.Id}:{dto.ContentSha256}"));

        var candidateDtos = Ordered(
            snapshot.Candidates.Select(WireRelationMapping.ToDto),
            static dto => $"{dto.Kind}:{dto.Source.Id}:{dto.ProposedTarget.Id}");
        var unresolvedDtos = Ordered(
            snapshot.Unresolved.Select(WireRelationMapping.ToDto),
            static dto => $"{dto.Kind}:{dto.Source.Id}:{dto.Cause}");
        var frontierDtos = Ordered(
            snapshot.Frontiers.Select(WireRelationMapping.ToDto),
            static dto => $"{dto.Occurrence.Owner.Id}:{dto.Occurrence.Kind}:{dto.Occurrence.OccurrenceOrdinal}");

        var versions = TaxonomyTables.Default.Versions;
        return new WireDocument(
            new ManifestEnvelope(
                versions.SchemaVersion,
                versions.TaxonomyVersion,
                versions.ObservationSchemaVersion,
                context.SolutionKey,
                context.SolutionFileName,
                []),
            ReadEmbeddedRegistry(),
            Ordered(solutions, static dto => dto.Identity.Id),
            Ordered(projects, static dto => dto.Identity.Id),
            Ordered(documents, static dto => dto.Identity.Id),
            Ordered(symbols, static dto => dto.Identity.Id),
            Ordered(components, static dto => dto.Identity.Id),
            Ordered(deploymentUnits, static dto => dto.Identity.Id),
            Ordered(entryPoints, static dto => dto.Identity.Id),
            Ordered(boundaryOperations, static dto => dto.Identity.Id),
            Ordered(externalSystems, static dto => dto.Identity.Id),
            Ordered(contracts, static dto => dto.Identity.Id),
            Ordered(contractBindings, static dto => dto.Identity.Id),
            Ordered(contractRevisions, static dto => dto.Identity.Id),
            Ordered(dataStores, static dto => dto.Identity.Id),
            Ordered(dataObjects, static dto => dto.Identity.Id),
            Ordered(dataFields, static dto => dto.Identity.Id),
            Ordered(dataOperations, static dto => dto.Identity.Id),
            Ordered(configurationBindings, static dto => dto.Identity.Id),
            observationDtos,
            confirmedDtos,
            candidateDtos,
            unresolvedDtos,
            frontierDtos,
            [],
            MapCoverage(snapshot.Coverage),
            MapCertification(snapshot.Certification),
            new DiagnosticsEnvelope(MapDiagnostics(snapshot)),
            new MeasurementsEnvelope([]));
    }

    public static FactualSnapshot FromWire(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var facts = ImmutableArray.CreateBuilder<IFact>();
        facts.AddRange(document.Solutions.Select(WireFactMapping.FromDto));
        facts.AddRange(document.Projects.Select(WireFactMapping.FromDto));
        facts.AddRange(document.Documents.Select(WireFactMapping.FromDto));
        facts.AddRange(document.Symbols.Select(WireFactMapping.FromDto));
        facts.AddRange(document.Components.Select(WireFactMapping.FromDto));
        facts.AddRange(document.DeploymentUnits.Select(WireFactMapping.FromDto));
        facts.AddRange(document.EntryPoints.Select(WireFactMapping.FromDto));
        facts.AddRange(document.BoundaryOperations.Select(WireFactMapping.FromDto));
        facts.AddRange(document.ExternalSystems.Select(WireFactMapping.FromDto));
        facts.AddRange(document.Contracts.Select(WireFactMapping.FromDto));
        facts.AddRange(document.ContractBindings.Select(WireFactMapping.FromDto));
        facts.AddRange(document.ContractRevisions.Select(WireFactMapping.FromDto));
        facts.AddRange(document.DataStores.Select(WireFactMapping.FromDto));
        facts.AddRange(document.DataObjects.Select(WireFactMapping.FromDto));
        facts.AddRange(document.DataFields.Select(WireFactMapping.FromDto));
        facts.AddRange(document.DataOperations.Select(WireFactMapping.FromDto));
        facts.AddRange(document.ConfigurationBindings.Select(WireFactMapping.FromDto));

        var factsArr = facts.ToImmutable();
        var factsById = factsArr
            .GroupBy(static fact => fact.Reference.Id.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var observationsArr = document.Observations.Values
            .SelectMany(records => records.Select(WireObservationMapping.FromDto))
            .ToImmutableArray();
        var confirmedArr = document.ConfirmedRelations.Values
            .SelectMany(records => records.Select(dto => WireRelationMapping.FromDto(dto, factsById)))
            .ToImmutableArray();
        var candidatesArr = document.Candidates.Select(WireRelationMapping.FromDto).ToImmutableArray();
        var unresolvedArr = document.Unresolved.Select(WireRelationMapping.FromDto).ToImmutableArray();
        var frontiersArr = document.Frontiers.Select(WireRelationMapping.FromDto).ToImmutableArray();

        if (factsArr.IsEmpty
            && observationsArr.IsEmpty
            && confirmedArr.IsEmpty
            && candidatesArr.IsEmpty
            && unresolvedArr.IsEmpty
            && frontiersArr.IsEmpty)
        {
            return FactualSnapshot.Empty;
        }

        return new FactualSnapshot(
            factsArr,
            observationsArr,
            confirmedArr,
            candidatesArr,
            unresolvedArr,
            frontiersArr);
    }

    /// <summary>
    /// Maps the computed coverage report (GCPC-002) onto the wire envelope. A missing report means the
    /// snapshot never reached <c>ValidationAndCoverageStage</c> (a hand-built snapshot in a test, for
    /// example, rather than a genuine analysis run) -- each metric then publishes its own
    /// <c>not_applicable</c> reason instead of a fabricated ratio, exactly as GCPC-009 requires when
    /// nothing was actually evaluated.
    /// </summary>
    private static CoverageEnvelope MapCoverage(CoverageReport? coverage) =>
        coverage is null
            ? new CoverageEnvelope(
                UnevaluatedMetric, UnevaluatedMetric, UnevaluatedMetric, UnevaluatedMetric)
            : new CoverageEnvelope(
                ToDto(coverage.EntryPointCoverage),
                ToDto(coverage.LinkedCallCoverage),
                ToDto(coverage.ContractCoverage),
                ToDto(coverage.PersistenceCoverage));

    private static readonly CoverageMetricDto UnevaluatedMetric =
        CoverageMetricDto.NotApplicable("Coverage was not computed for this snapshot.");

    /// <summary>
    /// Merges layout-time degradation reasons (GCPC-004) -- e.g. a record <see cref="LayoutPlanner"/>
    /// could not reduce to fit the publication ceiling and published in a shard of its own instead --
    /// onto the coverage metric each reason's family feeds (<see cref="LayoutPlan.CoverageMetricDegradations"/>),
    /// keeping any reasons the metric already carried. A metric absent from <paramref name="byMetric"/> is
    /// returned unchanged. <see cref="PublicationPipeline"/> calls this after planning so a real
    /// degradation is never silently dropped from <c>coverage.json</c>.
    /// </summary>
    internal static CoverageEnvelope WithCoverageDegradations(
        CoverageEnvelope coverage,
        ImmutableDictionary<CoverageMetricKind, ImmutableArray<DegradationReasonDto>> byMetric)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(byMetric);

        if (byMetric.IsEmpty)
        {
            return coverage;
        }

        return coverage with
        {
            EntryPointCoverage = AppendDegradations(coverage.EntryPointCoverage, byMetric, CoverageMetricKind.EntryPoint),
            LinkedCallCoverage = AppendDegradations(coverage.LinkedCallCoverage, byMetric, CoverageMetricKind.LinkedCall),
            ContractCoverage = AppendDegradations(coverage.ContractCoverage, byMetric, CoverageMetricKind.Contract),
            PersistenceCoverage = AppendDegradations(coverage.PersistenceCoverage, byMetric, CoverageMetricKind.Persistence),
        };
    }

    private static CoverageMetricDto AppendDegradations(
        CoverageMetricDto metric,
        ImmutableDictionary<CoverageMetricKind, ImmutableArray<DegradationReasonDto>> byMetric,
        CoverageMetricKind kind)
    {
        if (!byMetric.TryGetValue(kind, out var additional) || additional.IsEmpty)
        {
            return metric;
        }

        var merged = Ordered(
            metric.DegradationReasons.AddRange(additional),
            static dto => $"{dto.Code}:{dto.Detail}");

        return metric.NotApplicableReason is not null
            ? CoverageMetricDto.NotApplicable(metric.NotApplicableReason, merged)
            : CoverageMetricDto.Evaluated(metric.Numerator, metric.Denominator, metric.Exclusions, metric.Unknowns, merged);
    }

    private static CoverageMetricDto ToDto(CoverageMetric metric)
    {
        var degradationReasons = Ordered(
            metric.DegradationReasons.Select(static reason => new DegradationReasonDto(reason.Code, reason.Detail, reason.AffectedCount)),
            static dto => $"{dto.Code}:{dto.Detail}");

        return metric.State == CoverageMetricState.NotApplicable
            ? CoverageMetricDto.NotApplicable(metric.NotApplicableReason!, degradationReasons)
            : CoverageMetricDto.Evaluated(metric.Numerator, metric.Denominator, metric.Exclusions, metric.Unknowns, degradationReasons);
    }

    /// <summary>
    /// Maps the computed run-certification report (GCPC-001, GCPC-006..GCPC-010) onto the wire envelope.
    /// A missing report (a snapshot that never reached <c>ValidationAndCoverageStage</c>) publishes
    /// <c>degraded</c> rather than a fabricated <c>passed</c> -- <c>not_evaluated</c> is not a status
    /// this envelope can construct at all (GCPC-001).
    /// </summary>
    private static RunCertificationEnvelope MapCertification(RunCertificationReport? certification) =>
        certification is null
            ? new RunCertificationEnvelope("degraded", ["Run certification was not computed for this snapshot."])
            : new RunCertificationEnvelope(WireStatus(certification.Status), certification.Reasons);

    private static string WireStatus(RunCertificationStatus status) => status switch
    {
        RunCertificationStatus.Passed => "passed",
        RunCertificationStatus.Degraded => "degraded",
        RunCertificationStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, $"'{status}' is not a defined run-certification status."),
    };

    private static ImmutableArray<DiagnosticRecordDto> MapDiagnostics(FactualSnapshot snapshot)
    {
        var named = Ordered(
            snapshot.Diagnostics.Select(static record =>
                new DiagnosticRecordDto(record.Code, record.Message, record.IdentityOrKey)),
            static dto => $"{dto.Code}:{dto.IdentityOrKey}:{dto.Message}");
        var secrets = Ordered(
            snapshot.SuspectedSecrets.Select(ToSuspectedSecretDto),
            static dto => $"{dto.IdentityOrKey}:{dto.Message}");
        return named.AddRange(secrets);
    }

    private static DiagnosticRecordDto ToSuspectedSecretDto(SuspectedSecretEvidence evidence) =>
        new(
            "suspected-secret",
            $"{evidence.Span.StartLine}:{evidence.Span.StartColumn}-{evidence.Span.EndLine}:{evidence.Span.EndColumn} {evidence.Excerpt.Value}",
            evidence.Document.Value);

    private static ImmutableArray<T> Ordered<T>(IEnumerable<T> items, Func<T, string> identity) =>
        items.OrderBy(identity, StringComparer.Ordinal).ToImmutableArray();

    private static ImmutableArray<byte> ReadEmbeddedRegistry()
    {
        var assembly = typeof(DomainMapper).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("taxonomy-registry.json", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray().ToImmutableArray();
    }
}
