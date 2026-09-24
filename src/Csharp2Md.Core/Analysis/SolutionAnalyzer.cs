using System.Text.RegularExpressions;
using System.Xml.Linq;
using Csharp2Md.Core.Analysis.Extraction;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Analysis.Semantics;

namespace Csharp2Md.Core.Analysis;

internal sealed class SolutionAnalysisException : InvalidOperationException
{
    public SolutionAnalysisException(string cause, SolutionIdentity solution, ProjectIdentity? project = null, AnalysisVariant? variant = null, Exception? innerException = null)
        : base($"analysis:{cause}", innerException) { Cause = cause; Solution = solution; Project = project; Variant = variant; }
    public string Cause { get; } public SolutionIdentity Solution { get; } public ProjectIdentity? Project { get; } public AnalysisVariant? Variant { get; }
}

internal static class SolutionAnalyzer
{
    internal static async Task<FactualGraph> AnalyzeAsync(
        string solutionPath,
        bool includeTests,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullSolutionPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(fullSolutionPath))
        {
            throw new SolutionAnalysisException(
                "solution-not-found",
                CanonicalIdentity.CreateSolution(Path.GetFileNameWithoutExtension(fullSolutionPath), Path.GetFileName(fullSolutionPath)));
        }

        var authorizedRoot = FindAuthorizedRoot(fullSolutionPath);
        var logicalSolutionPath = PathGuard.ToLogicalPath(authorizedRoot, fullSolutionPath);
        var solution = CanonicalIdentity.CreateSolution(
            Path.GetFileNameWithoutExtension(fullSolutionPath),
            logicalSolutionPath);
        var plannedVariants = await ProjectVariantPlanner.DiscoverAsync(
            fullSolutionPath,
            authorizedRoot,
            cancellationToken).ConfigureAwait(false);
        var policy = new AnalysisPolicy(includeTests);
        var fragments = ImmutableArray.CreateBuilder<FactualGraph>();
        var extractedCount = 0;
        var filteredCount = 0;

        foreach (var plannedVariant in plannedVariants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = CanonicalIdentity.CreateProject(solution, plannedVariant.ProjectLogicalRelativePath);
            AnalysisVariant? variant = null;
            try
            {
                await using var workspace = await ProjectVariantWorkspace.OpenAsync(
                    authorizedRoot,
                    plannedVariant,
                    cancellationToken).ConfigureAwait(false);
                var compilation = await workspace.GetRootCompilationAsync(cancellationToken).ConfigureAwait(false);
                variant = CanonicalIdentity.CreateVariant(
                    plannedVariant.TargetFramework,
                    "Release",
                    [],
                    "default");

                var projectPath = Path.Combine(
                    authorizedRoot,
                    plannedVariant.ProjectLogicalRelativePath.Replace('/', Path.DirectorySeparatorChar));
                var inventory = SourceInventory.Collect(
                    authorizedRoot,
                    Path.GetDirectoryName(projectPath)!,
                    policy);
                var architecture = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
                    solution,
                    project,
                    variant,
                    inventory.Accepted,
                    File.ReadAllText(projectPath),
                    compilation));

                var referencedCompilations = compilation is null
                    ? ImmutableArray<ReferencedProjectCompilation>.Empty
                    : await ReferencedCompilationsAsync(workspace, solution, authorizedRoot, cancellationToken).ConfigureAwait(false);
                var causal = compilation is null
                    ? new CausalRelationExtractionResult([], [], [], [], [])
                    : CausalRelationExtractor.Extract(new CausalRelationExtractionInput(
                        solution,
                        project,
                        variant,
                        compilation,
                        ReferencedProjects(workspace, solution, authorizedRoot),
                        referencedCompilations));
                var persistence = compilation is null
                    ? new ConfigurationPersistenceExtractionResult([], [], [], [])
                    : ConfigurationPersistenceExtractor.Extract(new ConfigurationPersistenceExtractionInput(
                        solution,
                        project,
                        variant,
                        compilation,
                        referencedCompilations));

                var entities = architecture.Entities
                    .AddRange(causal.Entities)
                    .AddRange(persistence.Entities)
                    .DistinctBy(static entity => entity.CanonicalKey)
                    .OrderBy(static entity => entity.CanonicalKey, StringComparer.Ordinal)
                    .ToImmutableArray();
                var occurrences = architecture.Occurrences
                    .AddRange(causal.Occurrences)
                    .AddRange(persistence.Occurrences)
                    .OrderBy(static occurrence => occurrence.EntityCanonicalKey, StringComparer.Ordinal)
                    .ThenBy(static occurrence => occurrence.Locator.RelativePath, StringComparer.Ordinal)
                    .ToImmutableArray();
                var evidence = architecture.Evidence
                    .AddRange(causal.Evidence)
                    .AddRange(persistence.Evidence)
                    .DistinctBy(static item => item.CanonicalKey)
                    .OrderBy(static item => item.CanonicalKey, StringComparer.Ordinal)
                    .ToImmutableArray();
                var relations = causal.Relations
                    .AddRange(persistence.Relations)
                    .DistinctBy(static relation => relation.CanonicalKey)
                    .OrderBy(static relation => relation.CanonicalKey, StringComparer.Ordinal)
                    .ToImmutableArray();
                var gaps = causal.Gaps
                    .DistinctBy(static gap => gap.CanonicalKey)
                    .OrderBy(static gap => gap.CanonicalKey, StringComparer.Ordinal)
                    .ToImmutableArray();
                var sources = inventory.Accepted
                    .Select(document => new SourceDocumentSnapshot(
                        CanonicalIdentity.CreateDocumentKey(solution, document.RelativePath),
                        new LogicalLocator(document.RelativePath, new SourceSpan(1, 1, 1, 1), project),
                        document.IsTest,
                        document.ContentDigest))
                    .OrderBy(static source => source.CanonicalKey, StringComparer.Ordinal)
                    .ToImmutableArray();
                var fragmentExtracted = entities.Length + occurrences.Length + evidence.Length
                    + relations.Length + gaps.Length + sources.Length;
                extractedCount += fragmentExtracted;
                filteredCount += inventory.ExcludedRelativePaths.Length;
                fragments.Add(new FactualGraph(
                    solution,
                    entities,
                    occurrences,
                    evidence,
                    relations,
                    gaps,
                    sources,
                    new ExtractionMeasurements(fragmentExtracted, inventory.ExcludedRelativePaths.Length)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OccurrenceCollisionException)
            {
                throw;
            }
            catch (Exception exception) when (exception is not SolutionAnalysisException)
            {
                throw new SolutionAnalysisException(
                    $"project-analysis-failed:{exception.GetType().Name}",
                    solution,
                    project,
                    variant,
                    exception);
            }
        }

        return Assemble(solution, fragments, extractedCount, filteredCount, cancellationToken);
    }

    internal static FactualGraph Assemble(SolutionIdentity solution, IEnumerable<FactualGraph> fragments, int extractedCount, int filteredCount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solution); ArgumentNullException.ThrowIfNull(fragments); cancellationToken.ThrowIfCancellationRequested();
        var graphs = fragments.ToArray();
        if (graphs.Any(g => g.Solution != solution)) throw new SolutionAnalysisException("solution-mismatch", solution);
        return new FactualGraph(solution,
            graphs.SelectMany(g => g.Entities).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Occurrences).OrderBy(x => x.EntityCanonicalKey, StringComparer.Ordinal).ThenBy(x => x.Project.CanonicalKey, StringComparer.Ordinal).ThenBy(x => CanonicalIdentity.VariantKey(x.Variant), StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Evidence).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Relations).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Gaps).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Sources).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            new ExtractionMeasurements(extractedCount, filteredCount));
    }

    private static ImmutableArray<ProjectIdentity> ReferencedProjects(
        ProjectVariantWorkspace workspace,
        SolutionIdentity solution,
        string authorizedRoot) =>
        workspace.ReferencedProjects()
            .Where(static project => !string.IsNullOrWhiteSpace(project.FilePath))
            .Select(project => CanonicalIdentity.CreateProject(
                solution,
                PathGuard.ToLogicalPath(authorizedRoot, project.FilePath!)))
            .OrderBy(static project => project.CanonicalKey, StringComparer.Ordinal)
            .ToImmutableArray();

    // Pairs each directly-referenced project with its own already-computed Compilation, so
    // CausalRelationExtractor can resolve a target symbol's true owner by exact SyntaxTree identity
    // (see DEP-01's OwnerOf) instead of attributing it to whichever root observed it. Roslyn already
    // builds these compilations as a side effect of resolving the root's own CompilationReferences, so
    // this reuses cached results rather than triggering new compilation work.
    private static async Task<ImmutableArray<ReferencedProjectCompilation>> ReferencedCompilationsAsync(
        ProjectVariantWorkspace workspace,
        SolutionIdentity solution,
        string authorizedRoot,
        CancellationToken cancellationToken)
    {
        var builder = ImmutableArray.CreateBuilder<ReferencedProjectCompilation>();
        foreach (var project in workspace.ReferencedProjects())
        {
            if (string.IsNullOrWhiteSpace(project.FilePath))
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            builder.Add(new ReferencedProjectCompilation(
                CanonicalIdentity.CreateProject(solution, PathGuard.ToLogicalPath(authorizedRoot, project.FilePath!)),
                compilation));
        }

        return builder.ToImmutable();
    }

    private static string FindAuthorizedRoot(string solutionPath)
    {
        var candidates = DeclaredProjectPaths(solutionPath)
            .Append(solutionPath)
            .Select(Path.GetFullPath)
            .ToArray();
        var root = Path.GetDirectoryName(solutionPath)
            ?? throw new InvalidOperationException("The solution path has no parent directory.");
        while (candidates.Any(candidate => !PathGuard.ContainsPath(root, candidate)))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new InvalidOperationException("The solution projects do not share an authorized root.");
        }

        return PathGuard.Normalize(root);
    }

    private static IEnumerable<string> DeclaredProjectPaths(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        if (Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            var document = XDocument.Load(solutionPath, LoadOptions.None);
            foreach (var project in document.Descendants()
                         .Where(static element => element.Name.LocalName == "Project")
                         .Select(static element => (string?)element.Attribute("Path"))
                         .Where(static path => !string.IsNullOrWhiteSpace(path)))
            {
                yield return Path.GetFullPath(Path.Combine(solutionDirectory, project!));
            }

            yield break;
        }

        foreach (var line in File.ReadLines(solutionPath))
        {
            foreach (Match match in Regex.Matches(line, "\"([^\"]+\\.csproj)\"", RegexOptions.IgnoreCase))
            {
                yield return Path.GetFullPath(Path.Combine(solutionDirectory, match.Groups[1].Value));
            }
        }
    }
}
