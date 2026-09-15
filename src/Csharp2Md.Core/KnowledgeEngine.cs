namespace Csharp2Md.Core;

public sealed class KnowledgeEngine
{
    public Task<AnalyzeResult> AnalyzeAsync(
        AnalyzeRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = ValidateAnalyzeRequest(request);
        if (diagnostics.Length > 0)
        {
            return Task.FromResult(new AnalyzeResult(committed: false, diagnostics));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AnalyzeResult(committed: false, ImmutableArray<EngineDiagnostic>.Empty));
    }

    public PackageValidationResult Validate(ValidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = ValidateValidateRequest(request);
        if (diagnostics.Length > 0)
        {
            return new PackageValidationResult(succeeded: false, diagnostics);
        }

        return new PackageValidationResult(succeeded: false, ImmutableArray<EngineDiagnostic>.Empty);
    }

    private static ImmutableArray<EngineDiagnostic> ValidateAnalyzeRequest(AnalyzeRequest request)
    {
        if (request.SolutionPaths.IsDefaultOrEmpty ||
            request.SolutionPaths.Any(static path => string.IsNullOrWhiteSpace(path)))
        {
            return ImmutableArray.Create(InvocationDiagnostic("solution-paths-required"));
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return ImmutableArray.Create(InvocationDiagnostic("output-directory-required"));
        }

        return ImmutableArray<EngineDiagnostic>.Empty;
    }

    private static ImmutableArray<EngineDiagnostic> ValidateValidateRequest(ValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PackageDirectory))
        {
            return ImmutableArray.Create(InvocationDiagnostic("package-directory-required"));
        }

        return ImmutableArray<EngineDiagnostic>.Empty;
    }

    private static EngineDiagnostic InvocationDiagnostic(string cause) =>
        new("invalid-request", "invocation", cause);
}

public sealed record AnalyzeRequest
{
    public ImmutableArray<string> SolutionPaths { get; }

    public string OutputDirectory { get; }

    public bool IncludeTests { get; }

    public AnalyzeRequest(
        ImmutableArray<string> solutionPaths,
        string outputDirectory,
        bool includeTests = false)
    {
        SolutionPaths = solutionPaths.IsDefault ? ImmutableArray<string>.Empty : solutionPaths;
        OutputDirectory = outputDirectory;
        IncludeTests = includeTests;
    }
}

public sealed record ValidateRequest
{
    public string PackageDirectory { get; }

    public ValidateRequest(string packageDirectory)
    {
        PackageDirectory = packageDirectory;
    }
}

public sealed record AnalyzeResult
{
    public bool Committed { get; }

    public ImmutableArray<EngineDiagnostic> Diagnostics { get; }

    public AnalyzeResult(bool committed, ImmutableArray<EngineDiagnostic> diagnostics)
    {
        Committed = committed;
        Diagnostics = diagnostics.IsDefault ? ImmutableArray<EngineDiagnostic>.Empty : diagnostics;
    }
}

public sealed record PackageValidationResult
{
    public bool Succeeded { get; }

    public ImmutableArray<EngineDiagnostic> Diagnostics { get; }

    public PackageValidationResult(bool succeeded, ImmutableArray<EngineDiagnostic> diagnostics)
    {
        Succeeded = succeeded;
        Diagnostics = diagnostics.IsDefault ? ImmutableArray<EngineDiagnostic>.Empty : diagnostics;
    }
}

public sealed record EngineDiagnostic
{
    public string Code { get; }

    public string Stage { get; }

    public string Cause { get; }

    public string? Solution { get; }

    public string? Project { get; }

    public string? Variant { get; }

    public string? Family { get; }

    public string? Artifact { get; }

    public EngineDiagnostic(
        string code,
        string stage,
        string cause,
        string? solution = null,
        string? project = null,
        string? variant = null,
        string? family = null,
        string? artifact = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentException.ThrowIfNullOrWhiteSpace(cause);
        Code = code;
        Stage = stage;
        Cause = cause;
        Solution = solution;
        Project = project;
        Variant = variant;
        Family = family;
        Artifact = artifact;
    }
}
