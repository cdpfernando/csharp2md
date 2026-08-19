using System.Collections.Frozen;

namespace Csharp2Md.Core.Analysis.Semantics;

/// <summary>
/// One shared type-spelling normalization rule, used identically by the syntax-only
/// (<c>SyntaxFactExtractor</c>) and semantic (<c>SymbolFactEnricher</c>) fact producers and by every
/// qualified-name/parameter-type lookup, so a type declared as <c>string</c> and a query for
/// <c>System.String</c> compare equal without two divergent inline implementations.
/// </summary>
internal static class TypeNameNormalizer
{
    private const string GlobalPrefix = "global::";

    private static readonly FrozenDictionary<string, string> PredefinedTypeMetadataNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bool"] = "System.Boolean",
        ["byte"] = "System.Byte",
        ["sbyte"] = "System.SByte",
        ["short"] = "System.Int16",
        ["ushort"] = "System.UInt16",
        ["int"] = "System.Int32",
        ["uint"] = "System.UInt32",
        ["long"] = "System.Int64",
        ["ulong"] = "System.UInt64",
        ["char"] = "System.Char",
        ["float"] = "System.Single",
        ["double"] = "System.Double",
        ["decimal"] = "System.Decimal",
        ["string"] = "System.String",
        ["object"] = "System.Object",
        ["void"] = "System.Void",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Collapses a leading <c>global::</c> and the 16 C# predefined-type keywords into one
    /// <c>global::</c>-prefixed comparable form. Idempotent.
    /// </summary>
    public static string Normalize(string typeSpelling)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeSpelling);

        var bare = typeSpelling.StartsWith(GlobalPrefix, StringComparison.Ordinal)
            ? typeSpelling[GlobalPrefix.Length..]
            : typeSpelling;

        return GlobalPrefix + PredefinedTypeMetadataNames.GetValueOrDefault(bare, bare);
    }
}
