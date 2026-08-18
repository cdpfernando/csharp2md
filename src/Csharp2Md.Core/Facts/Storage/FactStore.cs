using System.Security.Cryptography;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Facts.Storage;

internal sealed record StoredFactFragment(
    FactId RootFactId,
    ArtifactReference Reference,
    string Sha256,
    int ByteLength);

internal interface IArtifactReferenceFactory
{
    ArtifactReference Create(FactId factId);
}

internal interface IAtomicFileOperations
{
    void WriteAllBytes(string path, byte[] bytes);
    void MoveReplace(string source, string destination);
    void DeleteIfExists(string path);
}

internal sealed class FactStore(
    string outputRoot,
    IArtifactReferenceFactory? referenceFactory = null,
    IAtomicFileOperations? files = null)
{
    private readonly string _outputRoot = Path.GetFullPath(outputRoot);
    private readonly IArtifactReferenceFactory _referenceFactory = referenceFactory ?? new Sha256ArtifactReferenceFactory();
    private readonly IAtomicFileOperations _files = files ?? new LocalAtomicFileOperations();
    private readonly Dictionary<ArtifactReference, FactId> _claimedReferences = [];

    public StoredFactFragment Persist(ValidatedFactFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var rootFact = SelectRoot(fragment.Facts);
        var reference = _referenceFactory.Create(rootFact.Header.Id);
        if (_claimedReferences.TryGetValue(reference, out var priorId) && priorId != rootFact.Header.Id)
        {
            throw new FactStoreException(
                $"Distinct fact IDs '{priorId.Value}' and '{rootFact.Header.Id.Value}' map to '{reference.Value}'.");
        }

        var bytes = FactualJsonSerializer.Serialize(FactualJsonMapper.Map(fragment));
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var destination = ResolveArtifactPath(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = Path.Combine(
            Path.GetDirectoryName(destination)!,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            _files.WriteAllBytes(temporary, bytes);
            _files.MoveReplace(temporary, destination);
            _claimedReferences[reference] = rootFact.Header.Id;
            return new StoredFactFragment(rootFact.Header.Id, reference, hash, bytes.Length);
        }
        catch
        {
            _files.DeleteIfExists(temporary);
            throw;
        }
    }

    private string ResolveArtifactPath(ArtifactReference reference)
    {
        var rawRoot = Path.Combine(_outputRoot, "raw");
        var path = Path.GetFullPath(Path.Combine(rawRoot, reference.Value.Replace('/', Path.DirectorySeparatorChar)));
        var factsRoot = Path.GetFullPath(Path.Combine(rawRoot, "facts")) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(factsRoot, PathComparison))
        {
            throw new FactStoreException($"Artifact reference escapes raw/facts: {reference.Value}");
        }

        return path;
    }

    private static IFact SelectRoot(ImmutableArray<IFact> facts)
    {
        if (facts.IsEmpty)
        {
            throw new FactStoreException("A validated fragment must contain at least one fact.");
        }

        var documents = facts.Where(static fact => fact is DocumentFact).ToArray();
        return documents.Length switch
        {
            1 => documents[0],
            > 1 => throw new FactStoreException("A persisted document fragment cannot contain several document roots."),
            _ => facts.OrderBy(static fact => fact.Header.Id.Value, StringComparer.Ordinal).First(),
        };
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed class Sha256ArtifactReferenceFactory : IArtifactReferenceFactory
    {
        public ArtifactReference Create(FactId factId) => ArtifactReference.Create(factId);
    }

    private sealed class LocalAtomicFileOperations : IAtomicFileOperations
    {
        public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);
        public void MoveReplace(string source, string destination) => File.Move(source, destination, overwrite: true);
        public void DeleteIfExists(string path) => File.Delete(path);
    }
}

internal sealed class FactStoreException(string message) : InvalidOperationException(message);

internal static class FactualJsonMapper
{
    public static FactualJsonDocument Map(ValidatedFactFragment fragment)
    {
        var facts = fragment.Facts;
        return new FactualJsonDocument(
            FactualJsonSerializer.SchemaVersion,
            facts.OfType<SolutionFact>().Select(Map).ToImmutableArray(),
            facts.OfType<ProjectFact>().Select(Map).ToImmutableArray(),
            facts.OfType<TargetFact>().Select(Map).ToImmutableArray(),
            facts.OfType<DocumentFact>().Select(Map).ToImmutableArray(),
            facts.OfType<SourceSectionFact>().Select(Map).ToImmutableArray(),
            facts.OfType<SymbolFact>().Select(Map).ToImmutableArray(),
            facts.OfType<ComponentFact>().Select(Map).ToImmutableArray(),
            facts.OfType<RelationFact>().Select(Map).ToImmutableArray(),
            fragment.Diagnostics.Select(Map).ToImmutableArray(),
            []);
    }

    private static SolutionFactJson Map(SolutionFact fact) =>
        new(Map(fact.Header), fact.Name, fact.ProjectIds.Select(static id => id.Value).ToImmutableArray());

    private static ProjectFactJson Map(ProjectFact fact) =>
        new(Map(fact.Header), fact.ProjectId.Value, fact.Name, fact.RelativePath,
            fact.TargetIds.Select(static id => id.Value).ToImmutableArray(),
            fact.DocumentIds.Select(static id => id.Value).ToImmutableArray(),
            fact.Evaluation is null ? null : new ProjectEvaluationJson(
                fact.Evaluation.DeclaredSdk,
                fact.Evaluation.EvaluatedImports,
                fact.Evaluation.TargetFrameworks,
                fact.Evaluation.RequestedAnalysis,
                Wire(fact.Evaluation.EffectiveResolution),
                fact.Evaluation.RestorePerformed,
                fact.Evaluation.Isolation));

    private static TargetFactJson Map(TargetFact fact) =>
        new(Map(fact.Header), fact.TargetId.Value, fact.ProjectId.Value, fact.TargetFramework,
            fact.Evaluation is null ? null : new TargetEvaluationJson(
                fact.Evaluation.OutputType,
                fact.Evaluation.AssemblyName,
                fact.Evaluation.RootNamespace,
                fact.Evaluation.CompileItems,
                fact.Evaluation.ProjectReferences,
                fact.Evaluation.PackageReferences,
                fact.Evaluation.References,
                fact.Evaluation.Constants,
                fact.Evaluation.LanguageVersion,
                fact.Evaluation.NullableMode,
                fact.Evaluation.CompiledExtensions));

    private static DocumentFactJson Map(DocumentFact fact) =>
        new(Map(fact.Header), fact.DocumentId.Value, fact.ProjectId.Value, fact.RelativePath,
            fact.Sections.Select(static section => section.Header.Id.Value).ToImmutableArray(),
            fact.SymbolIds.Select(static id => id.Value).ToImmutableArray());

    private static SourceSectionFactJson Map(SourceSectionFact fact) =>
        new(Map(fact.Header), fact.DocumentId.Value, fact.SectionKind, fact.OccurrenceOrdinal,
            fact.StartOffset, fact.Length, fact.Source);

    private static SymbolFactJson Map(SymbolFact fact) =>
        new(Map(fact.Header), fact.SymbolId.Value, fact.DocumentId.Value, fact.SymbolKind,
            fact.ContainsErrorSymbol, fact.BaseAndInterfaceIds.Select(static id => id.Value).ToImmutableArray(),
            fact.Attributes, fact.RelevantTypeReferences,
            fact.Semantics is null ? null : new SymbolSemanticJson(
                fact.Semantics.ImplementedMemberIds.Select(static id => id.Value).ToImmutableArray(),
                fact.Semantics.OverriddenMemberId?.Value));

    private static ComponentFactJson Map(ComponentFact fact) =>
        new(Map(fact.Header), fact.ComponentId.Value, fact.ComponentKind,
            fact.ProjectIds.Select(static id => id.Value).ToImmutableArray());

    private static RelationFactJson Map(RelationFact fact) =>
        new(Map(fact.Header), fact.RelationId.Value, fact.SourceId.Value, fact.TargetId?.Value,
            Wire(fact.Partition), fact.RelationKind, fact.UnresolvedReason);

    private static AnalysisDiagnosticJson Map(AnalysisDiagnostic diagnostic) =>
        new(diagnostic.Id.Value, diagnostic.Code, Wire(diagnostic.Severity), Wire(diagnostic.Stage),
            diagnostic.ScopeId.Value, diagnostic.Message,
            diagnostic.Data.Select(static item => new DiagnosticDataJson(item.Key, item.Value)).ToImmutableArray(),
            diagnostic.Evidence.Select(Map).ToImmutableArray(), diagnostic.ExtensionId?.Value);

    private static FactHeaderJson Map(FactHeader header) =>
        new(header.Id.Value, Wire(header.Kind), Wire(header.Resolution),
            header.Provenance.Select(static provenance => new FactProvenanceJson(
                provenance.EngineId, provenance.EngineVersion, provenance.DetectorId?.Value, provenance.DetectorVersion)).ToImmutableArray(),
            header.Evidence.Select(Map).ToImmutableArray(),
            header.DiagnosticIds.Select(static id => id.Value).ToImmutableArray());

    private static EvidenceJson Map(Evidence evidence) =>
        new(evidence.DocumentId.Value, evidence.RelativePath, evidence.StartLine, evidence.StartColumn,
            evidence.EndLine, evidence.EndColumn);

    private static string Wire<T>(T value) where T : struct, Enum =>
        value.ToString().Replace("SourceSection", "source-section", StringComparison.Ordinal).ToLowerInvariant();
}
