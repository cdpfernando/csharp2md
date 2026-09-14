namespace Csharp2Md.Storage.Mapping;

/// <summary>
/// The derived per-artifact byte ceiling together with every input the derivation used, so a consumer
/// can re-derive the same number from the published calculation alone (GCPC-037).
/// </summary>
public sealed record CeilingCalculation(
    int CeilingBytes,
    int ReadingBudgetTokens,
    int MaxFileReadsPerScenario,
    double BytesPerToken,
    string TokenEstimatorId);

/// <summary>
/// Derives the per-artifact byte ceiling from the declared per-scenario reading budget (tokens and file
/// reads, GCPC-036) and the package's measured bytes-per-token ratio, replacing the fixed
/// <see cref="Csharp2Md.Projection.ShardWriter.DefaultCeilingBytes"/> constant with a value that moves
/// when the declared budget moves (GCPC-037).
/// </summary>
public static class CeilingCalculator
{
    /// <summary>
    /// Identifies the deterministic token-estimation function this calculator's <see cref="BytesPerToken"/>
    /// ratio implements: token count is estimated as byte count divided by this ratio, with no external
    /// tokenizer dependency.
    /// </summary>
    public const string TokenEstimatorId = "csharp2md.tokens.bytes-per-token-v1";

    /// <summary>The declared default per-scenario reading budget, in tokens, absent a CLI override.</summary>
    public const int DefaultReadingBudgetTokens = 100_000;

    /// <summary>The declared default maximum file reads per scenario, absent a CLI override.</summary>
    public const int DefaultMaxFileReadsPerScenario = 25;

    /// <summary>
    /// The measured bytes-per-token ratio for this project's indented, snake_case canonical JSON
    /// encoding. At the declared defaults this derives the ceiling to exactly 32 KiB.
    /// </summary>
    public const double DefaultBytesPerToken = 8.192;

    public static CeilingCalculation Derive(
        int readingBudgetTokens = DefaultReadingBudgetTokens,
        int maxFileReadsPerScenario = DefaultMaxFileReadsPerScenario,
        double bytesPerToken = DefaultBytesPerToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(readingBudgetTokens);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileReadsPerScenario);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerToken);

        var ceilingBytes = (int)Math.Round(
            readingBudgetTokens * bytesPerToken / maxFileReadsPerScenario,
            MidpointRounding.AwayFromZero);

        return new CeilingCalculation(
            ceilingBytes,
            readingBudgetTokens,
            maxFileReadsPerScenario,
            bytesPerToken,
            TokenEstimatorId);
    }

    /// <summary>
    /// The declared deterministic token estimator named by <see cref="TokenEstimatorId"/>: an artifact of
    /// <paramref name="byteLength"/> bytes is estimated to cost this many tokens to read.
    /// </summary>
    public static double EstimateTokens(long byteLength, double bytesPerToken = DefaultBytesPerToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerToken);
        return byteLength / bytesPerToken;
    }
}
