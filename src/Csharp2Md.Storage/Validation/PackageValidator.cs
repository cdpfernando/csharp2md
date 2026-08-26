using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
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

    private static readonly FrozenSet<string> UnixFilesystemRoots = FrozenSet.ToFrozenSet(
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
            EnsureContentHash(dto, dto.Identity.Id, dto.ContentSha256);
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

    private static bool IsAbsoluteFilesystemPath(string text)
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

        var confirmed = ImmutableDictionary.CreateBuilder<string, ImmutableArray<ConfirmedRelationDto>>(StringComparer.Ordinal);
        foreach (var (key, records) in document.ConfirmedRelations)
        {
            var kept = KeepOrQuarantine(
                records,
                WireRelationMapping.FromDto,
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
                : new RunCertificationEnvelope("failed"),
        };

        return new ValidationReport(next, quarantined);
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
