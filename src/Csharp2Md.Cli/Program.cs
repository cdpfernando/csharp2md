using System.CommandLine;
using System.Globalization;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Output;
using Csharp2Md.Core.Topic;

const string Usage =
    "Usage: csharp2md [directory] [--manifest <file>] [--output <directory>] [--force] "
    + "[--topic <slug>] [--domain <slug>] [--analysis <syntax-only|semantic>] "
    + "[--trust <untrusted|trusted-solution>] [--include-source-generators] [--analysis-timeout <duration>]";

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
var analysisOption = new Option<string?>("--analysis")
{
    Description = "Analysis mode: syntax-only (default) or semantic.",
};
var trustOption = new Option<string?>("--trust")
{
    Description = "Input trust: untrusted (default) or trusted-solution.",
};
var generatorsOption = new Option<bool>("--include-source-generators")
{
    Description = "Run source generators in trusted semantic mode.",
};
var timeoutOption = new Option<string?>("--analysis-timeout")
{
    Description = "Positive per-service analysis timeout (default 00:10:00).",
};

var rootCommand = new RootCommand("csharp2md - convert a C#/.NET codebase to Markdown");
rootCommand.Arguments.Add(directoryArgument);
rootCommand.Options.Add(manifestOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(forceOption);
rootCommand.Options.Add(topicOption);
rootCommand.Options.Add(domainOption);
rootCommand.Options.Add(analysisOption);
rootCommand.Options.Add(trustOption);
rootCommand.Options.Add(generatorsOption);
rootCommand.Options.Add(timeoutOption);

var consoleObserver = new ConsoleProgressObserver();

rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
    var directory = parseResult.GetValue(directoryArgument);
    var manifestFile = parseResult.GetValue(manifestOption);
    var configuredOutput = parseResult.GetValue(outputOption);
    var force = parseResult.GetValue(forceOption);

    if (directory is not null && manifestFile is not null)
    {
        return Invalid("specify either a directory or --manifest, not both.", includeUsage: true);
    }

    var inputRoot = directory?.FullName ?? manifestFile?.Directory?.FullName ?? Environment.CurrentDirectory;
    if (manifestFile is null && !Directory.Exists(inputRoot))
    {
        return Invalid($"input directory does not exist or is not a directory: {inputRoot}");
    }

    if (manifestFile is not null && !manifestFile.Exists)
    {
        return Invalid($"manifest file does not exist: {manifestFile.FullName}");
    }

    var outputRoot = configuredOutput?.FullName ?? OutputPathResolver.DefaultForInput(inputRoot);
    if (outputRoot is null)
    {
        return Invalid($"cannot derive an output name from input directory '{inputRoot}'; specify --output.");
    }

    var topicResult = TopicOptions.Create(
        parseResult.GetValue(topicOption),
        parseResult.GetValue(domainOption),
        inputRoot);
    if (!topicResult.IsSuccess)
    {
        return Invalid(topicResult.Error!);
    }

    if (!TryAnalysisMode(parseResult.GetValue(analysisOption), out var analysisMode))
    {
        return Invalid("--analysis must be 'syntax-only' or 'semantic'.");
    }

    if (!TryTrustMode(parseResult.GetValue(trustOption), out var trustMode))
    {
        return Invalid("--trust must be 'untrusted' or 'trusted-solution'.");
    }

    if (!TryTimeout(parseResult.GetValue(timeoutOption), out var timeout))
    {
        return Invalid("--analysis-timeout must be a positive duration.");
    }

    var requestResult = AnalysisRequest.Create(
        manifestFile?.FullName ?? inputRoot,
        outputRoot,
        force,
        new AnalysisOptions
        {
            Mode = analysisMode,
            Trust = trustMode,
            IncludeSourceGenerators = parseResult.GetValue(generatorsOption),
            ServiceTimeout = timeout,
        },
        topicResult.Options!.Topic,
        topicResult.Options.Domain);
    if (!requestResult.IsSuccess)
    {
        return Invalid(requestResult.Message!);
    }

    var result = await new AnalysisEngine(consoleObserver).AnalyzeAsync(requestResult.Request!, cancellationToken);
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine($"csharp2md: {diagnostic}");
    }

    Console.WriteLine(result.Summary);
    Console.WriteLine($"Output topic path: {Path.GetFullPath(outputRoot)}");
    return result.ExitCode;
});

return await rootCommand.Parse(args).InvokeAsync();

static int Invalid(string message, bool includeUsage = false)
{
    Console.Error.WriteLine($"csharp2md: {message}");
    if (includeUsage)
    {
        Console.Error.WriteLine(Usage);
    }

    return 1;
}

static bool TryAnalysisMode(string? value, out AnalysisMode mode)
{
    mode = value switch
    {
        null or "syntax-only" => AnalysisMode.SyntaxOnly,
        "semantic" => AnalysisMode.Semantic,
        _ => default,
    };
    return value is null or "syntax-only" or "semantic";
}

static bool TryTrustMode(string? value, out TrustMode mode)
{
    mode = value switch
    {
        null or "untrusted" => TrustMode.Untrusted,
        "trusted-solution" => TrustMode.TrustedSolution,
        _ => default,
    };
    return value is null or "untrusted" or "trusted-solution";
}

static bool TryTimeout(string? value, out TimeSpan timeout)
{
    timeout = TimeSpan.FromMinutes(AnalysisRequest.DefaultServiceTimeoutMinutes);
    return value is null
        || (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out timeout) && timeout > TimeSpan.Zero);
}

internal sealed class ConsoleProgressObserver : Csharp2Md.Core.Analysis.IAnalysisEngineObserver
{
    public void ScopeStarted(string scope)
    {
        Console.Error.WriteLine($"[csharp2md] Analyzing {scope}");
    }

    public void ScopeCompleted(string scope)
    {
        Console.Error.WriteLine($"[csharp2md] Completed {scope}");
    }
}
