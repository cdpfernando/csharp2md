namespace Csharp2Md.Domain.Identity;

public enum SymbolParameterModifier
{
    None,
    Ref,
    Out,
    In,
}

public readonly record struct SymbolParameterSignature(string FullyQualifiedType, SymbolParameterModifier Modifier = SymbolParameterModifier.None);

public readonly record struct CanonicalSymbolSignature
{
    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized symbol signature has no value.");

    public static CanonicalSymbolSignature Create(
        string symbolKind,
        string fullyQualifiedContainer,
        string metadataName,
        int genericArity,
        string fullyQualifiedType,
        IEnumerable<SymbolParameterSignature>? parameters = null,
        IEnumerable<string>? typeArguments = null)
    {
        if (genericArity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(genericArity));
        }

        var parameterValues = (parameters ?? [])
            .Select(static parameter =>
                $"{ModifierText(parameter.Modifier)}{FactIdGrammar.RequireCanonicalText(parameter.FullyQualifiedType, nameof(parameter.FullyQualifiedType))}")
            .ToArray();
        var typeArgumentValues = (typeArguments ?? [])
            .Select(static argument => FactIdGrammar.RequireCanonicalText(argument, nameof(typeArguments)))
            .ToArray();

        var signature = FactIdGrammar.Create(
            "signature",
            ("kind", FactIdGrammar.RequireCanonicalText(symbolKind, nameof(symbolKind))),
            ("container", FactIdGrammar.RequireCanonicalText(fullyQualifiedContainer, nameof(fullyQualifiedContainer))),
            ("metadata", FactIdGrammar.RequireCanonicalText(metadataName, nameof(metadataName))),
            ("arity", genericArity.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("type", FactIdGrammar.RequireCanonicalText(fullyQualifiedType, nameof(fullyQualifiedType))),
            ("parameters", parameterValues.Length == 0 ? null : string.Join(',', parameterValues)),
            ("type-arguments", typeArgumentValues.Length == 0 ? null : string.Join(',', typeArgumentValues)));

        return new CanonicalSymbolSignature(signature.Value.Replace("id1:signature", "sig1", StringComparison.Ordinal));
    }

    /// <summary>
    /// The decoded value of <paramref name="key"/> in <paramref name="signature"/> - the inverse of the
    /// encoding <see cref="Create"/> applies. Null when the component is absent or carries the <c>-</c>
    /// sentinel that stands for an omitted component.
    /// </summary>
    public static string? Component(string signature, string key)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var marker = ";" + key + "=";
        var start = signature.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = signature.IndexOf(';', start);
        var encoded = end < 0 ? signature[start..] : signature[start..end];
        return encoded.Length == 0 || encoded == "-" ? null : Uri.UnescapeDataString(encoded);
    }

    /// <summary>The decoded value of <paramref name="key"/> in this signature.</summary>
    public string? Component(string key) => Component(Value, key);

    private CanonicalSymbolSignature(string value) => _value = value;

    public override string ToString() => Value;

    private static string ModifierText(SymbolParameterModifier modifier) => modifier switch
    {
        SymbolParameterModifier.None => "",
        SymbolParameterModifier.Ref => "ref ",
        SymbolParameterModifier.Out => "out ",
        SymbolParameterModifier.In => "in ",
        _ => throw new ArgumentOutOfRangeException(nameof(modifier)),
    };
}
