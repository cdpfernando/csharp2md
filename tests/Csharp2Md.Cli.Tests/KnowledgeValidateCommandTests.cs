using System.CommandLine;
using System.CommandLine.Help;
using Csharp2Md.Cli;
using Csharp2Md.Core;

namespace Csharp2Md.Cli.Tests;

public sealed class KnowledgeValidateCommandTests
{
    [Fact]
    public void Root_ExposesOnlyAnalyzeAndValidateCommands()
    {
        var names = CommandFactory.CreateRootCommand().Subcommands
            .Select(static command => command.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["analyze", "validate"], names);
    }

    [Fact]
    public void Validate_ExposesOnlyRequiredPackageOption()
    {
        var validate = ValidateCommand();
        var options = validate.Options
            .Where(static option => option is not HelpOption and not VersionOption)
            .ToArray();

        var package = Assert.Single(options);
        Assert.Equal("--package", package.Name);
        Assert.True(package.Required);
        Assert.Equal(ArgumentArity.ExactlyOne, package.Arity);
        Assert.Empty(validate.Arguments);
    }

    [Fact]
    public async Task Validate_WithoutPackage_IsRejectedBeforeCore()
    {
        var recorder = new ValidateRecorder(Succeeded());

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(["validate"], validatePackage: recorder.Validate);

        Assert.Equal(ExitCodes.InvalidInvocation, exitCode);
        Assert.Contains("--package", stderr, StringComparison.Ordinal);
        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task Validate_Success_ForwardsNormalizedPackageToCoreOnce()
    {
        var recorder = new ValidateRecorder(Succeeded());
        var package = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "..");

        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(
            ["validate", "--package", package],
            validatePackage: recorder.Validate);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(Path.GetFullPath(package), Assert.Single(recorder.Requests).PackageDirectory);
        Assert.Contains("validated and certified", stdout, StringComparison.Ordinal);
        Assert.Contains(Path.GetFullPath(package), stdout, StringComparison.Ordinal);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public async Task Validate_Success_DoesNotMutateThePackageDirectory()
    {
        var package = CliTestPaths.UniqueOutputPath();
        Directory.CreateDirectory(package);
        var sentinel = Path.Combine(package, "sentinel.txt");
        var before = new byte[] { 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(sentinel, before);
        try
        {
            var recorder = new ValidateRecorder(Succeeded());

            var (exitCode, _, _) = await CliInvoke.RunAsync(
                ["validate", "--package", package],
                validatePackage: recorder.Validate);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(before, await File.ReadAllBytesAsync(sentinel));
            Assert.Equal(Path.GetFullPath(package), Assert.Single(recorder.Requests).PackageDirectory);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(package);
        }
    }

    [Theory]
    [InlineData("invocation", ExitCodes.InvalidInvocation)]
    [InlineData("certification", ExitCodes.CertificationFailed)]
    [InlineData("validation", ExitCodes.StructuralCorruption)]
    public async Task Validate_MapsCoreRejectionStagesToCurrentExitCodes(
        string stage,
        int expectedExitCode)
    {
        var recorder = new ValidateRecorder(Failed(new EngineDiagnostic("package-invalid", stage, "specified-cause")));

        var (exitCode, stdout, _) = await CliInvoke.RunAsync(
            ["validate", "--package", CliTestPaths.UniqueOutputPath()],
            validatePackage: recorder.Validate);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Single(recorder.Requests);
    }

    [Fact]
    public async Task Validate_ReportsAllStructuredDiagnosticCoordinates()
    {
        var recorder = new ValidateRecorder(Failed(new EngineDiagnostic(
            "package-corruption",
            "validation",
            "hash-mismatch",
            solution: "orders",
            project: "src/Orders.csproj",
            variant: "net10.0",
            family: "bulk",
            artifact: "bulk/000001.json")));

        var (_, _, stderr) = await CliInvoke.RunAsync(
            ["validate", "--package", CliTestPaths.UniqueOutputPath()],
            validatePackage: recorder.Validate);

        Assert.Contains("code=package-corruption", stderr, StringComparison.Ordinal);
        Assert.Contains("stage=validation", stderr, StringComparison.Ordinal);
        Assert.Contains("cause=hash-mismatch", stderr, StringComparison.Ordinal);
        Assert.Contains("solution=orders", stderr, StringComparison.Ordinal);
        Assert.Contains("project=src/Orders.csproj", stderr, StringComparison.Ordinal);
        Assert.Contains("variant=net10.0", stderr, StringComparison.Ordinal);
        Assert.Contains("family=bulk", stderr, StringComparison.Ordinal);
        Assert.Contains("artifact=bulk/000001.json", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCommand_DoesNotContainComposeOrCompatibilityDispatch()
    {
        var source = File.ReadAllText(Path.Combine(
            CliTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Cli",
            "CommandFactory.cs"));

        Assert.DoesNotContain("ComposeAction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BatchComposer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IncompatibleProvenance", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Csharp2Md.Analysis", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Csharp2Md.Storage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Csharp2Md.Projection", source, StringComparison.Ordinal);
    }

    private static Command ValidateCommand() =>
        Assert.Single(CommandFactory.CreateRootCommand().Subcommands, static command => command.Name == "validate");

    private static PackageValidationResult Succeeded() => new(succeeded: true, diagnostics: []);

    private static PackageValidationResult Failed(params EngineDiagnostic[] diagnostics) =>
        new(succeeded: false, [.. diagnostics]);

    private sealed class ValidateRecorder
    {
        private readonly PackageValidationResult _result;

        internal List<ValidateRequest> Requests { get; } = [];

        internal ValidateRecorder(PackageValidationResult result) => _result = result;

        internal PackageValidationResult Validate(ValidateRequest request)
        {
            Requests.Add(request);
            return _result;
        }
    }
}
