using System.CommandLine;
using Csharp2Md.Analysis;
using Csharp2Md.Storage;

namespace Csharp2Md.Cli;

internal static class CommandFactory
{
    internal static RootCommand CreateRootCommand(IAnalysisEngine? engine = null)
    {
        var analysisEngine = engine ?? new AnalysisEngine(new InMemoryTransactionalStore());

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

            var result = await analysisEngine.AnalyzeAsync(request, cancellationToken).ConfigureAwait(false);
            var output = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;

            WriteDiagnostics(result, error);
            output.WriteLine(FormatSummary(result));
            return result.HasUnpublishedSolution ? 2 : 0;
        });

        rootCommand.Subcommands.Add(analyze);
        return rootCommand;
    }

    internal static Task<int> InvokeAsync(
        string[] args,
        IAnalysisEngine? engine = null,
        InvocationConfiguration? configuration = null) =>
        CreateRootCommand(engine).Parse(args).InvokeAsync(configuration);

    internal static int Invalid(ParseResult parseResult, string message)
    {
        parseResult.InvocationConfiguration.Error.WriteLine($"csharp2md: {message}");
        return 1;
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

            error.WriteLine($"csharp2md:{detail} {outcome.LogicalRelativePath}");
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
