namespace Csharp2Md.Core;

// Explicit-property form (per dotnet-skills:csharp-coding-standards' validated pattern, corrected):
// the primary-constructor + validating-ctor form does not compile (CS0111 — duplicate signature).
public readonly record struct ServiceName
{
    public string Value { get; }

    public ServiceName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
}
