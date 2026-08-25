using System.Collections.Frozen;
using System.Text.Json;
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

    public static ValidationReport Validate(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureRegisteredKinds(document);
        return new ValidationReport(document, document.Quarantine);
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
}
