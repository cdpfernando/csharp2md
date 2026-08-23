using System.Text;
using Csharp2Md.Core.Analysis.Components;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Composition;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Output;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.Core.Projection.Markdown;

namespace Csharp2Md.Core.Analysis;

internal delegate FactValidationResult FragmentValidationFunc(FactValidationInput input);

public interface IAnalysisEngineObserver
{
    void ScopeStarted(string scope);
    void ScopeCompleted(string scope);
}

public sealed class AnalysisEngine
{
    private static readonly FactProvenance Provenance = new("csharp2md.syntax", "1");
    private readonly InertInventory _inventory;
    private readonly FragmentValidationFunc _validate;
    private readonly IAnalysisEngineObserver? _observer;
    private readonly IProjectEvaluationAdapter _evaluator;
    private readonly ISemanticCompilationAdapter _compilationAdapter;
    private readonly ISourceGeneratorAdapter _generatorAdapter;
    private readonly Action<SymbolIndex>? _onSymbolIndexBuilt;
    private readonly IEnumerable<IDataAccessAnalyzer>? _dataAccessAnalyzers;

    public AnalysisEngine()
        : this(new InertInventory(), FactValidator.Validate, null,
            new DotnetMsBuildEvaluator(), new SemanticCompilationAdapter(), new SourceGeneratorAdapter())
    {
    }

    public AnalysisEngine(IAnalysisEngineObserver? observer)
        : this(new InertInventory(), FactValidator.Validate, observer,
            new DotnetMsBuildEvaluator(), new SemanticCompilationAdapter(), new SourceGeneratorAdapter())
    {
    }

    internal AnalysisEngine(
        InertInventory inventory,
        FragmentValidationFunc validate,
        IAnalysisEngineObserver? observer,
        Action<SymbolIndex>? onSymbolIndexBuilt = null,
        IEnumerable<IDataAccessAnalyzer>? dataAccessAnalyzers = null)
        : this(inventory, validate, observer,
            new DotnetMsBuildEvaluator(), new SemanticCompilationAdapter(), new SourceGeneratorAdapter(),
            onSymbolIndexBuilt, dataAccessAnalyzers)
    {
    }

    internal AnalysisEngine(
        InertInventory inventory,
        FragmentValidationFunc validate,
        IAnalysisEngineObserver? observer,
        IProjectEvaluationAdapter evaluator,
        ISemanticCompilationAdapter compilationAdapter,
        ISourceGeneratorAdapter generatorAdapter,
        Action<SymbolIndex>? onSymbolIndexBuilt = null,
        IEnumerable<IDataAccessAnalyzer>? dataAccessAnalyzers = null)
    {
        _onSymbolIndexBuilt = onSymbolIndexBuilt;
        _dataAccessAnalyzers = dataAccessAnalyzers;
        _inventory = inventory;
        _validate = validate;
        _observer = observer;
        _evaluator = evaluator;
        _compilationAdapter = compilationAdapter;
        _generatorAdapter = generatorAdapter;
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var inventory = _inventory.Inventory(request);
        if (!inventory.IsSuccess)
        {
            return Result(request, AnalysisMode.SyntaxOnly, 1,
                "Inventory failed before output preparation.",
                inventory.Diagnostics.Select(static diagnostic => diagnostic.Message));
        }

        var inputRoot = Directory.Exists(request.Input)
            ? Path.GetFullPath(request.Input)
            : Path.GetDirectoryName(Path.GetFullPath(request.Input))!;
        try
        {
            new OutputWriter(request.OutputRoot).PrepareRun(inputRoot, request.ForceOutput);
        }
        catch (OutputPreparationException exception)
        {
            return Result(request, AnalysisMode.SyntaxOnly, 1, exception.Message, [exception.Message]);
        }

        var store = new FactStore(request.OutputRoot);
        var storedFragments = ImmutableArray.CreateBuilder<StoredFactFragment>();
        var coverageFacts = ImmutableArray.CreateBuilder<IFact>();
        var symbolFacts = ImmutableArray.CreateBuilder<SymbolFact>();
        var coverageOverrides = ImmutableArray.CreateBuilder<ScopeCoverageInput>();
        var analysisDiagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
        // Pass one of database access discovery accumulates here across the whole run; pass two reads
        // the snapshot once the document loop is done.
        var databaseClaims = new DatabaseClaimAccumulator();
        // Pass one of relation resolution accumulates here across the whole run, mirroring
        // databaseClaims; pass two (RelationResolver) reads the snapshot once the document loop and
        // database resolution are both done (AD-018).
        var relationClaims = new RelationClaimAccumulator();
        var resultDiagnostics = new List<string>(inventory.Diagnostics.Select(static diagnostic => diagnostic.Message));
        var loadedExtensions = ImmutableArray.CreateBuilder<string>();
        var analysedProjects = new HashSet<ProjectFactId>();
        var structuralFailure = false;
        var semanticSuccess = false;
        var documentCount = 0;
        var projectCount = 0;

        foreach (var service in inventory.Services)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScopeStarted($"service:{service.RootPath}");
            try
            {
                foreach (var project in service.Projects)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // A shared project listed by more than one solution is reached once per path. That is
                    // normal, not an anomaly, so the repeat is skipped silently: analysing it again would
                    // duplicate its documents, fragments and coverage scopes.
                    if (!analysedProjects.Add(ProjectFactId.Create(project.RelativePath)))
                    {
                        continue;
                    }

                    projectCount++;
                    ScopeStarted($"project:{project.RelativePath}");
                    try
                    {
                        var projectResult = await AnalyzeProjectAsync(
                                request, project, store, storedFragments, coverageFacts,
                                symbolFacts, coverageOverrides, analysisDiagnostics, resultDiagnostics,
                                loadedExtensions, databaseClaims, relationClaims, cancellationToken)
                            .ConfigureAwait(false);
                        documentCount += projectResult.DocumentCount;
                        structuralFailure |= projectResult.StructuralFailure;
                        semanticSuccess |= projectResult.SemanticSuccess;
                    }
                    finally
                    {
                        ScopeCompleted($"project:{project.RelativePath}");
                    }
                }
            }
            finally
            {
                ScopeCompleted($"service:{service.RootPath}");
            }
        }

        var effectiveMode = request.Options.Mode is AnalysisMode.Semantic && semanticSuccess
            ? AnalysisMode.Semantic
            : AnalysisMode.SyntaxOnly;
        var accumulated = coverageFacts.ToImmutable();
        var symbolIndex = SymbolIndexBuilder.Build(
            symbolFacts,
            accumulated.OfType<ProjectFact>(),
            accumulated.OfType<DocumentFact>(),
            accumulated.OfType<TargetFact>());
        _onSymbolIndexBuilt?.Invoke(symbolIndex);

        // Pass two of database access discovery. It runs here and not per document because the
        // configuration naming an entity's table commonly lives in another document, and
        // configured-over-convention precedence is undecidable until every document has been seen.
        var databaseResolution = DatabaseMappingResolver.Resolve(databaseClaims.ToSnapshot(), symbolIndex);
        var database = DatabaseFragmentBuilder.Build(databaseResolution, _validate);
        analysisDiagnostics.AddRange(database.Diagnostics);
        // RelationProjector/DatabaseAggregateProjector only react to RelationFact/ComponentFact and
        // DatabaseObjectFact/DatabaseColumnFact, which exist solely inside these two pass-two aggregate
        // fragments - never inside a per-document or per-project fragment. Projecting from just these two
        // (instead of every validated fragment the whole run has produced) keeps this bounded by the
        // aggregate size rather than by total codebase size.
        var aggregateFragments = ImmutableArray.CreateBuilder<ValidatedFactFragment>();
        if (database.Fragment is { } databaseFragment)
        {
            storedFragments.Add(store.Persist(databaseFragment));
            aggregateFragments.Add(databaseFragment);
        }
        else if (!database.Diagnostics.IsEmpty)
        {
            structuralFailure = true;
        }

        var databaseAggregate = DatabaseAggregateProjector.Project(aggregateFragments);
        analysisDiagnostics.AddRange(databaseAggregate.Diagnostics);

        // Pass two of relation resolution (AD-018): runs once the run's complete SymbolIndex and the
        // database resolution both exist, since RELR-32's database relations arrive as already-targeted
        // claims from databaseResolution.Relations. knownFactIds is the union of every identity the run
        // actually produced - symbols, database nodes, and persisted documents (an inferred-publish
        // claim's owner falls back to its document when no enclosing member exists) - so
        // ExistingTargetStrategy (RELR-02/03) and C2M-FV-002 can tell a proven reference from a dangling one.
        relationClaims.AddResolved(databaseResolution.Relations);
        var knownFactIds = symbolIndex.Symbols.Select(static symbol => symbol.SymbolId.ToFactId())
            .Concat(databaseResolution.Objects.Select(static entry => entry.ObjectId.ToFactId()))
            .Concat(databaseResolution.Columns.Select(static entry => entry.ColumnId.ToFactId()))
            .Concat(accumulated.OfType<DocumentFact>().Select(static document => document.Header.Id))
            .ToHashSet();
        // AddResolved's claims carry evidence from whichever document DatabaseMappingResolver read them
        // from, but relationClaims only records a document's extent when that same document also
        // produced at least one syntax-only relation claim (RelationClaimAccumulator's own memory rule,
        // T6). A document that is pure SQL text with no calls/creates/etc - OrderSqlQueries.cs in the
        // fixture - has database claims but zero relation claims, so its extent would otherwise be
        // missing here; databaseResolution.Documents is the superset databaseClaims already recorded.
        var relationSnapshot = relationClaims.ToSnapshot();
        var relationSnapshotWithDatabaseDocuments = relationSnapshot with
        {
            Documents = relationSnapshot.Documents
                .Concat(databaseResolution.Documents)
                .DistinctBy(static document => document.DocumentId)
                .OrderBy(static document => document.DocumentId.Value, StringComparer.Ordinal)
                .ToImmutableArray(),
        };
        var relationResolution = RelationResolver.Default.Resolve(
            relationSnapshotWithDatabaseDocuments, symbolIndex, knownFactIds, cancellationToken);
        // The resolver's own C2M-RELR-* diagnostics (unresolved/ambiguous/dynamic targets, a throwing
        // strategy) are distinct from RelationFragmentBuilder.Build's ValidationDiagnostics (C2M-FV-*
        // only) - unlike DatabaseMappingResolver.Resolve, which never produces diagnostics of its own,
        // this pass-two step does, and RELR-37's diagnostics must reach diagnostics.json and the run's
        // own summary just as any other analysis diagnostic does.
        analysisDiagnostics.AddRange(relationResolution.Diagnostics);
        var relations = RelationFragmentBuilder.Build(relationResolution, knownFactIds, _validate);
        analysisDiagnostics.AddRange(relations.Diagnostics);
        if (relations.Fragment is { } relationFragment)
        {
            storedFragments.Add(store.Persist(relationFragment));
            aggregateFragments.Add(relationFragment);
        }
        else if (!relations.Diagnostics.IsEmpty)
        {
            structuralFailure = true;
        }

        // Pass two of component synthesis (COMP-01/03): one ComponentFact per analysed ProjectFact, using
        // every project id the run actually produced so C2M-FV-002 accepts a component's own reference to
        // the project it mirrors - that project fact lives in a fragment this builder never sees.
        var projects = accumulated.OfType<ProjectFact>().ToImmutableArray();
        var componentKnownIds = knownFactIds
            .Concat(projects.Select(static project => project.ProjectId.ToFactId()))
            .ToHashSet();
        var componentResult = ComponentFragmentBuilder.Build(projects, componentKnownIds, _validate);
        analysisDiagnostics.AddRange(componentResult.Diagnostics);
        if (componentResult.Fragment is { } componentFragment)
        {
            storedFragments.Add(store.Persist(componentFragment));
            aggregateFragments.Add(componentFragment);
        }
        else if (!componentResult.Diagnostics.IsEmpty)
        {
            structuralFailure = true;
        }

        // Pass two of component graph projection (COMP-10..COMP-27): must run - and its diagnostics must be
        // added to analysisDiagnostics - on its own statement here, strictly before CoverageProjector.Project
        // below. RelationProjector.Project used to run inline inside the snapshot constructor *after*
        // coverage was already computed, which is exactly why a projector-produced diagnostic (C2M-CG-001)
        // would otherwise never reach diagnostics.json (design.md's diagnostic-ordering trap; mirrors the
        // databaseAggregate precedent above).
        // databaseResolution.Objects/.Columns are pre-fact ResolvedDatabaseObject/ResolvedDatabaseColumn
        // records; the actual DatabaseObjectFact/DatabaseColumnFact instances GraphNodeIndex needs live in
        // the database fragment DatabaseFragmentBuilder already validated into aggregateFragments above -
        // the same source ComponentGraphProjector itself reads ComponentFact/RelationFact from.
        var nodeIndex = GraphNodeIndex.Build(
            componentResult.Components, projects, accumulated.OfType<DocumentFact>(),
            symbolFacts,
            aggregateFragments.SelectMany(static fragment => fragment.Facts.OfType<DatabaseObjectFact>()),
            aggregateFragments.SelectMany(static fragment => fragment.Facts.OfType<DatabaseColumnFact>()));
        var graph = ComponentGraphProjector.Project(aggregateFragments, nodeIndex);
        analysisDiagnostics.AddRange(graph.Diagnostics);

        resultDiagnostics.AddRange(analysisDiagnostics.Select(static diagnostic => diagnostic.Message));
        var honestCoverage = CoverageProjector.Project(new CoverageProjectionRequest(
            request.Options.Mode, accumulated, analysisDiagnostics.ToImmutable(), [], coverageOverrides.ToImmutable()));
        var snapshot = new AggregateOutputSnapshot(
            request.Topic, request.Domain, "4.0.0", request.Options.Mode, effectiveMode, request.Options.Trust,
            loadedExtensions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
            new ManifestCoverage(inventory.Services.Length, projectCount, documentCount),
            storedFragments.ToImmutable(), honestCoverage, RelationProjector.Project(aggregateFragments),
            databaseAggregate, graph);
        new CanonicalAggregateWriter().WritePrepared(request.OutputRoot, snapshot, TimeProvider.System);

        return Result(
            request,
            effectiveMode,
            structuralFailure ? 1 : 0,
            $"Analyzed {projectCount} project(s) and {documentCount} document(s); wrote {storedFragments.Count} factual fragment(s).",
            resultDiagnostics);
    }

    private async Task<ProjectAnalysisResult> AnalyzeProjectAsync(
        AnalysisRequest request,
        InventoryProject project,
        FactStore store,
        ImmutableArray<StoredFactFragment>.Builder storedFragments,
        ImmutableArray<IFact>.Builder coverageFacts,
        ImmutableArray<SymbolFact>.Builder symbolFacts,
        ImmutableArray<ScopeCoverageInput>.Builder coverageOverrides,
        ImmutableArray<AnalysisDiagnostic>.Builder analysisDiagnostics,
        List<string> resultDiagnostics,
        ImmutableArray<string>.Builder loadedExtensions,
        DatabaseClaimAccumulator databaseClaims,
        RelationClaimAccumulator relationClaims,
        CancellationToken cancellationToken)
    {
        var projectId = ProjectFactId.Create(project.RelativePath);
        var sources = ImmutableArray.CreateBuilder<SemanticProjectDocument>();
        foreach (var relativeSourcePath in project.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = File.ReadAllText(project.SourcePaths[relativeSourcePath]);
            sources.Add(new SemanticProjectDocument(
                relativeSourcePath,
                source,
                SyntaxFactExtractor.Extract(projectId, relativeSourcePath, source, _dataAccessAnalyzers)));
        }

        var syntacticProject = new ProjectFact(
            FactHeader.Create(projectId.ToFactId(), FactKind.Project, FactResolution.Syntactic, [Provenance]),
            projectId,
            project.Name,
            project.RelativePath,
            [],
            sources.Select(static source => source.Extraction.Document.DocumentId).ToImmutableArray());
        var processed = request.Options.Mode is AnalysisMode.Semantic
            ? await new TrustedSemanticProjectProcessor(
                    _evaluator, _compilationAdapter, _generatorAdapter, _observer)
                .ProcessAsync(project, syntacticProject, sources.ToImmutable(), request.Options, cancellationToken)
                .ConfigureAwait(false)
            : SyntaxOnly(syntacticProject, sources.ToImmutable());

        analysisDiagnostics.AddRange(processed.Diagnostics);
        loadedExtensions.AddRange(processed.LoadedExtensions);
        var structuralFailure = false;
        var persistedDocumentIds = ImmutableArray.CreateBuilder<DocumentFactId>();
        foreach (var document in processed.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = document.Source.RelativePath;
            ScopeStarted($"document:{relativePath}");
            try
            {
                var extraction = document.Source.Extraction;
                // DAD-18: an analyzer that failed on this document is reported whether or not the
                // document itself goes on to validate.
                analysisDiagnostics.AddRange(extraction.DatabaseDiagnostics);
                IFact[] syntacticFacts =
                [
                    extraction.Document,
                    .. extraction.Document.Sections,
                    .. extraction.Symbols,
                ];
                if (syntacticFacts.GroupBy(static fact => fact.Header.Id).Any(static group => group.Skip(1).Any()))
                {
                    var syntaxValidation = _validate(FactValidationInput.Create(
                        syntacticFacts,
                        documents: [DocumentExtent.Create(
                            extraction.Document.DocumentId,
                            relativePath,
                            LineLengths(document.Source.SourceText))],
                        knownFactIds: [projectId.ToFactId()]));
                    analysisDiagnostics.AddRange(syntaxValidation.ValidationDiagnostics);
                    if (!syntaxValidation.IsValid)
                    {
                        structuralFailure = true;
                        resultDiagnostics.AddRange(syntaxValidation.ValidationDiagnostics.Select(static diagnostic => diagnostic.Message));
                        continue;
                    }
                }

                var linkedIds = document.LinkedSymbolIds.IsEmpty ? extraction.Document.SymbolIds : document.LinkedSymbolIds;
                var documentFact = extraction.Document with { SymbolIds = linkedIds };
                var errorIds = document.EnrichedSymbols
                    .Where(static symbol => symbol.ContainsErrorSymbol)
                    .Select(static symbol => symbol.SymbolId)
                    .ToHashSet();
                var baseline = new IFact[] { documentFact }
                    .Concat(extraction.Document.Sections)
                    .Concat(extraction.Symbols.Where(symbol => !errorIds.Contains(symbol.SymbolId)));
                var enrichment = document.EnrichedSymbols.Cast<IFact>();
                var merged = FactMerger.Merge(baseline, enrichment, document.Diagnostics);
                analysisDiagnostics.AddRange(merged.StructuralDiagnostics);
                if (!merged.IsValid)
                {
                    structuralFailure = true;
                    resultDiagnostics.AddRange(merged.StructuralDiagnostics.Select(static diagnostic => diagnostic.Message));
                    continue;
                }

                var validation = _validate(FactValidationInput.Create(
                    merged.Facts,
                    diagnostics: merged.Diagnostics,
                    documents: [DocumentExtent.Create(documentFact.DocumentId, relativePath, LineLengths(document.Source.SourceText))],
                    knownFactIds: [projectId.ToFactId()]));
                analysisDiagnostics.AddRange(validation.ValidationDiagnostics);
                if (!validation.IsValid)
                {
                    structuralFailure = true;
                    resultDiagnostics.AddRange(validation.ValidationDiagnostics.Select(static diagnostic => diagnostic.Message));
                    continue;
                }

                var fragment = validation.Fragment!;
                var stored = store.Persist(fragment);
                storedFragments.Add(stored);
                persistedDocumentIds.Add(documentFact.DocumentId);
                coverageFacts.Add(fragment.Facts.OfType<DocumentFact>().Single());
                databaseClaims.Add(
                    documentFact.DocumentId,
                    relativePath,
                    LineLengths(document.Source.SourceText).ToImmutableArray(),
                    extraction.DatabaseClaims);
                // AD-018: syntax-only mode never attempted semantic refinement, so this document's
                // baseline claims (CreateClaims) are the whole story; trusted mode already merged that
                // same baseline with its refinement inside RefineClaims (T13), so EnrichedRelations is
                // complete by itself and must not be re-merged with a second CreateClaims call.
                var documentRelationClaims = document.Attempted
                    ? document.EnrichedRelations
                    : RelationCollector.CreateClaims(documentFact.DocumentId, relativePath, extraction.RelationCandidates);
                relationClaims.Add(
                    documentFact.DocumentId,
                    relativePath,
                    LineLengths(document.Source.SourceText).ToImmutableArray(),
                    documentRelationClaims);
                symbolFacts.AddRange(fragment.Facts.OfType<SymbolFact>());
                coverageOverrides.Add(new ScopeCoverageInput(
                    documentFact.DocumentId.ToFactId(), CoverageApplicability.Applicable,
                    document.Attempted ? CoverageAttempt.Attempted : CoverageAttempt.NotAttempted));
                WriteMarkdown(request.OutputRoot, relativePath, FrontmatterV2.Create(fragment, stored), MarkdownProjector.Project(fragment));
            }
            finally
            {
                ScopeCompleted($"document:{relativePath}");
            }
        }

        var projectFact = processed.Project.Project with
        {
            DocumentIds = persistedDocumentIds.Distinct().OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray(),
        };
        var projectMerge = FactMerger.Merge([projectFact, .. processed.Project.Targets], diagnostics: processed.Project.Diagnostics);
        analysisDiagnostics.AddRange(projectMerge.StructuralDiagnostics);
        if (!projectMerge.IsValid)
        {
            structuralFailure = true;
            resultDiagnostics.AddRange(projectMerge.StructuralDiagnostics.Select(static diagnostic => diagnostic.Message));
        }
        else
        {
            var validation = _validate(FactValidationInput.Create(
                projectMerge.Facts,
                diagnostics: projectMerge.Diagnostics,
                knownFactIds: persistedDocumentIds.Select(static id => id.ToFactId())));
            analysisDiagnostics.AddRange(validation.ValidationDiagnostics);
            if (validation.IsValid)
            {
                var fragment = validation.Fragment!;
                storedFragments.Add(store.Persist(fragment));
                coverageFacts.AddRange(fragment.Facts.Where(static fact => fact is ProjectFact or TargetFact));
            }
            else
            {
                structuralFailure = true;
                resultDiagnostics.AddRange(validation.ValidationDiagnostics.Select(static diagnostic => diagnostic.Message));
            }
        }

        return new ProjectAnalysisResult(processed.Documents.Length, structuralFailure, processed.AnySemanticSuccess);
    }

    private static TrustedSemanticProjectResult SyntaxOnly(
        ProjectFact project,
        ImmutableArray<SemanticProjectDocument> sources) =>
        new(
            new ProjectFactEnrichmentResult(project, [], []),
            sources.Select(static source => new SemanticProcessedDocument(source, false, [], [], [], [])).ToImmutableArray(),
            [], [], false);

    private static AnalysisResult Result(
        AnalysisRequest request,
        AnalysisMode effectiveMode,
        int exitCode,
        string summary,
        IEnumerable<string> diagnostics) =>
        new(exitCode, request.Options.Mode, effectiveMode, summary,
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray());

    private static IEnumerable<int> LineLengths(string source)
    {
        using var reader = new StringReader(source);
        string? line;
        var any = false;
        while ((line = reader.ReadLine()) is not null)
        {
            any = true;
            yield return line.Length;
        }

        if (!any || source.EndsWith('\n'))
        {
            yield return 0;
        }
    }

    private static void WriteMarkdown(
        string outputRoot,
        string relativeSourcePath,
        FrontmatterV2 frontmatter,
        string markdown)
    {
        var path = Path.Combine(
            outputRoot,
            "raw",
            "codebase",
            relativeSourcePath.Replace('/', Path.DirectorySeparatorChar)) + ".md";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, frontmatter.ToYaml() + '\n' + markdown, new UTF8Encoding(false));
    }

    private void ScopeStarted(string scope) => _observer?.ScopeStarted(scope);
    private void ScopeCompleted(string scope) => _observer?.ScopeCompleted(scope);

    private sealed record ProjectAnalysisResult(int DocumentCount, bool StructuralFailure, bool SemanticSuccess);
}
