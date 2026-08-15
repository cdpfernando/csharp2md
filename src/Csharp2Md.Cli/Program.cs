using System.CommandLine;
using System.ComponentModel;
using Csharp2Md.Core.Pipeline;

var manifestOption = new Option<FileInfo>("--manifest")
{
    Description = "Path to the manifest JSON file listing the service roots to analyze.",
    Required = true,
};

var outputOption = new Option<DirectoryInfo>("--output")
{
    Description = "Output directory for generated Markdown. Existing content is regenerated in full.",
    Required = true,
};

var rootCommand = new RootCommand("csharp2md - convert a C#/.NET codebase to Markdown")
{
    manifestOption,
    outputOption
};

rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
    var manifest = parseResult.GetValue(manifestOption)!;
    var output = parseResult.GetValue(outputOption)!;

    PipelineRunResult result;
    try
    {
        result = await new AnalysisPipeline().RunAsync(manifest.FullName, output.FullName, cancellationToken);
    }
    catch (Exception exception) when (CannotStartBuildHost(exception))
    {
        // design.md Error Handling: the Roslyn BuildHost launches `dotnet` out of process, and a
        // machine without it on PATH must get an actionable message rather than a raw stack trace.
        Console.Error.WriteLine(
            "csharp2md: could not start the Roslyn build host — the .NET SDK ('dotnet') could not be "
            + "started. Install the .NET SDK and make sure 'dotnet' is on PATH, then run csharp2md again.");
        return 1;
    }

    if (!result.IsSuccess)
    {
        // P1-16: the specific problem, and nothing was written.
        Console.Error.WriteLine($"csharp2md: {result.ManifestError!.Value.Message}");
        return 1;
    }

    foreach (var warning in result.Warnings)
    {
        Console.Error.WriteLine($"csharp2md: warning: {warning}");
    }

    Console.Write(RunReporter.Summarize(result.LoadReport)); // P1-10
    Console.WriteLine($"Wrote {result.Graph.Edges.Count} dependency edge(s) to {output.FullName}");

    // P3-05: a run that completed is a success, even if some projects were degraded.
    return 0;
});

return await rootCommand.Parse(args).InvokeAsync();

// MSBuildWorkspace starts exactly one external process — the BuildHost — so a Win32Exception
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
