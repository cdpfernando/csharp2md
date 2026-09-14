using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using DomainDocument = Csharp2Md.Domain.Facts.Document;
using DomainProject = Csharp2Md.Domain.Facts.Project;
using DomainSolution = Csharp2Md.Domain.Facts.Solution;
using DomainProjectId = Csharp2Md.Domain.Identity.ProjectId;
using DomainSolutionId = Csharp2Md.Domain.Identity.SolutionId;
using RoslynProject = Microsoft.CodeAnalysis.Project;

namespace Csharp2Md.Analysis.Extraction;

internal static class ProjectMetadataEmitter
{
    private static readonly BindingDiagnostic Configured = new("configured", "configured");

    public static int Emit(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var bound = context.BoundSolution;
        if (bound is null)
        {
            return 0;
        }

        var snapshot = context.Accumulator.ToSnapshot();
        var solution = snapshot.Facts.OfType<DomainSolution>().SingleOrDefault();
        if (solution is null)
        {
            return 0;
        }

        var projects = snapshot.Facts.OfType<DomainProject>()
            .ToDictionary(static project => project.Id.Value, StringComparer.Ordinal);
        var documents = snapshot.Facts.OfType<DomainDocument>()
            .Where(static document => document.RelativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                static document => document.RelativePath,
                StringComparer.Ordinal);
        var compiledAssemblies = new HashSet<string>(
            bound.Compilations.Select(static compilation => compilation.AssemblyName).OfType<string>(),
            StringComparer.Ordinal);
        var unresolvable = snapshot.Diagnostics
            .Where(static record => string.Equals(record.Code, "unresolvable-sdk", StringComparison.Ordinal))
            .Select(static record => record.IdentityOrKey)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var processed = new HashSet<string>(StringComparer.Ordinal);
        var unanalyzed = new HashSet<string>(StringComparer.Ordinal);
        var emitted = 0;

        foreach (var lease in bound.Leases)
        {
            foreach (var project in lease.Solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (project.Language != LanguageNames.CSharp || string.IsNullOrEmpty(project.FilePath))
                {
                    continue;
                }

                var logicalPath = AuthorizedRoot.ToLogicalPath(context.AuthorizedRoot, project.FilePath);
                if (!processed.Add(logicalPath))
                {
                    continue;
                }

                var projectId = DomainProjectId.Create(solution.Id, logicalPath);
                if (!projects.TryGetValue(projectId.Value, out var owner)
                    || unresolvable.Contains(logicalPath)
                    || !compiledAssemblies.Contains(project.AssemblyName))
                {
                    continue;
                }

                if (!documents.TryGetValue(logicalPath, out var csproj))
                {
                    continue;
                }

                var hash = ObservationMaterializer.HashFileBytes(project.FilePath);
                var locator = ObservationMaterializer.CreateWholeDocumentLocator(csproj, project.FilePath);
                AddObservation(
                    context,
                    owner.Reference,
                    Payload("output-kind", MapOutputKind(project.CompilationOptions?.OutputKind)),
                    occurrenceOrdinal: 1,
                    locator,
                    hash);
                emitted++;

                var referenced = CollectReferencedPaths(
                    lease.Solution,
                    project,
                    solution.Id,
                    projects,
                    context.AuthorizedRoot,
                    unanalyzed,
                    context);
                var ordinal = 2;
                foreach (var referencedPath in referenced)
                {
                    AddObservation(
                        context,
                        owner.Reference,
                        Payload("project-reference", referencedPath),
                        ordinal,
                        locator,
                        hash);
                    emitted++;
                    ordinal++;
                }
            }
        }

        return emitted;
    }

    private static ImmutableArray<string> CollectReferencedPaths(
        Microsoft.CodeAnalysis.Solution roslynSolution,
        RoslynProject project,
        DomainSolutionId solutionId,
        IReadOnlyDictionary<string, DomainProject> projects,
        string authorizedRoot,
        HashSet<string> unanalyzed,
        PipelineContext context)
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var reference in project.ProjectReferences)
        {
                var referenced = roslynSolution.GetProject(reference.ProjectId);
            var identity = UnanalyzedIdentity(reference, referenced, authorizedRoot);
            if (referenced is null || string.IsNullOrEmpty(referenced.FilePath))
            {
                RecordUnanalyzed(context, unanalyzed, identity);
                continue;
            }

            var logicalPath = AuthorizedRoot.ToLogicalPath(authorizedRoot, referenced.FilePath);
            if (!TryProjectId(solutionId, logicalPath, out var projectId)
                || !projects.ContainsKey(projectId.Value))
            {
                RecordUnanalyzed(context, unanalyzed, identity);
                continue;
            }

            paths.Add(logicalPath);
        }

        return [.. paths];
    }

    private static string UnanalyzedIdentity(
        ProjectReference reference,
        RoslynProject? referenced,
        string authorizedRoot)
    {
        if (referenced?.FilePath is { Length: > 0 } path)
        {
            return Relativize(path, authorizedRoot);
        }

        var fallback = reference.ProjectId.ToString();
        return string.IsNullOrWhiteSpace(fallback)
            ? "unanalyzed-project"
            : Relativize(fallback, authorizedRoot);
    }

    private static string Relativize(string path, string authorizedRoot)
    {
        var candidate = path.Replace('\\', '/');
        if (Path.IsPathRooted(path))
        {
            candidate = AuthorizedRoot.ToLogicalPath(authorizedRoot, path);
        }

        if (Path.IsPathRooted(candidate)
            || (candidate.Length >= 2 && char.IsAsciiLetter(candidate[0]) && candidate[1] == ':')
            || candidate.Contains("..", StringComparison.Ordinal))
        {
            return Path.GetFileName(path.Replace('\\', '/'));
        }

        return candidate;
    }

    private static bool TryProjectId(DomainSolutionId solutionId, string logicalPath, out DomainProjectId projectId)
    {
        try
        {
            projectId = DomainProjectId.Create(solutionId, logicalPath);
            return true;
        }
        catch (ArgumentException)
        {
            projectId = default;
            return false;
        }
    }

    private static void RecordUnanalyzed(PipelineContext context, HashSet<string> seen, string identity)
    {
        if (!seen.Add(identity))
        {
            return;
        }

        context.Accumulator.AddDiagnostic(new Csharp2Md.Analysis.Storage.DiagnosticRecord(
            "unanalyzed-project-reference",
            $"The project reference '{identity}' was not analyzed in this solution.",
            identity));
    }

    private static void AddObservation(
        PipelineContext context,
        FactReference owner,
        NormalizedPayload payload,
        int occurrenceOrdinal,
        EvidenceLocator locator,
        DocumentHash hash) =>
        context.Accumulator.AddObservation(
            Observation.Create(
                owner,
                ObservationKind.Configuration,
                payload,
                occurrenceOrdinal,
                locator,
                EvidenceMethod.Configured,
                Configured,
                hash,
                ObservationMaterializer.Version));

    private static NormalizedPayload Payload(string key, string value) =>
        NormalizedPayload.Create(
            [new PayloadEntry(key, StructuralLiteral.Create(LiteralRole.ConfigurationKey, value, key))]);

    private static string MapOutputKind(OutputKind? kind) =>
        kind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication
            ? "application"
            : "library";
}
