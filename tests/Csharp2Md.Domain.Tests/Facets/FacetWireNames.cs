namespace Csharp2Md.Domain.Tests.Facets;

/// <summary>
/// Converts a PascalCase enum member name to its documented kebab-case wire spelling, for tests that assert
/// an axis's conceptual value set before <c>FacetAxes</c> introduces the explicit, non-derived pairing.
/// </summary>
internal static class FacetWireNames
{
    public static string ToKebabCase(string pascalCase) =>
        string.Concat(pascalCase.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? "-" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));
}
