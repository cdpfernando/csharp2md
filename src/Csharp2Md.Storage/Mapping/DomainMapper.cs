using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

public static class DomainMapper
{
    private static readonly CoverageMetricDto ZeroCoverage = new(0, 0, 0, 0, []);

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

        var versions = TaxonomyVersions.Initial;
        var artifacts = BuildManifestArtifacts(
            structural: solutions.Count + projects.Count + documents.Count + symbols.Count,
            architecture: components.Count + deploymentUnits.Count + entryPoints.Count + boundaryOperations.Count + externalSystems.Count,
            contract: contracts.Count + contractBindings.Count + contractRevisions.Count,
            persistence: dataStores.Count + dataObjects.Count + dataFields.Count + dataOperations.Count,
            configuration: configurationBindings.Count);

        return new WireDocument(
            new ManifestEnvelope(
                versions.SchemaVersion,
                versions.TaxonomyVersion,
                versions.ObservationSchemaVersion,
                context.SolutionKey,
                context.SolutionFileName,
                artifacts),
            ReadEmbeddedRegistry(),
            solutions.ToImmutable(),
            projects.ToImmutable(),
            documents.ToImmutable(),
            symbols.ToImmutable(),
            components.ToImmutable(),
            deploymentUnits.ToImmutable(),
            entryPoints.ToImmutable(),
            boundaryOperations.ToImmutable(),
            externalSystems.ToImmutable(),
            contracts.ToImmutable(),
            contractBindings.ToImmutable(),
            contractRevisions.ToImmutable(),
            dataStores.ToImmutable(),
            dataObjects.ToImmutable(),
            dataFields.ToImmutable(),
            dataOperations.ToImmutable(),
            configurationBindings.ToImmutable(),
            ImmutableDictionary<string, ImmutableArray<ObservationDto>>.Empty,
            ImmutableDictionary<string, ImmutableArray<ConfirmedRelationDto>>.Empty,
            [],
            [],
            [],
            [],
            new CoverageEnvelope(ZeroCoverage, ZeroCoverage, ZeroCoverage, ZeroCoverage),
            new RunCertificationEnvelope("not_evaluated"),
            new DiagnosticsEnvelope([]),
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

        return new FactualSnapshot(
            facts.ToImmutable(),
            [],
            [],
            [],
            [],
            []);
    }

    private static ImmutableArray<ManifestEntry> BuildManifestArtifacts(
        int structural,
        int architecture,
        int contract,
        int persistence,
        int configuration)
    {
        var artifacts = ImmutableArray.CreateBuilder<ManifestEntry>();
        AddFamily(artifacts, FactFamily.Structural, structural);
        AddFamily(artifacts, FactFamily.Architecture, architecture);
        AddFamily(artifacts, FactFamily.Contract, contract);
        AddFamily(artifacts, FactFamily.Persistence, persistence);
        AddFamily(artifacts, FactFamily.Configuration, configuration);

        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            artifacts.Add(new ManifestEntry(
                "observations/" + kind.WireName,
                "payload",
                0,
                "observations/" + kind.WireName + ".json"));
        }

        foreach (var relation in TaxonomyTables.Default.Relations)
        {
            artifacts.Add(new ManifestEntry(
                "relations/confirmed/" + relation.WireName,
                "payload",
                0,
                "relations/confirmed/" + relation.WireName + ".json"));
        }

        artifacts.Add(new ManifestEntry("relations/candidates", "payload", 0, "relations/candidates.json"));
        artifacts.Add(new ManifestEntry("relations/unresolved", "payload", 0, "relations/unresolved.json"));
        artifacts.Add(new ManifestEntry("relations/frontiers", "payload", 0, "relations/frontiers.json"));

        return artifacts.ToImmutable();
    }

    private static void AddFamily(ImmutableArray<ManifestEntry>.Builder artifacts, FactFamily family, int count)
    {
        var segment = family.ToString().ToLowerInvariant();
        artifacts.Add(new ManifestEntry(
            "facts/" + segment,
            "payload",
            count,
            "facts/" + segment + ".json"));
    }

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
