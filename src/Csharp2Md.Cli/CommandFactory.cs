using System.CommandLine;
using Csharp2Md.Core;
using CoreAnalyzeRequest = Csharp2Md.Core.AnalyzeRequest;
using CoreAnalyzeResult = Csharp2Md.Core.AnalyzeResult;
using CoreValidateRequest = Csharp2Md.Core.ValidateRequest;
using CoreValidateResult = Csharp2Md.Core.PackageValidationResult;

namespace Csharp2Md.Cli;

internal static class CommandFactory
{
    internal static RootCommand CreateRootCommand(
        Func<CoreAnalyzeRequest, CancellationToken, Task<CoreAnalyzeResult>>? analyzeAsync = null,
        Func<CoreValidateRequest, CoreValidateResult>? validatePackage = null)
    {
        var rootCommand = new RootCommand("Analyze .NET solutions into a knowledge graph.");
        var knowledgeEngine = new KnowledgeEngine();
        analyzeAsync ??= knowledgeEngine.AnalyzeAsync;
        validatePackage ??= knowledgeEngine.Validate;

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
        validate.SetAction(ValidateAction(packageOption, validatePackage));

        rootCommand.Subcommands.Add(validate);
        return rootCommand;
    }

    private static Func<ParseResult, CancellationToken, Task<int>> ValidateAction(
        Option<string> packageOption,
        Func<CoreValidateRequest, CoreValidateResult> validatePackage) =>
        (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var packageCandidate = parseResult.GetValue(packageOption);
            var stdout = parseResult.InvocationConfiguration.Output;
            if (string.IsNullOrWhiteSpace(packageCandidate))
            {
                return Task.FromResult(Invalid(parseResult, "--package"));
            }

            try
            {
                packageCandidate = Path.GetFullPath(packageCandidate);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Task.FromResult(Invalid(parseResult, $"invalid package path: {packageCandidate}"));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = validatePackage(new CoreValidateRequest(packageCandidate));
            WriteDiagnostics(result.Diagnostics, parseResult.InvocationConfiguration.Error);
            if (!result.Succeeded)
            {
                return Task.FromResult(RejectedExitCode(result.Diagnostics));
            }

            stdout.WriteLine($"Knowledge package validated and certified: {packageCandidate}");
            return Task.FromResult(ExitCodes.Success);
        };

    internal static Task<int> InvokeAsync(
        string[] args,
        Func<CoreAnalyzeRequest, CancellationToken, Task<CoreAnalyzeResult>>? analyzeAsync = null,
        Func<CoreValidateRequest, CoreValidateResult>? validatePackage = null,
        InvocationConfiguration? configuration = null) =>
        CreateRootCommand(analyzeAsync, validatePackage).Parse(args).InvokeAsync(configuration);

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
