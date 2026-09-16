using System.CommandLine;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Core;
using Csharp2Md.Projection;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Retrieval;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;
using CoreAnalyzeRequest = Csharp2Md.Core.AnalyzeRequest;
using CoreAnalyzeResult = Csharp2Md.Core.AnalyzeResult;

namespace Csharp2Md.Cli;

internal static class CommandFactory
{
    internal static RootCommand CreateRootCommand(
        Func<CoreAnalyzeRequest, CancellationToken, Task<CoreAnalyzeResult>>? analyzeAsync = null)
    {
        var rootCommand = new RootCommand("Analyze .NET solutions into a knowledge graph.");
        var knowledgeEngine = new KnowledgeEngine();
        analyzeAsync ??= knowledgeEngine.AnalyzeAsync;

        var solutionOption = new Option<string[]>("--solution")
        {
            Description = "Path to a solution to analyze. Repeat for each solution.",
            Required = true,
            Arity = ArgumentArity.OneOrMore,
        };

        var outputOption = new Option<string>("--output")
        {
            Description = "Directory that receives the committed multi-solution knowledge package.",
            Required = true,
        };

        var includeTestsOption = new Option<bool>("--include-tests")
        {
            Description = "Include test projects and documents in analysis and record that policy in the package identity.",
        };

        var analyze = new Command("analyze", "Analyze one or more solutions.");
        analyze.Options.Add(solutionOption);
        analyze.Options.Add(outputOption);
        analyze.Options.Add(includeTestsOption);
        analyze.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var paths = parseResult.GetValue(solutionOption) ?? [];
            var normalizedPaths = ImmutableArray.CreateBuilder<string>(paths.Length);
            var seen = new HashSet<string>(CanonicalPathComparer);
            foreach (var candidate in paths)
            {
                string path;
                try
                {
                    path = Path.GetFullPath(candidate);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    return Invalid(parseResult, $"invalid solution path: {candidate}");
                }

                if (!File.Exists(path))
                {
                    return Invalid(parseResult, $"solution path does not exist: {path}");
                }

                if (!seen.Add(path))
                {
                    return Invalid(parseResult, $"solution path is specified more than once: {path}");
                }

                normalizedPaths.Add(path);
            }

            var outputCandidate = parseResult.GetValue(outputOption);
            if (string.IsNullOrWhiteSpace(outputCandidate))
            {
                return Invalid(parseResult, "--output");
            }

            string outputPath;
            try
            {
                outputPath = Path.GetFullPath(outputCandidate);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Invalid(parseResult, $"invalid output path: {outputCandidate}");
            }

            if (File.Exists(outputPath))
            {
                return Invalid(parseResult, $"output path is a file: {outputPath}");
            }

            var request = new CoreAnalyzeRequest(
                normalizedPaths.ToImmutable(),
                outputPath,
                parseResult.GetValue(includeTestsOption));
            var result = await analyzeAsync(request, cancellationToken).ConfigureAwait(false);
            var stdout = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;

            WriteDiagnostics(result.Diagnostics, error);
            if (!result.Committed)
            {
                return RejectedExitCode(result.Diagnostics);
            }

            stdout.WriteLine($"Knowledge package committed and certified: {outputPath}");
            return ExitCodes.Success;
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
                // Re-plan with the real ceiling this package was actually published under (its own
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
        Func<CoreAnalyzeRequest, CancellationToken, Task<CoreAnalyzeResult>>? analyzeAsync = null,
        InvocationConfiguration? configuration = null) =>
        CreateRootCommand(analyzeAsync).Parse(args).InvokeAsync(configuration);

    internal static int Invalid(ParseResult parseResult, string message)
    {
        parseResult.InvocationConfiguration.Error.WriteLine($"csharp2md: {message}");
        return ExitCodes.InvalidInvocation;
    }

    private static void WriteDiagnostics(ImmutableArray<EngineDiagnostic> diagnostics, TextWriter error)
    {
        foreach (var diagnostic in diagnostics)
        {
            var fields = new List<string>
            {
                $"code={diagnostic.Code}",
                $"stage={diagnostic.Stage}",
                $"cause={diagnostic.Cause}",
            };
            AddCoordinate(fields, "solution", diagnostic.Solution);
            AddCoordinate(fields, "project", diagnostic.Project);
            AddCoordinate(fields, "variant", diagnostic.Variant);
            AddCoordinate(fields, "family", diagnostic.Family);
            AddCoordinate(fields, "artifact", diagnostic.Artifact);
            error.WriteLine($"csharp2md: {string.Join(' ', fields)}");
        }
    }

    private static void AddCoordinate(List<string> fields, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add($"{name}={value}");
        }
    }

    private static int RejectedExitCode(ImmutableArray<EngineDiagnostic> diagnostics) =>
        diagnostics.Any(static diagnostic => string.Equals(diagnostic.Stage, "invocation", StringComparison.Ordinal))
            ? ExitCodes.InvalidInvocation
            : diagnostics.Any(static diagnostic => string.Equals(diagnostic.Stage, "certification", StringComparison.Ordinal))
                ? ExitCodes.CertificationFailed
                : ExitCodes.StructuralCorruption;

    private static StringComparer CanonicalPathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
