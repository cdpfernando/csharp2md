using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Classification;

/// <summary>
/// Symbol-shaped access to the components of a
/// <see cref="Csharp2Md.Domain.Identity.CanonicalSymbolSignature"/>. Decoding itself belongs to the
/// signature contract in Domain; this type only names the components classifiers ask for.
/// </summary>
internal static class SignatureReader
{
    /// <summary>The <c>kind</c> component - <c>namedtype</c>, <c>method</c>, <c>property</c>, ...</summary>
    public static string? Kind(Symbol symbol) => FieldOf(symbol, "kind");

    /// <summary>The fully-qualified type or namespace that declares the symbol.</summary>
    public static string? Container(Symbol symbol) => FieldOf(symbol, "container");

    /// <summary>The symbol's metadata name.</summary>
    public static string? Metadata(Symbol symbol) => FieldOf(symbol, "metadata");

    /// <summary>The symbol's fully-qualified type - a property's type, a method's return type.</summary>
    public static string? Type(Symbol symbol) => FieldOf(symbol, "type");

    /// <summary>The decoded value of <paramref name="key"/> in <paramref name="signature"/>.</summary>
    public static string? Field(string signature, string key) =>
        CanonicalSymbolSignature.Component(signature, key);

    /// <summary>
    /// The single type argument of a constructed generic type name - the <c>TEntity</c> of a
    /// <c>DbSet&lt;TEntity&gt;</c> property's <c>type</c> component. Null when the name is not
    /// generic or carries more than one argument.
    /// </summary>
    public static string? SoleTypeArgument(string? typeName)
    {
        if (typeName is null)
        {
            return null;
        }

        var open = typeName.IndexOf('<', StringComparison.Ordinal);
        if (open < 0 || !typeName.EndsWith('>'))
        {
            return null;
        }

        var argument = typeName[(open + 1)..^1].Trim();
        return argument.Length == 0 || HasSeparatorAtTopLevel(argument) ? null : argument;
    }

    private static string? FieldOf(Symbol symbol, string key)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return Field(symbol.Signature.Value, key);
    }

    private static bool HasSeparatorAtTopLevel(string argument)
    {
        var depth = 0;
        foreach (var character in argument)
        {
            switch (character)
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return true;
            }
        }

        return false;
    }
}
