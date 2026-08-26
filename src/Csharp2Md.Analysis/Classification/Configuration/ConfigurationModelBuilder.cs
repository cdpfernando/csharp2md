using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Classification.Configuration;

/// <summary>
/// Resolves declared configuration keys and the facts that consume them into a
/// <see cref="ConfigurationModel"/>. The builder reads the ledger and nothing else, so no Roslyn
/// type reaches classification (CDC-08, CDC-35).
/// </summary>
internal static class ConfigurationModelBuilder
{
    internal const string KeyPayloadKey = "key";
    internal const string ResolutionPayloadKey = "resolution";
    internal const string AddressPayloadKey = "address";
    internal const string ConnectionStringsPrefix = "ConnectionStrings:";
    internal const string ServicesPrefix = "Services:";
    internal const string UngroupedDocumentCode = "ungrouped-configuration-document";

    /// <summary>The resolved configuration picture for this snapshot. Later steps fill edges and targets.</summary>
    public static ConfigurationModel Build(ClassifierContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var keys = CollectDeclaredKeys(context);
        return new ConfigurationModel(
            keys,
            [],
            [],
            [],
            new ConfigurationCoverage(keys.Length, 0, 0));
    }

    private static ImmutableArray<DeclaredKey> CollectDeclaredKeys(ClassifierContext context)
    {
        var documentsById = context.FactsByType<Document>()
            .ToDictionary(static document => document.Reference.Id.Value, StringComparer.Ordinal);
        var diagnosed = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<DeclaredKey>();
        foreach (var observation in context.ObservationsByKind(ObservationKind.Configuration)
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal))
        {
            if (!documentsById.TryGetValue(observation.Identity.Owner.Id.Value, out var document))
            {
                continue;
            }

            var keyPath = PayloadReader.Value(observation, KeyPayloadKey);
            if (keyPath is null)
            {
                continue;
            }

            var component = context.ComponentForProject(document.OwningProject);
            if (component is null)
            {
                if (diagnosed.Add(document.Reference.Id.Value))
                {
                    context.Accumulator.AddDiagnostic(
                        new DiagnosticRecord(
                            UngroupedDocumentCode,
                            $"The configuration document '{document.RelativePath}' belongs to a project that is not grouped into a component.",
                            document.RelativePath));
                }

                continue;
            }

            keys.Add(
                new DeclaredKey(
                    keyPath,
                    ParseResolution(PayloadReader.Value(observation, ResolutionPayloadKey)),
                    PayloadReader.Value(observation, AddressPayloadKey),
                    component.Reference,
                    observation.Identity));
        }

        return [.. keys];
    }

    private static KeyResolution ParseResolution(string? value) => value switch
    {
        "literal" => KeyResolution.Literal,
        "dynamic" => KeyResolution.Dynamic,
        "unknown" => KeyResolution.Unknown,
        _ => KeyResolution.Unknown,
    };
}
