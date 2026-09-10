using System.CommandLine;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
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

        var analyze = new Command("analyze", "Analyze one or more solutions.");
        analyze.Options.Add(solutionOption);
        analyze.Options.Add(outputOption);
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

            AnalysisRequest request;
            try
            {
                request = AnalysisRequest.Create([.. paths]);
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

                analysisEngine = new AnalysisEngine(new FilesystemTransactionalStore(outputPath, new PackageProjector()));
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
        return rootCommand;
    }

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
                // The unsplit default ceiling: the live `analyze` pipeline plans every family this way too
                // until T52 makes the derived ceiling the enforced default. When that lands, this must plan
                // with the same real ceiling `analyze` used (from the package's own published provenance),
                // or a sharded package's projection citations will not resolve against this reconstruction.
                var view = PublishedPackageView.From(document);
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
