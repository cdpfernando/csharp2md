using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using FactProjectDetectionContext = Csharp2Md.Core.Detection.Contracts.ProjectDetectionContext;

namespace Csharp2Md.Core.Detection.CompileTime;

/// <summary>
/// Projects already-evaluated project/package references (T25's real MSBuild evaluation, not a
/// re-parse of the project XML) into compile-time-only relation facts (FACT-16, FACT-48). Because
/// these references never name a remote service and evidence would have to point at project XML
/// rather than a source document, they carry no <see cref="RelationFact.SourceId"/> evidence -
/// the validator only requires evidence for runtime relations.
/// </summary>
internal sealed class CompileTimeReferenceDetector : IProjectFactDetector
{
    private const string ProjectReferenceKind = "project-reference";
    private const string PackageReferenceKind = "package-reference";

    public DetectorDescriptor Descriptor { get; } = DetectorDescriptor.Create(
        DetectorId.Create("io.csharp2md.compile-time"),
        "1.0.0",
        [DetectorLevel.Project],
        [FactKind.Relation]);

    public DetectorResult Detect(FactProjectDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var facts = context.Targets
            .SelectMany(target => ProjectAndPackageFacts(context, target, ordinals))
            .ToImmutableArray();
        return DetectorResult.Create(facts);
    }

    private static IEnumerable<RelationFact> ProjectAndPackageFacts(
        FactProjectDetectionContext context,
        TargetFact target,
        Dictionary<string, int> ordinals)
    {
        foreach (var entry in context.Index.GetProjectReferences(target.TargetId))
        {
            yield return CreateFact(
                context,
                ProjectReferenceKind,
                entry.Reference,
                entry.TargetProjectId?.ToFactId(),
                entry.TargetProjectId is null
                    ? "The referenced project is outside the analyzed solution or could not be evaluated."
                    : null,
                ordinals);
        }

        foreach (var packageId in target.Evaluation?.PackageReferences ?? [])
        {
            yield return CreateFact(
                context,
                PackageReferenceKind,
                packageId,
                null,
                "A package reference names an external dependency, never a project within this solution.",
                ordinals);
        }
    }

    private static RelationFact CreateFact(
        FactProjectDetectionContext context,
        string kind,
        string reference,
        FactId? targetId,
        string? unresolvedReason,
        Dictionary<string, int> ordinals)
    {
        var claim = $"reference={reference}";
        var ordinalKey = $"{kind}\0{claim}";
        var ordinal = ordinals.GetValueOrDefault(ordinalKey) + 1;
        ordinals[ordinalKey] = ordinal;
        var id = RelationFactId.Create(context.Project.ProjectId.ToFactId(), kind, claim, ordinal);
        var header = FactHeader.Create(id.ToFactId(), FactKind.Relation, FactResolution.Exact);
        return new RelationFact(
            header,
            id,
            context.Project.ProjectId.ToFactId(),
            targetId,
            RelationPartition.CompileTime,
            kind,
            unresolvedReason,
            [new RelationDetail("reference", reference)]);
    }
}
