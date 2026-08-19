using System.CommandLine;
using System.ComponentModel;
using System.Reflection;
using Csharp2Md.Core.Manifests;
using Csharp2Md.Core.Output;
using Csharp2Md.Core.Pipeline;
using Csharp2Md.Core.Topic;

const string Usage =
    "Usage: csharp2md [directory] [--manifest <file>] [--output <directory>] [--force] "
    + "[--topic <slug>] [--domain <slug>]";

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

var topicOption = new Option<string?>("--topic")
{
    Description = "Slug identifying the generated topic. Defaults to the slug of the input directory name.",
};

var domainOption = new Option<string?>("--domain")
{
    Description = "Slug identifying the topic's domain. Defaults to 'system-design'.",
};

var rootCommand = new RootCommand("csharp2md - convert a C#/.NET codebase to Markdown");
rootCommand.Arguments.Add(directoryArgument);
rootCommand.Options.Add(manifestOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(forceOption);
rootCommand.Options.Add(topicOption);
rootCommand.Options.Add(domainOption);

rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
    var directory = parseResult.GetValue(directoryArgument);
    var manifestFile = parseResult.GetValue(manifestOption);
    var configuredOutput = parseResult.GetValue(outputOption);
    var force = parseResult.GetValue(forceOption);
    var topic = parseResult.GetValue(topicOption);
    var domain = parseResult.GetValue(domainOption);

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

    // WIKI-14: rejected before anything touches outputRoot - no OutputWriter.PrepareRun has run yet.
    var topicOptionsResult = TopicOptions.Create(topic, domain, inputRoot);
    if (!topicOptionsResult.IsSuccess)
    {
        Console.Error.WriteLine($"csharp2md: {topicOptionsResult.Error}");
        return 1;
    }

    var topicOptions = topicOptionsResult.Options!;

    PipelineRunResult result;
    try
    {
        var pipeline = new AnalysisPipeline();
        result = manifestFile is not null
            ? await pipeline.RunAsync(
                manifestFile.FullName, outputRoot, cancellationToken, forceOutput: force, topicOptions: topicOptions)
            : await pipeline.RunAsync(
                new Manifest([new ManifestEntry(inputRoot)]),
                inputRoot,
                outputRoot,
                cancellationToken,
                forceOutput: force,
                topicOptions: topicOptions);
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

    // WIKI-10/WIKI-11/WIKI-18..22: the topic scaffold and run log are written for every successful
    // manifest run (including one that will still exit 1 on frontmatter validation failures below -
    // "the rest of the topic is still generated", design.md), never for a failed manifest (nothing
    // was written at all in that case, handled by the !result.IsSuccess branch above).
    var toolVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    TopicScaffoldWriter.Write(outputRoot, topicOptions, toolVersion);

    var absoluteOutputRoot = Path.GetFullPath(outputRoot);
    RunLogWriter.Write(
        outputRoot,
        new RunLogData(
            BuildInvocation(directory?.FullName, manifestFile, outputRoot, force, topicOptions),
            result.DocumentCount,
            result.Graph.Edges.Count,
            result.ServiceCount,
            result.FrontmatterFailures,
            absoluteOutputRoot),
        TimeProvider.System);

    foreach (var failure in result.FrontmatterFailures)
    {
        Console.Error.WriteLine($"csharp2md: {failure.SourcePath}: {failure.Error}");
    }

    Console.Write(RunReporter.Summarize(result.LoadReport));
    // WIKI-17: document count, frontmatter validation failure count, and the output topic path.
    Console.WriteLine(
        $"Wrote {result.DocumentCount} document(s) and {result.Graph.Edges.Count} dependency edge(s) to {outputRoot}");
    Console.WriteLine($"Frontmatter validation failures: {result.FrontmatterFailures.Count}");
    Console.WriteLine($"Output topic path: {absoluteOutputRoot}");

    return result.ExitCode;
});

return await rootCommand.Parse(args).InvokeAsync();

// WIKI-18: reconstructed from the resolved arguments (defaults already applied), not the raw argv -
// so a zero-config run's log.md still records the topic/domain/output it actually used.
static string BuildInvocation(
    string? directoryArg, FileInfo? manifestFile, string outputRoot, bool force, TopicOptions options)
{
    var parts = new List<string> { "csharp2md" };

    if (manifestFile is not null)
    {
        parts.Add($"--manifest \"{manifestFile.FullName}\"");
    }
    else if (directoryArg is not null)
    {
        parts.Add($"\"{directoryArg}\"");
    }

    parts.Add($"--output \"{outputRoot}\"");
    parts.Add($"--topic {options.Topic}");
    parts.Add($"--domain {options.Domain}");

    if (force)
    {
        parts.Add("--force");
    }

    return string.Join(' ', parts);
}

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
