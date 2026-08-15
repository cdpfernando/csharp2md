using System.CommandLine;
using System.ComponentModel;
using Csharp2Md.Core.Manifests;
using Csharp2Md.Core.Output;
using Csharp2Md.Core.Pipeline;

const string Usage = "Usage: csharp2md [directory] [--manifest <file>] [--output <directory>] [--force]";

var directoryArgument = new Argument<DirectoryInfo?>("directory")
{
    Description = "Directory to analyze. Defaults to the current working directory.",
    Arity = ArgumentArity.ZeroOrOne,
};

var manifestOption = new Option<FileInfo?>("--manifest")
{
    Description = "Optional manifest JSON file listing service roots to analyze.",
};

var outputOption = new Option<DirectoryInfo?>("--output", "-o")
{
    Description = "Exact output directory. Defaults to a sibling named <input>_md.",
};

var forceOption = new Option<bool>("--force")
{
    Description = "Replace a non-empty output directory not previously created by csharp2md.",
};

var rootCommand = new RootCommand("csharp2md - convert a C#/.NET codebase to Markdown");
rootCommand.Arguments.Add(directoryArgument);
rootCommand.Options.Add(manifestOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(forceOption);

rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
    var directory = parseResult.GetValue(directoryArgument);
    var manifestFile = parseResult.GetValue(manifestOption);
    var configuredOutput = parseResult.GetValue(outputOption);
    var force = parseResult.GetValue(forceOption);

    if (directory is not null && manifestFile is not null)
    {
        Console.Error.WriteLine("csharp2md: specify either a directory or --manifest, not both.");
        Console.Error.WriteLine(Usage);
        return 1;
    }

    var inputRoot = directory?.FullName
        ?? manifestFile?.Directory?.FullName
        ?? Environment.CurrentDirectory;

    if (manifestFile is null && !Directory.Exists(inputRoot))
    {
        Console.Error.WriteLine($"csharp2md: input directory does not exist or is not a directory: {inputRoot}");
        return 1;
    }

    var outputRoot = configuredOutput?.FullName ?? OutputPathResolver.DefaultForInput(inputRoot);
    if (outputRoot is null)
    {
        Console.Error.WriteLine(
            $"csharp2md: cannot derive an output name from input directory '{inputRoot}'; specify --output.");
        return 1;
    }

    PipelineRunResult result;
    try
    {
        var pipeline = new AnalysisPipeline();
        result = manifestFile is not null
            ? await pipeline.RunAsync(
                manifestFile.FullName, outputRoot, cancellationToken, forceOutput: force)
            : await pipeline.RunAsync(
                new Manifest([new ManifestEntry(inputRoot)]),
                inputRoot,
                outputRoot,
                cancellationToken,
                forceOutput: force);
    }
    catch (OutputPreparationException exception)
    {
        Console.Error.WriteLine($"csharp2md: {exception.Message}");
        return 1;
    }
    catch (Exception exception) when (CannotStartBuildHost(exception))
    {
        Console.Error.WriteLine(
            "csharp2md: could not start the Roslyn build host - the .NET SDK ('dotnet') could not be "
            + "started. Install the .NET SDK and make sure 'dotnet' is on PATH, then run csharp2md again.");
        return 1;
    }

    if (!result.IsSuccess)
    {
        Console.Error.WriteLine($"csharp2md: {result.ManifestError!.Value.Message}");
        return 1;
    }

    foreach (var warning in result.Warnings)
    {
        Console.Error.WriteLine($"csharp2md: warning: {warning}");
    }

    Console.Write(RunReporter.Summarize(result.LoadReport));
    Console.WriteLine($"Wrote {result.Graph.Edges.Count} dependency edge(s) to {outputRoot}");

    return 0;
});

return await rootCommand.Parse(args).InvokeAsync();

// MSBuildWorkspace starts exactly one external process - the BuildHost - so a Win32Exception
// anywhere in the chain means that launch failed.
static bool CannotStartBuildHost(Exception exception)
{
    for (var current = exception; current is not null; current = current.InnerException)
    {
        if (current is Win32Exception)
        {
            return true;
        }
    }

    return false;
}
