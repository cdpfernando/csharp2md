using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Validation;

public sealed record ValidationReport(WireDocument Document, ImmutableArray<QuarantineRecordDto> Quarantine);

public static class PackageValidator
{
    private static readonly FrozenSet<string> FactTypeNames = TaxonomyTables.Default.FactTypes
        .Select(descriptor => descriptor.Name)
        .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ObservationKindNames = TaxonomyTables.Default.ObservationKinds
        .Select(descriptor => descriptor.WireName)
        .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> RelationKindNames = TaxonomyTables.Default.Relations
        .Select(descriptor => descriptor.WireName)
        .ToFrozenSet(StringComparer.Ordinal);

    internal static readonly FrozenSet<string> UnixFilesystemRoots = FrozenSet.ToFrozenSet(
        [
            "home",
            "usr",
            "opt",
            "var",
            "etc",
            "tmp",
            "root",
            "dev",
            "proc",
            "sys",
            "mnt",
            "media",
            "users",
            "private",
            "volumes",
        ],
        StringComparer.OrdinalIgnoreCase);

    public static ValidationReport Validate(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureRegisteredKinds(document);
        EnsureUniqueFactIdentities(document);
        EnsureContentHashes(document);
        EnsureNoAbsolutePaths(document);
        EnsureStructuralConstruction(document);
        return QuarantineInvalidDerived(document);
    }

    public static T ReadPayloadOrThrow<T>(ReadOnlySpan<byte> utf8, string artifactKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactKey);

        try
        {
            return CanonicalJson.Read<T>(utf8);
        }
        catch (JsonException)
        {
            throw new PublicationRejectedException("schema", artifactKey);
        }
    }

    /// <summary>
    /// Checks a manifest against the artifact bytes it describes: every declared entry exists with the
    /// declared count and byte size (GCPC-061), and every artifact is declared -- nothing is reachable
    /// that the manifest does not name (GCPC-062). A deferred artifact (a raw source copy, published with
    /// a placeholder zero count and size because its bytes cannot be read twice -- see
    /// <c>ManifestBuilder</c>) is skipped rather than compared, since there is nothing genuine to compare
    /// it against. Also checks the manifest's own provenance for compatibility with the running generator
    /// (GCPC-071's rejection reason; the exit-code mapping itself is Phase 9's CLI work).
    /// </summary>
    public static void ValidatePublishedManifest(
        ManifestEnvelope manifest,
        IReadOnlyDictionary<string, ImmutableArray<byte>> artifactsByKey,
        IReadOnlySet<string>? deferredKeys = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(artifactsByKey);
        deferredKeys ??= FrozenSet<string>.Empty;

        foreach (var entry in manifest.Artifacts)
        {
            if (deferredKeys.Contains(entry.Path))
            {
                continue;
            }

            if (!artifactsByKey.TryGetValue(entry.Path, out var bytes))
            {
                throw new PublicationRejectedException("manifest-file-missing", entry.Path);
            }

            if (bytes.Length != entry.ByteSize)
            {
                throw new PublicationRejectedException(
                    "manifest-size-mismatch",
                    $"{entry.Path}: manifest declares {entry.ByteSize} bytes, the artifact is {bytes.Length} bytes.");
            }

            // The taxonomy registry is one indivisible document, not a homogeneous record set: it
            // legitimately declares count 1 while holding many internal tables (see ManifestBuilder /
            // LayoutPlanner). Summing its arrays generically would not be "its own top-level entry count"
            // in any meaningful sense, so it is exempt from the generic recount below.
            if (entry.Path == PackagePublisher.RegistryKey)
            {
                continue;
            }

            var realCount = ManifestBuilder.CountTopLevelEntries(bytes.AsSpan());
            if (realCount != entry.Count)
            {
                throw new PublicationRejectedException(
                    "manifest-count-mismatch",
                    $"{entry.Path}: manifest declares count {entry.Count}, the artifact's real count is {realCount}.");
            }
        }

        var declared = manifest.Artifacts.Select(static entry => entry.Path).ToFrozenSet(StringComparer.Ordinal);
        foreach (var key in artifactsByKey.Keys)
        {
            if (!declared.Contains(key) && !deferredKeys.Contains(key))
            {
                throw new PublicationRejectedException("undeclared-file", key);
            }
        }

        if (manifest.Provenance is { } provenance)
        {
            EnsureProvenanceCompatible(provenance);
        }
    }

    /// <summary>Rejects provenance naming a generator version newer than the one currently running.</summary>
    public static void EnsureProvenanceCompatible(ProvenanceDto provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);

        var running = ProvenanceDto.Current();
        if (Version.TryParse(provenance.GeneratorVersion, out var declared)
            && Version.TryParse(running.GeneratorVersion, out var current)
            && declared > current)
        {
            throw new PublicationRejectedException(
                "incompatible-provenance",
                $"Package generator version {provenance.GeneratorVersion} is newer than the running generator version {running.GeneratorVersion}.");
        }
    }

    /// <summary>
    /// Re-validates an already-published package directory: reads its manifest and every file it
    /// declares, checks cardinality and provenance the same way publication does (AD-025 -- one validator
    /// serves both), and never writes anything.
    /// </summary>
    public static void ValidatePackageDirectory(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        var manifestPath = Path.Combine(packageDirectory, PackagePublisher.ManifestKey);
        if (!File.Exists(manifestPath))
        {
            throw new PublicationRejectedException("not-a-package", packageDirectory);
        }

        var manifest = ReadPayloadOrThrow<ManifestEnvelope>(File.ReadAllBytes(manifestPath), PackagePublisher.ManifestKey);

        var artifactsByKey = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal);
        foreach (var absolutePath in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(packageDirectory, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
            if (relative == PackagePublisher.ManifestKey)
            {
                continue;
            }

            artifactsByKey[relative] = File.ReadAllBytes(absolutePath).ToImmutableArray();
        }

        ValidatePublishedManifest(manifest, artifactsByKey);
    }

    private static void EnsureRegisteredKinds(WireDocument document)
    {
        foreach (var factType in EnumerateFactTypes(document))
        {
            if (!FactTypeNames.Contains(factType))
            {
                throw new PublicationRejectedException("unregistered-kind", factType);
            }
        }

        foreach (var kind in EnumerateObservationKinds(document))
        {
            if (!ObservationKindNames.Contains(kind))
            {
                throw new PublicationRejectedException("unregistered-kind", kind);
            }
        }

        foreach (var kind in EnumerateRelationKinds(document))
        {
            if (!RelationKindNames.Contains(kind))
            {
                throw new PublicationRejectedException("unregistered-kind", kind);
            }
        }
    }

    private static IEnumerable<string> EnumerateFactTypes(WireDocument document)
    {
        foreach (var dto in document.Solutions) yield return dto.Identity.FactType;
        foreach (var dto in document.Projects) yield return dto.Identity.FactType;
        foreach (var dto in document.Documents) yield return dto.Identity.FactType;
        foreach (var dto in document.Symbols) yield return dto.Identity.FactType;
        foreach (var dto in document.Components) yield return dto.Identity.FactType;
        foreach (var dto in document.DeploymentUnits) yield return dto.Identity.FactType;
        foreach (var dto in document.EntryPoints) yield return dto.Identity.FactType;
        foreach (var dto in document.BoundaryOperations) yield return dto.Identity.FactType;
        foreach (var dto in document.ExternalSystems) yield return dto.Identity.FactType;
        foreach (var dto in document.Contracts) yield return dto.Identity.FactType;
        foreach (var dto in document.ContractBindings) yield return dto.Identity.FactType;
        foreach (var dto in document.ContractRevisions) yield return dto.Identity.FactType;
        foreach (var dto in document.DataStores) yield return dto.Identity.FactType;
        foreach (var dto in document.DataObjects) yield return dto.Identity.FactType;
        foreach (var dto in document.DataFields) yield return dto.Identity.FactType;
        foreach (var dto in document.DataOperations) yield return dto.Identity.FactType;
        foreach (var dto in document.ConfigurationBindings) yield return dto.Identity.FactType;
    }

    private static IEnumerable<string> EnumerateObservationKinds(WireDocument document)
    {
        foreach (var records in document.Observations.Values)
        {
            foreach (var dto in records)
            {
                yield return dto.Identity.Kind;
            }
        }
    }

    private static IEnumerable<string> EnumerateRelationKinds(WireDocument document)
    {
        foreach (var records in document.ConfirmedRelations.Values)
        {
            foreach (var dto in records)
            {
                yield return dto.Kind;
            }
        }

        foreach (var dto in document.Candidates)
        {
            yield return dto.Kind;
        }

        foreach (var dto in document.Unresolved)
        {
            yield return dto.Kind;
        }
    }

    private static void EnsureUniqueFactIdentities(WireDocument document)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var identity in EnumerateFactIdentities(document))
        {
            if (!seen.Add(identity))
            {
                throw new PublicationRejectedException("identity-collision", identity);
            }
        }
    }

    private static IEnumerable<string> EnumerateFactIdentities(WireDocument document)
    {
        foreach (var dto in document.Solutions) yield return dto.Identity.Id;
        foreach (var dto in document.Projects) yield return dto.Identity.Id;
        foreach (var dto in document.Documents) yield return dto.Identity.Id;
        foreach (var dto in document.Symbols) yield return dto.Identity.Id;
        foreach (var dto in document.Components) yield return dto.Identity.Id;
        foreach (var dto in document.DeploymentUnits) yield return dto.Identity.Id;
        foreach (var dto in document.EntryPoints) yield return dto.Identity.Id;
        foreach (var dto in document.BoundaryOperations) yield return dto.Identity.Id;
        foreach (var dto in document.ExternalSystems) yield return dto.Identity.Id;
        foreach (var dto in document.Contracts) yield return dto.Identity.Id;
        foreach (var dto in document.ContractBindings) yield return dto.Identity.Id;
        foreach (var dto in document.ContractRevisions) yield return dto.Identity.Id;
        foreach (var dto in document.DataStores) yield return dto.Identity.Id;
        foreach (var dto in document.DataObjects) yield return dto.Identity.Id;
        foreach (var dto in document.DataFields) yield return dto.Identity.Id;
        foreach (var dto in document.DataOperations) yield return dto.Identity.Id;
        foreach (var dto in document.ConfigurationBindings) yield return dto.Identity.Id;
    }

    private static void EnsureContentHashes(WireDocument document)
    {
        foreach (var dto in document.Solutions)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.Projects)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.Documents)
        {
            EnsureDocumentContentHash(dto);
        }

        foreach (var dto in document.Symbols)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.Components)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.DeploymentUnits)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.EntryPoints)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.BoundaryOperations)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.ExternalSystems)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.Contracts)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.ContractBindings)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.ContractRevisions)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.DataStores)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.DataObjects)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.DataFields)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.DataOperations)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var dto in document.ConfigurationBindings)
        {
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
        }

        foreach (var records in document.Observations.Values)
        {
            foreach (var dto in records)
            {
                EnsureContentHash(dto, ObservationIdentity(dto), dto.ContentSha256);
            }
        }

        foreach (var records in document.ConfirmedRelations.Values)
        {
            foreach (var dto in records)
            {
                EnsureContentHash(dto, RelationIdentity(dto.Kind, dto.Source.Id, dto.Target.Id), dto.ContentSha256);
            }
        }

        foreach (var dto in document.Candidates)
        {
            EnsureContentHash(dto, RelationIdentity(dto.Kind, dto.Source.Id, dto.ProposedTarget.Id), dto.ContentSha256);
        }

        foreach (var dto in document.Unresolved)
        {
            EnsureContentHash(dto, dto.Source.Id, dto.ContentSha256);
        }

        foreach (var dto in document.Frontiers)
        {
            EnsureContentHash(dto, dto.Occurrence.Owner.Id, dto.ContentSha256);
        }
    }

    private static void EnsureDocumentContentHash(DocumentDto dto)
    {
        var payload = CanonicalJson.PayloadContentSha256(dto);
        if (string.Equals(dto.ContentSha256, payload, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _ = DocumentHash.Create(dto.ContentSha256);
        }
        catch (ArgumentException exception)
        {
            throw new PublicationRejectedException("content-hash", dto.Identity.Id, exception);
        }
    }

    private static void EnsureContentHash<T>(T dto, string identity, string actual)
    {
        if (!string.Equals(actual, CanonicalJson.PayloadContentSha256(dto), StringComparison.Ordinal))
        {
            throw new PublicationRejectedException("content-hash", identity);
        }
    }

    private static string ObservationIdentity(ObservationDto dto) =>
        $"{dto.Identity.Owner.Id}:{dto.Identity.Kind}:{dto.Identity.OccurrenceOrdinal}";

    private static string RelationIdentity(string kind, string sourceId, string targetId) =>
        kind + ":" + sourceId + ":" + targetId;

    private static void EnsureNoAbsolutePaths(WireDocument document)
    {
        ScanRecords(document.Solutions);
        ScanRecords(document.Projects);
        ScanRecords(document.Documents);
        ScanRecords(document.Symbols);
        ScanRecords(document.Components);
        ScanRecords(document.DeploymentUnits);
        ScanRecords(document.EntryPoints);
        ScanRecords(document.BoundaryOperations);
        ScanRecords(document.ExternalSystems);
        ScanRecords(document.Contracts);
        ScanRecords(document.ContractBindings);
        ScanRecords(document.ContractRevisions);
        ScanRecords(document.DataStores);
        ScanRecords(document.DataObjects);
        ScanRecords(document.DataFields);
        ScanRecords(document.DataOperations);
        ScanRecords(document.ConfigurationBindings);

        foreach (var records in document.Observations.Values)
        {
            ScanRecords(records);
        }

        foreach (var records in document.ConfirmedRelations.Values)
        {
            ScanRecords(records);
        }

        ScanRecords(document.Candidates);
        ScanRecords(document.Unresolved);
        ScanRecords(document.Frontiers);
        ScanRecords(document.Quarantine);
    }

    private static void ScanRecords<T>(ImmutableArray<T> records)
    {
        foreach (var dto in records)
        {
            ScanNode(JsonNode.Parse(CanonicalJson.Write(dto).AsSpan()), string.Empty);
        }
    }

    private static void ScanNode(JsonNode? node, string field)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text) && IsAbsoluteFilesystemPath(text):
                throw new PublicationRejectedException("absolute-path", field);
            case JsonObject obj:
                foreach (var property in obj)
                {
                    ScanNode(property.Value, property.Key);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    ScanNode(item, field);
                }

                break;
        }
    }

    internal static void EnsureNoAbsolutePaths(string artifactKey, ReadOnlySpan<byte> payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactKey);

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            EnsureNoAbsolutePathTokens(Encoding.UTF8.GetString(payload), artifactKey);
            return;
        }

        try
        {
            try
        {
            ScanNode(node, artifactKey);
        }
        catch (PublicationRejectedException exception) when (exception.Gate == "absolute-path")
        {
            throw new PublicationRejectedException("absolute-path", artifactKey);
        }
        }
        catch (PublicationRejectedException exception) when (exception.Gate == "absolute-path")
        {
            throw new PublicationRejectedException("absolute-path", artifactKey);
        }
    }

    internal static void EnsureNoAbsolutePathTokens(string text, string artifactKey)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactKey);

        var start = 0;
        for (var index = 0; index <= text.Length; index++)
        {
            if (index < text.Length && !IsPathTokenSeparator(text[index]))
            {
                continue;
            }

            if (index > start)
            {
                var token = text[start..index].Trim('"', '\'', '`', '<', '>', '[', ']', '(', ')', ',');
                if (IsAbsoluteFilesystemPath(token))
                {
                    throw new PublicationRejectedException("absolute-path", artifactKey);
                }
            }

            start = index + 1;
        }
    }

    private static bool IsPathTokenSeparator(char value) =>
        char.IsWhiteSpace(value) || value is '"' or '\'' or '`' or '(' or ')' or '[' or ']' or '<' or '>' or ',';

    internal static bool IsAbsoluteFilesystemPath(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        if (text[0] == '\\' || text.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        if (text.Length >= 2 && char.IsAsciiLetter(text[0]) && text[1] == ':')
        {
            return true;
        }

        return text[0] == '/' && UnixFilesystemRoots.Contains(FirstPathSegment(text));
    }

    private static string FirstPathSegment(string path)
    {
        var start = 0;
        while (start < path.Length && path[start] is '/' or '\\')
        {
            start++;
        }

        if (start >= path.Length)
        {
            return string.Empty;
        }

        var end = start;
        while (end < path.Length && path[end] is not '/' and not '\\')
        {
            end++;
        }

        return path[start..end];
    }

    private static void EnsureStructuralConstruction(WireDocument document)
    {
        TryCreate(document.Solutions, WireFactMapping.FromDto, static dto => dto.Identity.Id);
        TryCreate(document.Projects, WireFactMapping.FromDto, static dto => dto.Identity.Id);
        TryCreate(document.Documents, WireFactMapping.FromDto, static dto => dto.Identity.Id);
        TryCreate(document.Symbols, WireFactMapping.FromDto, static dto => dto.Identity.Id);

        foreach (var records in document.Observations.Values)
        {
            TryCreate(records, WireObservationMapping.FromDto, ObservationIdentity);
        }
    }

    private static void TryCreate<TDto, TResult>(
        ImmutableArray<TDto> records,
        Func<TDto, TResult> fromDto,
        Func<TDto, string> identity)
    {
        foreach (var dto in records)
        {
            try
            {
                fromDto(dto);
            }
            catch (Exception exception) when (IsConstructionFailure(exception))
            {
                throw new PublicationRejectedException("construction", identity(dto));
            }
        }
    }

    private static bool IsConstructionFailure(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or FormatException or KeyNotFoundException;

    private static ValidationReport QuarantineInvalidDerived(WireDocument document)
    {
        var quarantine = ImmutableArray.CreateBuilder<QuarantineRecordDto>();
        quarantine.AddRange(document.Quarantine);

        var components = KeepOrQuarantine(document.Components, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var deploymentUnits = KeepOrQuarantine(document.DeploymentUnits, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var entryPoints = KeepOrQuarantine(document.EntryPoints, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var boundaryOperations = KeepOrQuarantine(document.BoundaryOperations, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var externalSystems = KeepOrQuarantine(document.ExternalSystems, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var contracts = KeepOrQuarantine(document.Contracts, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var contractBindings = KeepOrQuarantine(document.ContractBindings, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var contractRevisions = KeepOrQuarantine(document.ContractRevisions, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var dataStores = KeepOrQuarantine(document.DataStores, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var dataObjects = KeepOrQuarantine(document.DataObjects, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var dataFields = KeepOrQuarantine(document.DataFields, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var dataOperations = KeepOrQuarantine(document.DataOperations, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);
        var configurationBindings = KeepOrQuarantine(document.ConfigurationBindings, WireFactMapping.FromDto, static dto => dto.Identity.FactType, static dto => dto.Identity.Id, quarantine);

        var factsById = IndexDerivedFacts(
            document.Symbols,
            components,
            boundaryOperations,
            externalSystems,
            contracts,
            dataStores,
            dataObjects,
            dataFields,
            dataOperations,
            configurationBindings);
        var confirmed = ImmutableDictionary.CreateBuilder<string, ImmutableArray<ConfirmedRelationDto>>(StringComparer.Ordinal);
        foreach (var (key, records) in document.ConfirmedRelations)
        {
            var kept = KeepOrQuarantine(
                records,
                dto => WireRelationMapping.FromDto(dto, factsById),
                static dto => dto.Kind,
                static dto => RelationIdentity(dto.Kind, dto.Source.Id, dto.Target.Id),
                quarantine);
            if (!kept.IsEmpty)
            {
                confirmed[key] = kept;
            }
        }

        var quarantined = quarantine.ToImmutable();
        var next = document with
        {
            Components = components,
            DeploymentUnits = deploymentUnits,
            EntryPoints = entryPoints,
            BoundaryOperations = boundaryOperations,
            ExternalSystems = externalSystems,
            Contracts = contracts,
            ContractBindings = contractBindings,
            ContractRevisions = contractRevisions,
            DataStores = dataStores,
            DataObjects = dataObjects,
            DataFields = dataFields,
            DataOperations = dataOperations,
            ConfigurationBindings = configurationBindings,
            ConfirmedRelations = confirmed.ToImmutable(),
            Quarantine = quarantined,
            RunCertification = quarantined.IsEmpty
                ? document.RunCertification
                : new RunCertificationEnvelope("failed", ["A derived fact was quarantined during publication."]),
        };

        return new ValidationReport(next, quarantined);
    }

    private static IReadOnlyDictionary<string, IFact> IndexDerivedFacts(
        ImmutableArray<SymbolDto> symbols,
        ImmutableArray<ComponentDto> components,
        ImmutableArray<BoundaryOperationDto> boundaryOperations,
        ImmutableArray<ExternalSystemDto> externalSystems,
        ImmutableArray<ContractDto> contracts,
        ImmutableArray<DataStoreDto> dataStores,
        ImmutableArray<DataObjectDto> dataObjects,
        ImmutableArray<DataFieldDto> dataFields,
        ImmutableArray<DataOperationDto> dataOperations,
        ImmutableArray<ConfigurationBindingDto> configurationBindings)
    {
        var facts = new Dictionary<string, IFact>(StringComparer.Ordinal);
        Index(symbols, WireFactMapping.FromDto, facts);
        Index(components, WireFactMapping.FromDto, facts);
        Index(boundaryOperations, WireFactMapping.FromDto, facts);
        Index(externalSystems, WireFactMapping.FromDto, facts);
        Index(contracts, WireFactMapping.FromDto, facts);
        Index(dataStores, WireFactMapping.FromDto, facts);
        Index(dataObjects, WireFactMapping.FromDto, facts);
        Index(dataFields, WireFactMapping.FromDto, facts);
        Index(dataOperations, WireFactMapping.FromDto, facts);
        Index(configurationBindings, WireFactMapping.FromDto, facts);
        return facts;
    }

    private static void Index<TDto>(
        ImmutableArray<TDto> records,
        Func<TDto, IFact> fromDto,
        Dictionary<string, IFact> facts)
    {
        if (records.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var dto in records)
        {
            var fact = fromDto(dto);
            facts[fact.Reference.Id.Value] = fact;
        }
    }

    private static ImmutableArray<TDto> KeepOrQuarantine<TDto, TResult>(
        ImmutableArray<TDto> records,
        Func<TDto, TResult> fromDto,
        Func<TDto, string> recordKind,
        Func<TDto, string> identity,
        ImmutableArray<QuarantineRecordDto>.Builder quarantine)
    {
        if (records.IsDefaultOrEmpty)
        {
            return records.IsDefault ? [] : records;
        }

        var kept = ImmutableArray.CreateBuilder<TDto>();
        foreach (var dto in records)
        {
            try
            {
                fromDto(dto);
                kept.Add(dto);
            }
            catch (Exception exception) when (IsConstructionFailure(exception))
            {
                quarantine.Add(new QuarantineRecordDto(
                    recordKind(dto),
                    identity(dto),
                    "construction",
                    exception.Message,
                    PayloadElement(dto)));
            }
        }

        return kept.ToImmutable();
    }

    private static JsonElement PayloadElement<T>(T dto)
    {
        using var parsed = JsonDocument.Parse(CanonicalJson.Write(dto).ToArray());
        return parsed.RootElement.Clone();
    }
}
