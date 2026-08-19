using Csharp2Md.Core.Analysis.Semantics;

namespace Csharp2Md.Core.Tests.Analysis.Semantics;

public sealed class TypeNameNormalizerTests
{
    /// <summary>
    /// spec.md SYMIDX-08's literal predefined-type list, paired with the metadata name each keyword
    /// aliases. All 16 are enumerated here so a dropped entry fails the count assertion below.
    /// </summary>
    public static TheoryData<string, string> PredefinedTypeAliases => new()
    {
        { "bool", "global::System.Boolean" },
        { "byte", "global::System.Byte" },
        { "sbyte", "global::System.SByte" },
        { "short", "global::System.Int16" },
        { "ushort", "global::System.UInt16" },
        { "int", "global::System.Int32" },
        { "uint", "global::System.UInt32" },
        { "long", "global::System.Int64" },
        { "ulong", "global::System.UInt64" },
        { "char", "global::System.Char" },
        { "float", "global::System.Single" },
        { "double", "global::System.Double" },
        { "decimal", "global::System.Decimal" },
        { "string", "global::System.String" },
        { "object", "global::System.Object" },
        { "void", "global::System.Void" },
    };

    [Theory]
    [MemberData(nameof(PredefinedTypeAliases))]
    public void Normalize_PredefinedTypeKeyword_MapsToItsSystemMetadataName(string keyword, string expected) =>
        Assert.Equal(expected, TypeNameNormalizer.Normalize(keyword));

    [Fact]
    public void Normalize_CoversAllSixteenPredefinedTypeKeywords() =>
        Assert.Equal(16, PredefinedTypeAliases.Count);

    [Theory]
    [MemberData(nameof(PredefinedTypeAliases))]
    public void Normalize_GlobalPrefixedPredefinedTypeKeyword_MapsToTheSameSystemMetadataName(
        string keyword,
        string expected) =>
        Assert.Equal(expected, TypeNameNormalizer.Normalize($"global::{keyword}"));

    [Theory]
    [InlineData("System.String", "global::System.String")]
    [InlineData("global::System.String", "global::System.String")]
    [InlineData("System.Int32", "global::System.Int32")]
    [InlineData("global::Acme.Payments.PaymentsService", "global::Acme.Payments.PaymentsService")]
    public void Normalize_LeadingGlobalPrefix_CollapsesToOneComparableForm(string spelling, string expected) =>
        Assert.Equal(expected, TypeNameNormalizer.Normalize(spelling));

    [Fact]
    public void Normalize_SameTypeSpelledAsKeywordQualifiedOrGlobalQualified_ProducesOneComparableForm()
    {
        var fromKeyword = TypeNameNormalizer.Normalize("string");
        var fromQualified = TypeNameNormalizer.Normalize("System.String");
        var fromGlobalQualified = TypeNameNormalizer.Normalize("global::System.String");

        Assert.Equal("global::System.String", fromKeyword);
        Assert.Equal(fromKeyword, fromQualified);
        Assert.Equal(fromKeyword, fromGlobalQualified);
    }

    [Theory]
    [InlineData("Acme.Payments.PaymentsService", "global::Acme.Payments.PaymentsService")]
    [InlineData("PaymentsService", "global::PaymentsService")]
    [InlineData("Outer.Inner", "global::Outer.Inner")]
    [InlineData("List<int>", "global::List<int>")]
    public void Normalize_NonPredefinedNonPrefixedSpelling_PassesThroughWithOnlyTheGlobalPrefixAdded(
        string spelling,
        string expected) =>
        Assert.Equal(expected, TypeNameNormalizer.Normalize(spelling));

    public static TheoryData<string> IdempotencyInputs
    {
        get
        {
            var inputs = new TheoryData<string>();
            foreach (var row in PredefinedTypeAliases)
            {
                var keyword = (string)row[0];
                inputs.Add(keyword);
                inputs.Add($"global::{keyword}");
            }

            inputs.Add("System.String");
            inputs.Add("global::System.String");
            inputs.Add("Acme.Payments.PaymentsService");
            inputs.Add("PaymentsService");
            inputs.Add("List<int>");
            return inputs;
        }
    }

    [Theory]
    [MemberData(nameof(IdempotencyInputs))]
    public void Normalize_IsIdempotent(string spelling)
    {
        var once = TypeNameNormalizer.Normalize(spelling);

        Assert.Equal(once, TypeNameNormalizer.Normalize(once));
    }
}
