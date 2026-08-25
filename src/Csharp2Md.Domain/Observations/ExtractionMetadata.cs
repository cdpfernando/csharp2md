using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Observations;

public readonly record struct BindingDiagnostic
{
    public string Code { get; }

    public string Message { get; }

    public BindingDiagnostic(string code, string message)
    {
        Code = FactIdGrammar.RequireCanonicalText(code, nameof(code));
        Message = FactIdGrammar.RequireCanonicalText(message, nameof(message));
    }
}

public readonly record struct ExtractorVersion : IComparable<ExtractorVersion>
{
    public int Value { get; }

    public ExtractorVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "An extractor version must be a positive integer.");
        }

        Value = value;
    }

    public int CompareTo(ExtractorVersion other) => Value.CompareTo(other.Value);
}
