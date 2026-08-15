using System.CommandLine;
using Csharp2Md.Core.Loading;

var manifestOption = new Option<FileInfo>("--manifest")
{
    Description = "Path to the manifest file. (T4: interpreted directly as a solution path to "
        + "prove the packaged tool's BuildHost can load a solution; superseded by the real "
        + "manifest-driven pipeline in a later task.)",
    Required = true,
};

var outputOption = new Option<DirectoryInfo>("--output")
{
    Description = "Output directory for generated Markdown.",
    Required = true,
};

var rootCommand = new RootCommand("csharp2md - convert a C#/.NET codebase to Markdown")
{
    manifestOption,
    outputOption
};

rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
    var manifestFile = parseResult.GetValue(manifestOption)!;

    var loader = new SolutionLoader();
    var loaded = await loader.LoadAsync(manifestFile.FullName, cancellationToken);

    Console.WriteLine($"Loaded {loaded.Report.Projects.Count} project(s).");
    return 0;
});

return await rootCommand.Parse(args).InvokeAsync();
