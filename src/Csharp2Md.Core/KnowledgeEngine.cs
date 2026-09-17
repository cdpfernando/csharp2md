using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Retention;
using Csharp2Md.Core.Publication;
using Csharp2Md.Core.Publication.Certification;

namespace Csharp2Md.Core;

public sealed class KnowledgeEngine
{
    public async Task<AnalyzeResult> AnalyzeAsync(
        AnalyzeRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = ValidateAnalyzeRequest(request);
        if (diagnostics.Length > 0)
        {
            return new AnalyzeResult(committed: false, diagnostics);
        }

        try
        {
            var graphs = ImmutableArray.CreateBuilder<FactualGraph>(request.SolutionPaths.Length);
            foreach (var solutionPath in request.SolutionPaths)
            {
                graphs.Add(await SolutionAnalyzer.AnalyzeAsync(
                    solutionPath,
                    request.IncludeTests,
                    cancellationToken).ConfigureAwait(false));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var plan = PackageBuilder.Build(graphs.ToImmutable(), request.IncludeTests);
            var committed = PackagePublication.Publish(plan, request.OutputDirectory);
            if (committed.Certification.Solutions
                .SelectMany(static solution => solution.Journeys)
                .Any(static journey => journey.Status == JourneyCertificationStatus.Failed))
            {
                return Failure(new EngineDiagnostic(
                    "journey-certification",
                    "certification",
                    "failed-journey"));
            }

            return new AnalyzeResult(committed: true, ImmutableArray<EngineDiagnostic>.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (VariantPlanException exception)
        {
            return Failure(new EngineDiagnostic(exception.Code, "analysis", exception.Cause));
        }
        catch (SolutionAnalysisException exception)
        {
            return Failure(new EngineDiagnostic(
                "analysis-failed",
                "analysis",
                exception.Cause,
                exception.Solution.LogicalRelativePath,
                exception.Project?.LogicalRelativePath,
                exception.Variant is null ? null : CanonicalIdentity.VariantKey(exception.Variant)));
        }
        catch (OccurrenceCollisionException exception)
        {
            return Failure(new EngineDiagnostic(
                "variant-collision",
                "analysis",
                "incompatible-shape",
                variant: exception.VariantKey));
        }
        catch (RetentionException exception)
        {
            return Failure(new EngineDiagnostic("retention-failed", "retention", exception.Cause));
        }
        catch (PackageBudgetExceededException exception)
        {
            return Failure(new EngineDiagnostic("package-budget", "package-building", exception.Message));
        }
        catch (PackagePublicationException exception)
        {
            return Failure(new EngineDiagnostic(
                "publication-rejected",
                "publication",
                PublicationCause(exception),
                family: exception.Family,
                artifact: exception.Artifact));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return Failure(new EngineDiagnostic(
                "analysis-failed",
                "analysis",
                exception.GetType().Name));
        }
    }

    public PackageValidationResult Validate(ValidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = ValidateValidateRequest(request);
        if (diagnostics.Length > 0)
        {
            return new PackageValidationResult(succeeded: false, diagnostics);
        }

        var report = PackagePublication.Validate(request.PackageDirectory);
        if (!report.Succeeded)
        {
            return new PackageValidationResult(
                succeeded: false,
                report.Failures.Select(static failure => new EngineDiagnostic(
                    failure.Code,
                    failure.Stage,
                    failure.Cause,
                    family: failure.Family,
                    artifact: failure.Artifact)).ToImmutableArray());
        }

        try
        {
            var certification = JourneyCertifier.Certify(request.PackageDirectory);
            var failures = certification.Solutions
                .SelectMany(solution => solution.Journeys
                    .Where(static journey => journey.Status == JourneyCertificationStatus.Failed)
                    .Select(journey => new EngineDiagnostic(
                        "journey-certification",
                        "certification",
                        journey.Detail,
                        solution: solution.SolutionId.Value)))
                .ToImmutableArray();
            return new PackageValidationResult(failures.IsDefaultOrEmpty, failures);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return new PackageValidationResult(
                succeeded: false,
                ImmutableArray.Create(new EngineDiagnostic(
                    "package-corruption",
                    "validation",
                    exception.GetType().Name)));
        }
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

    private static AnalyzeResult Failure(EngineDiagnostic diagnostic) =>
        new(committed: false, ImmutableArray.Create(diagnostic));

    private static string PublicationCause(PackagePublicationException exception)
    {
        const string prefix = "publication: '";
        return exception.Message.StartsWith(prefix, StringComparison.Ordinal)
            && exception.Message.EndsWith("'.", StringComparison.Ordinal)
                ? exception.Message[prefix.Length..^2]
                : "publication-failed";
    }
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
