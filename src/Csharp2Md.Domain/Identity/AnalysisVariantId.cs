namespace Csharp2Md.Domain.Identity;

public readonly record struct AnalysisVariantId
{
    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized analysis-variant identity has no value.");

    public static AnalysisVariantId Create(
        string targetFramework,
        string configuration,
        IEnumerable<string> symbols,
        string environment)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        var sortedSymbols = symbols
            .Select(symbol => FactIdGrammar.RequireCanonicalText(symbol, nameof(symbols)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var id = FactIdGrammar.Create(
            "analysis-variant",
            ("tfm", FactIdGrammar.RequireCanonicalText(targetFramework, nameof(targetFramework))),
            ("configuration", FactIdGrammar.RequireCanonicalText(configuration, nameof(configuration))),
            ("symbols", sortedSymbols.Length == 0 ? null : string.Join(',', sortedSymbols)),
            ("environment", FactIdGrammar.RequireCanonicalText(environment, nameof(environment))));

        return new AnalysisVariantId(id.Value);
    }

    private AnalysisVariantId(string value) => _value = value;

    public override string ToString() => Value;
}
