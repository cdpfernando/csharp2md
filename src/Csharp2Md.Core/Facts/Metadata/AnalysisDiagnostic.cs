using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Facts.Metadata;

public enum DiagnosticStage
{
    Inventory,
    Evaluation,
    Workspace,
    Compilation,
    Document,
    Validation,
    Generator,
    Detector,
    Persistence,
    Projection,
}

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

public readonly record struct DiagnosticData(string Key, string Value) : IComparable<DiagnosticData>
{
    public int CompareTo(DiagnosticData other)
    {
        var keyComparison = StringComparer.Ordinal.Compare(Key, other.Key);
        return keyComparison != 0 ? keyComparison : StringComparer.Ordinal.Compare(Value, other.Value);
    }
}

public sealed record AnalysisDiagnostic : IComparable<AnalysisDiagnostic>
{
    public DiagnosticId Id { get; }

    public string Code { get; }

    public DiagnosticSeverity Severity { get; }

    public DiagnosticStage Stage { get; }

    public FactId ScopeId { get; }

    public string Message { get; }

    public ImmutableArray<DiagnosticData> Data { get; }

    public ImmutableArray<Evidence> Evidence { get; }

    public DetectorId? ExtensionId { get; }

    private AnalysisDiagnostic(
        DiagnosticId id,
        string code,
        DiagnosticSeverity severity,
        DiagnosticStage stage,
        FactId scopeId,
        string message,
        ImmutableArray<DiagnosticData> data,
        ImmutableArray<Evidence> evidence,
        DetectorId? extensionId)
    {
        Id = id;
        Code = code;
        Severity = severity;
        Stage = stage;
        ScopeId = scopeId;
        Message = message;
        Data = data;
        Evidence = evidence;
        ExtensionId = extensionId;
    }

    public static AnalysisDiagnostic Create(
        string code,
        DiagnosticSeverity severity,
        DiagnosticStage stage,
        FactId scopeId,
        string message,
        IEnumerable<DiagnosticData>? data = null,
        IEnumerable<Evidence>? evidence = null,
        DetectorId? extensionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var canonicalData = (data ?? [])
            .Select(ValidateData)
            .Distinct()
            .Order()
            .ToImmutableArray();
        var canonicalEvidence = (evidence ?? [])
            .Distinct()
            .Order()
            .ToImmutableArray();
        var stageText = stage.ToString().ToLowerInvariant();
        var fingerprint = ComputeFingerprint(message, canonicalData);
        var id = DiagnosticId.Create(stageText, scopeId, code, fingerprint);

        return new AnalysisDiagnostic(
            id,
            code,
            severity,
            stage,
            scopeId,
            message,
            canonicalData,
            canonicalEvidence,
            extensionId);
    }

    public int CompareTo(AnalysisDiagnostic? other) =>
        other is null ? 1 : StringComparer.Ordinal.Compare(Id.Value, other.Id.Value);

    private static DiagnosticData ValidateData(DiagnosticData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Value);
        return data;
    }

    private static string ComputeFingerprint(string message, ImmutableArray<DiagnosticData> data)
    {
        var input = new StringBuilder(message);
        foreach (var item in data)
        {
            input.Append('\n').Append(item.Key).Append('=').Append(item.Value);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString())));
    }
}
