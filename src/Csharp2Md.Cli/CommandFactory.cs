using System.CommandLine;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Retrieval;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli;

internal static class CommandFactory
{
    internal static RootCommand CreateRootCommand(IAnalysisEngine? engine = null)
    {
        var rootCommand = new RootCommand("Analyze .NET solutions into a knowledge graph.");

        var solutionOption = new Option<string[]>("--solution")
        {
            Description = "Path to a solution to analyze. Repeat for each solution.",
            Required = true,
            Arity = ArgumentArity.OneOrMore,
        };

        var outputOption = new Option<string>("--output")
        {
            Description = "Directory that receives the factual package for each requested solution.",
            Required = true,
        };

        var allowlistOption = new Option<string[]>("--allowlist")
        {
            Description = "A document path, relative to a requested solution's authorized root, to admit even "
                + "though the supported-document policy would otherwise exclude it. Repeat for each document.",
            Arity = ArgumentArity.ZeroOrMore,
        };

        var readingBudgetOption = new Option<int?>("--reading-budget-tokens")
        {
            Description = "The declared per-scenario reading budget in tokens, used to derive the enforced "
                + $"per-artifact byte ceiling. Must be positive. Defaults to {CeilingCalculator.DefaultReadingBudgetTokens}.",
        };

        var maxFileReadsOption = new Option<int?>("--max-file-reads-per-scenario")
        {
            Description = "The declared per-scenario maximum file reads, used to derive the enforced "
                + $"per-artifact byte ceiling. Must be positive. Defaults to {CeilingCalculator.DefaultMaxFileReadsPerScenario}.",
        };

        var analyze = new Command("analyze", "Analyze one or more solutions.");
        analyze.Options.Add(solutionOption);
        analyze.Options.Add(outputOption);
        analyze.Options.Add(allowlistOption);
        analyze.Options.Add(readingBudgetOption);
        analyze.Options.Add(maxFileReadsOption);
        analyze.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var paths = parseResult.GetValue(solutionOption) ?? [];
            foreach (var path in paths)
            {
                if (!Path.Exists(path))
                {
                    return Invalid(parseResult, $"solution path does not exist: {path}");
                }
            }

            // GCPC-036/GCPC-037: the declared per-scenario budget, validated before any analysis begins so
            // a malformed value publishes nothing (GCPC-073). Absent an override, CeilingCalculator's own
            // declared defaults apply -- the same ~32 KiB ceiling PublicationPipeline.Publish now enforces
            // by default for every caller (T52 closes the T37 deferred item: this was never the live
            // default before, only ever proven under an explicit test ceiling).
            var readingBudgetTokens = parseResult.GetValue(readingBudgetOption);
            var maxFileReadsPerScenario = parseResult.GetValue(maxFileReadsOption);
            CeilingCalculation ceiling;
            try
            {
                ceiling = CeilingCalculator.Derive(
                    readingBudgetTokens ?? CeilingCalculator.DefaultReadingBudgetTokens,
                    maxFileReadsPerScenario ?? CeilingCalculator.DefaultMaxFileReadsPerScenario);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Invalid(
                    parseResult,
                    "--reading-budget-tokens and --max-file-reads-per-scenario must be positive");
            }

            var allowlist = parseResult.GetValue(allowlistOption) ?? [];

            AnalysisRequest request;
            try
            {
                request = AnalysisRequest.Create([.. paths], [.. allowlist]);
            }
            catch (ArgumentException exception)
            {
                return Invalid(parseResult, exception.Message);
            }

            var analysisEngine = engine;
            if (analysisEngine is null)
            {
                var outputPath = parseResult.GetValue(outputOption);
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    return Invalid(parseResult, "--output");
                }

                // A BatchComposer alongside the projector: without it, FilesystemTransactionalStore's own
                // composer field stays null, PublicationPipeline.Publish's composer.Contribute branch never
                // runs, and PublishBatch's composer?.Compose(view) is permanently []. That left cross-
                // solution composition dead on every real `analyze` run -- proven only by tests that built
                // their own store with a composer directly. Found while proving T51's "compose reproduces
                // the same batch artifacts analyze produced" against a real multi-solution batch.
                //
                // PackageProjector's own ceiling is passed the same derived bytes as the store: GCPC-039
                // names catalogs and postings alongside facts, observations and relations as artifacts that
                // must split under the ceiling, so the projector cannot keep sharding at its own unrelated
                // 1 MiB default (ShardWriter.DefaultCeilingBytes) once the store enforces the real one.
                analysisEngine = new AnalysisEngine(
                    new FilesystemTransactionalStore(
                        outputPath,
                        new PackageProjector(ceiling.CeilingBytes),
                        new BatchComposer(),
                        readingBudgetTokens,
                        maxFileReadsPerScenario,
                        [.. allowlist]));
            }

            AnalysisResult result;
            try
            {
                result = await analysisEngine.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException exception)
            {
                return Invalid(parseResult, exception.Message);
            }

            var stdout = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;

            WriteDiagnostics(result, error);
            stdout.WriteLine(FormatSummary(result));
            return result.HasUnpublishedSolution || result.HasBatchPublicationFailure
                ? ExitCodes.PartialComposition
                : ExitCodes.Success;
        });

        rootCommand.Subcommands.Add(analyze);

        var packageOption = new Option<string>("--package")
        {
            Description = "Path to an already-published package directory to validate.",
            Required = true,
        };

        var validate = new Command("validate", "Re-validate an already-published package, touching no solution.");
        validate.Options.Add(packageOption);
        validate.SetAction(ValidateAction(packageOption));

        rootCommand.Subcommands.Add(validate);

        var composePackagesOption = new Option<string[]>("--package")
        {
            Description = "Path to an already-published package directory to include in the batch. Repeat for each solution.",
            Required = true,
            Arity = ArgumentArity.OneOrMore,
        };

        var composeOutputOption = new Option<string>("--output")
        {
            Description = "The root every named package already lives under; receives batch-manifest.json and composition/.",
            Required = true,
        };

        var compose = new Command("compose", "Recompose published packages into a batch, touching no solution.");
        compose.Options.Add(composePackagesOption);
        compose.Options.Add(composeOutputOption);
        compose.SetAction(ComposeAction(composePackagesOption, composeOutputOption));

        rootCommand.Subcommands.Add(compose);
        return rootCommand;
    }

    /// <summary>
    /// GCPC-068: rebuilds each named package's contribution through <see cref="ContributionReader"/> (no
    /// solution opened, no re-analysis) and reuses <see cref="BatchComposer.Compose"/> and <see
    /// cref="FilesystemTransactionalStore.PublishBatch"/> unchanged -- the exact write path a live
    /// <c>analyze</c> batch uses, driven by a seeded contribution instead of one a live commit produced. A
    /// named package with no manifest is treated as unpublished (GCPC-072): the batch declares incomplete
    /// scope and every already-committed package stays untouched, exactly as a live analyze batch does.
    /// </summary>
    private static Func<ParseResult, CancellationToken, Task<int>> ComposeAction(
        Option<string[]> packagesOption, Option<string> outputOption) =>
        (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var packagePaths = parseResult.GetValue(packagesOption) ?? [];
            var outputPath = parseResult.GetValue(outputOption);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Task.FromResult(Invalid(parseResult, "--output"));
            }

            var composer = new BatchComposer();
            var store = new FilesystemTransactionalStore(outputPath, composer: composer);
            var solutions = ImmutableArray.CreateBuilder<BatchSolutionRecord>(packagePaths.Length);
            foreach (var packagePath in packagePaths)
            {
                if (File.Exists(Path.Combine(packagePath, "manifest.json")))
                {
                    var manifest = PackageValidator.ReadPayloadOrThrow<ManifestEnvelope>(
                        File.ReadAllBytes(Path.Combine(packagePath, "manifest.json")), "manifest.json");
                    var coordinate = SolutionCoordinate.For(manifest.SolutionFileName);
                    solutions.Add(new BatchSolutionRecord(coordinate.Identity, manifest.SolutionFileName, PublicationStatus.Committed, null));
                    store.SeedContribution(coordinate.Identity.Value, ContributionReader.Read(packagePath, composer));
                }
                else
                {
                    var fallbackName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(packagePath)));
                    var coordinate = SolutionCoordinate.For(fallbackName);
                    solutions.Add(new BatchSolutionRecord(coordinate.Identity, fallbackName, PublicationStatus.Unpublished, "package-missing"));
                }
            }

            var stdout = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;
            try
            {
                store.PublishBatch(solutions.ToImmutable());
            }
            catch (PublicationRejectedException exception)
            {
                error.WriteLine($"csharp2md: {exception.Message}");
                return Task.FromResult(ExitCodes.StructuralCorruption);
            }

            var incomplete = solutions.Any(static record => record.Status == PublicationStatus.Unpublished);
            stdout.WriteLine(incomplete ? "Composition incomplete: at least one solution is unpublished." : "Composition complete.");
            return Task.FromResult(incomplete ? ExitCodes.PartialComposition : ExitCodes.Success);
        };

    /// <summary>
    /// GCPC-063, GCPC-064, GCPC-066, GCPC-067: re-hydrates the package named by <paramref name="packageOption"/>
    /// and re-runs the same validators that wrote it -- <see cref="PackageValidator.ValidatePackageDirectory"/>
    /// (manifest cardinality and provenance compatibility), <see cref="FactualPackageReader"/> (fact-level
    /// hashes, identities, registered kinds) and <see cref="ProjectionValidator"/> (dangling projection
    /// references, out-of-range ordinals, out-of-bounds locators) -- then reports the package's already-
    /// published certification status. AD-025: no new validator is introduced. Never a solution, a project
    /// or semantic analysis, and never a recomputed status. A plain method (not an <c>async</c> lambda)
    /// because every step here is synchronous; wrapping it in <c>async</c> with no <c>await</c> would be a
    /// build warning under this project's <c>TreatWarningsAsErrors</c>.
    /// </summary>
    private static Func<ParseResult, CancellationToken, Task<int>> ValidateAction(Option<string> packageOption) =>
        (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var packagePath = parseResult.GetValue(packageOption);
            var stdout = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;

            if (string.IsNullOrWhiteSpace(packagePath)
                || !File.Exists(Path.Combine(packagePath, "manifest.json")))
            {
                return Task.FromResult(Invalid(parseResult, $"not a published package: {packagePath}"));
            }

            PackageReadResult result;
            try
            {
                PackageValidator.ValidatePackageDirectory(packagePath);
                result = FactualPackageReader.Read(packagePath);

                var manifest = PackageValidator.ReadPayloadOrThrow<ManifestEnvelope>(
                    File.ReadAllBytes(Path.Combine(packagePath, "manifest.json")), "manifest.json");
                var context = new ManifestContext(manifest.SolutionKey, manifest.SolutionFileName);
                var document = DomainMapper.ToWire(result.Snapshot, context);
                // T52: re-plan with the real ceiling this package was actually published under (its own
                // provenance), not the unsplit default -- a sharded family's citations only resolve when
                // this reconstruction buckets records exactly the way the live publish did.
                var ceilingBytes = manifest.Provenance?.ArtifactCeilingBytes ?? int.MaxValue;
                var view = PublishedPackageView.From(document, LayoutPlanner.Plan(document, ceilingBytes));
                ProjectionValidator.Validate(view, result.Projections);
            }
            catch (PublicationRejectedException exception)
            {
                error.WriteLine($"csharp2md: {exception.Message}");
                return Task.FromResult(
                    exception.Gate == "incompatible-provenance"
                        ? ExitCodes.IncompatibleProvenance
                        : ExitCodes.StructuralCorruption);
            }

            stdout.WriteLine($"Certification: {result.Certification.Status}");
            ReportRetrievalScenarios(packagePath, stdout);

            return Task.FromResult(result.Certification.Status switch
            {
                "passed" => ExitCodes.Success,
                "degraded" => ExitCodes.Degraded,
                _ => ExitCodes.CertificationFailed,
            });
        };

    /// <summary>
    /// GCPC-052..GCPC-054's documented scenarios, walked against the package already on disk and reported
    /// -- never rewritten, since `validate` touches no solution and republishes nothing (GCPC-064,
    /// GCPC-067). Skipped when the package carries no `retrieval.md` (a package built without the real
    /// projection stage, such as a Storage-layer test fixture).
    /// </summary>
    private static void ReportRetrievalScenarios(string packagePath, TextWriter stdout)
    {
        if (!File.Exists(Path.Combine(packagePath, "retrieval.md")))
        {
            return;
        }

        var report = RetrievalScenarioRunner.Run(new PackageDirectoryArtifactSource(packagePath));
        foreach (var scenario in report.Scenarios)
        {
            var status = !scenario.Exercised
                ? "not exercised"
                : scenario.Reached
                    ? "reached"
                    : $"failed: {scenario.FailureReason}";
            stdout.WriteLine($"Scenario {scenario.Name}: {status}");
        }
    }

    internal static Task<int> InvokeAsync(
        string[] args,
        IAnalysisEngine? engine = null,
        InvocationConfiguration? configuration = null) =>
        CreateRootCommand(engine).Parse(args).InvokeAsync(configuration);

    internal static int Invalid(ParseResult parseResult, string message)
    {
        parseResult.InvocationConfiguration.Error.WriteLine($"csharp2md: {message}");
        return ExitCodes.InvalidInvocation;
    }

    private static void WriteDiagnostics(AnalysisResult result, TextWriter error)
    {
        foreach (var outcome in result.Solutions)
        {
            if (outcome.Status is not PublicationStatus.Unpublished)
            {
                continue;
            }

            var detail = outcome.FailingStage is { Length: > 0 } stage
                ? $" unpublished at {stage}"
                : " unpublished";
            if (outcome.StructuralCorruption)
            {
                detail += " (structural corruption)";
            }

            if (outcome.Detail is { Length: > 0 } named)
            {
                detail += $" {named}";
            }

            error.WriteLine($"csharp2md:{detail} {outcome.LogicalRelativePath}");
        }

        if (result.HasBatchPublicationFailure)
        {
            var reason = result.BatchPublicationGate ?? "batch";
            var message = result.BatchPublicationDetail is { Length: > 0 } named
                ? $"{reason}: {named}"
                : reason;
            error.WriteLine($"csharp2md: {message}");
        }
    }

    private static string FormatSummary(AnalysisResult result)
    {
        var lines = result.Solutions.Select(static outcome =>
        {
            var facts = outcome.Stages.Sum(static stage => stage.FactCount);
            var observations = outcome.Stages.Sum(static stage => stage.ObservationCount);
            var relations = outcome.Stages.Sum(static stage => stage.RelationCount);
            return $"{outcome.LogicalRelativePath}: {outcome.Status}; facts={facts} observations={observations} relations={relations}";
        });

        return "Analysis complete." + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}
