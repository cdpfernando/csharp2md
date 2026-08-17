namespace Csharp2Md.Core.Analysis.Contracts;

/// <summary>Caller-owned inputs and safety choices for one analysis run.</summary>
public sealed record AnalysisRequest
{
    public const int DefaultServiceTimeoutMinutes = 10;

    public string Input { get; }
    public string OutputRoot { get; }
    public bool ForceOutput { get; }
    public AnalysisOptions Options { get; }

    private AnalysisRequest(string input, string outputRoot, bool forceOutput, AnalysisOptions options)
    {
        Input = input;
        OutputRoot = outputRoot;
        ForceOutput = forceOutput;
        Options = options;
    }

    public static AnalysisRequestResult Create(
        string input,
        string outputRoot,
        bool forceOutput = false,
        AnalysisOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return AnalysisRequestResult.Failed(AnalysisRequestError.EmptyInput, "Analysis input is required.");
        }

        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            return AnalysisRequestResult.Failed(AnalysisRequestError.EmptyOutputRoot, "Output root is required.");
        }

        var resolvedOptions = options ?? AnalysisOptions.Default;
        return resolvedOptions.Validate() is { } error
            ? AnalysisRequestResult.Failed(error, AnalysisOptions.MessageFor(error))
            : AnalysisRequestResult.Success(new AnalysisRequest(input, outputRoot, forceOutput, resolvedOptions));
    }
}

public enum AnalysisMode { SyntaxOnly, Semantic }

public enum TrustMode { Untrusted, TrustedSolution }

public sealed record AnalysisOptions
{
    public static AnalysisOptions Default { get; } = new();

    public AnalysisMode Mode { get; init; } = AnalysisMode.SyntaxOnly;
    public TrustMode Trust { get; init; } = TrustMode.Untrusted;
    public bool IncludeSourceGenerators { get; init; }
    public TimeSpan ServiceTimeout { get; init; } = TimeSpan.FromMinutes(AnalysisRequest.DefaultServiceTimeoutMinutes);

    internal AnalysisRequestError? Validate() =>
        ServiceTimeout <= TimeSpan.Zero ? AnalysisRequestError.InvalidTimeout :
        Mode == AnalysisMode.Semantic && Trust != TrustMode.TrustedSolution ? AnalysisRequestError.SemanticRequiresTrustedSolution :
        IncludeSourceGenerators && (Mode != AnalysisMode.Semantic || Trust != TrustMode.TrustedSolution) ? AnalysisRequestError.GeneratorsRequireTrustedSemanticMode :
        null;

    internal static string MessageFor(AnalysisRequestError error) => error switch
    {
        AnalysisRequestError.SemanticRequiresTrustedSolution => "Semantic analysis requires trusted-solution trust.",
        AnalysisRequestError.GeneratorsRequireTrustedSemanticMode => "Source generators require trusted semantic analysis.",
        AnalysisRequestError.InvalidTimeout => "Analysis timeout must be positive.",
        _ => "The analysis request is invalid.",
    };
}

public enum AnalysisRequestError
{
    EmptyInput,
    EmptyOutputRoot,
    SemanticRequiresTrustedSolution,
    GeneratorsRequireTrustedSemanticMode,
    InvalidTimeout,
}

public sealed record AnalysisRequestResult
{
    public bool IsSuccess { get; private init; }
    public AnalysisRequest? Request { get; private init; }
    public AnalysisRequestError? Error { get; private init; }
    public string? Message { get; private init; }

    public static AnalysisRequestResult Success(AnalysisRequest request) => new() { IsSuccess = true, Request = request };
    public static AnalysisRequestResult Failed(AnalysisRequestError error, string message) => new() { Error = error, Message = message };
}
