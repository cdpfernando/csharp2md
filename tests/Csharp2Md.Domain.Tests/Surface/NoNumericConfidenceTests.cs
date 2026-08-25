using System.Reflection;
using System.Runtime.CompilerServices;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Surface;

/// <summary>
/// Proves TAX-59 over the whole assembly, not a hand-listed subset of taxonomy types: no fact, observation,
/// relation, candidate or promotion type may carry a numeric confidence, score, weight, probability, rank or
/// certainty. <see cref="AllowedNumericMembers"/> is the exhaustive, exact set of numeric members the domain
/// declares - the five version axes, the observation occurrence ordinal, the classifier version, the wrapped
/// extractor version, the source-span positions, the document-hash length constant and two collection counts -
/// each named by <c>nameof</c> so a rename that removes one forces this file to stop compiling.
/// </summary>
public sealed class NoNumericConfidenceTests
{
    private static readonly Type[] NumericTypes =
    [
        typeof(int),
        typeof(long),
        typeof(double),
        typeof(float),
        typeof(decimal),
    ];

    private static readonly string[] ForbiddenNameFragments =
    [
        "confidence",
        "score",
        "weight",
        "probability",
        "rank",
        "certainty",
    ];

    private static readonly (Type Owner, string Name)[] VersionAxesAndOccurrenceOrdinal =
    [
        (typeof(TaxonomyVersions), nameof(TaxonomyVersions.SchemaVersion)),
        (typeof(TaxonomyVersions), nameof(TaxonomyVersions.TaxonomyVersion)),
        (typeof(TaxonomyVersions), nameof(TaxonomyVersions.ObservationSchemaVersion)),
        (typeof(TaxonomyVersions), nameof(TaxonomyVersions.ExtractorSetVersion)),
        (typeof(TaxonomyVersions), nameof(TaxonomyVersions.ClassifierSetVersion)),
        (typeof(ObservationIdentity), nameof(ObservationIdentity.OccurrenceOrdinal)),
    ];

    private static readonly (Type Owner, string Name)[] AllowedNumericMembers =
    [
        .. VersionAxesAndOccurrenceOrdinal,
        (typeof(ClassifierIdentity), nameof(ClassifierIdentity.Version)),
        (typeof(ExtractorVersion), nameof(ExtractorVersion.Value)),
        (typeof(SourceSpan), nameof(SourceSpan.StartLine)),
        (typeof(SourceSpan), nameof(SourceSpan.StartColumn)),
        (typeof(SourceSpan), nameof(SourceSpan.EndLine)),
        (typeof(SourceSpan), nameof(SourceSpan.EndColumn)),
        (typeof(DocumentHash), nameof(DocumentHash.Length)),
        (typeof(IdentityLedger), nameof(IdentityLedger.Count)),
        (typeof(ConfirmedRelationSet), nameof(ConfirmedRelationSet.Count)),
    ];

    private static Assembly DomainAssembly => typeof(AssemblyMarker).Assembly;

    private sealed record DecoyWithConfidence(int ConfidenceScore);

    [Fact]
    [Trait("Requirement", "TAX-59")]
    public void Domain_HasNoMemberWhoseNameMatchesAForbiddenConfidenceLikeFragment()
    {
        var violations = ScanNumericMembers(DomainTypes())
            .Where(member => ContainsForbiddenFragment(member.Name))
            .Select(member => $"{member.Owner.FullName}.{member.Name}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Forbidden numeric member(s) found on the taxonomy: {string.Join(", ", violations)}");
    }

    [Fact]
    [Trait("Requirement", "TAX-59")]
    public void Domain_DeclaresExactlyTheDocumentedNumericMembers_NoMoreAndNoFewer()
    {
        var actual = ScanNumericMembers(DomainTypes()).ToHashSet();
        var expected = AllowedNumericMembers.ToHashSet();

        var undocumented = actual.Except(expected).Select(m => $"{m.Owner.FullName}.{m.Name}").ToArray();
        var missing = expected.Except(actual).Select(m => $"{m.Owner.FullName}.{m.Name}").ToArray();

        Assert.True(
            undocumented.Length == 0,
            $"Numeric member(s) found that are not on the documented allowlist: {string.Join(", ", undocumented)}");
        Assert.True(
            missing.Length == 0,
            $"Allowlisted numeric member(s) no longer found on the taxonomy: {string.Join(", ", missing)}");
    }

    [Fact]
    [Trait("Requirement", "TAX-59")]
    public void VersionAxesAndOccurrenceOrdinal_AreExplicitlyAllowedAndReallyExistAsNumericMembers()
    {
        var actualNumericMembers = ScanNumericMembers(DomainTypes()).ToHashSet();

        foreach (var allowed in VersionAxesAndOccurrenceOrdinal)
        {
            Assert.False(
                ContainsForbiddenFragment(allowed.Name),
                $"Allowlisted member '{allowed.Owner.FullName}.{allowed.Name}' unexpectedly matches a forbidden fragment.");
            Assert.Contains(allowed, actualNumericMembers);
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-59")]
    public void Scanner_FlagsANumericConfidenceMemberOnADecoyType_ProvingItIsNotVacuous()
    {
        var violations = ScanNumericMembers([typeof(DecoyWithConfidence)])
            .Where(member => ContainsForbiddenFragment(member.Name))
            .ToArray();

        var violation = Assert.Single(violations);
        Assert.Equal(nameof(DecoyWithConfidence.ConfidenceScore), violation.Name);
    }

    private static IEnumerable<Type> DomainTypes() =>
        DomainAssembly.GetTypes()
            .Where(type => type.Namespace is not null && type.Namespace.StartsWith("Csharp2Md.Domain", StringComparison.Ordinal))
            .Where(type => !Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute)))
            .Where(type => !type.Name.Contains('<', StringComparison.Ordinal));

    private static IEnumerable<(Type Owner, string Name)> ScanNumericMembers(IEnumerable<Type> types)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in types)
        {
            foreach (var field in type.GetFields(Flags))
            {
                if (!field.IsSpecialName && IsExposed(field) && IsNumeric(field.FieldType))
                {
                    yield return (type, field.Name);
                }
            }

            foreach (var property in type.GetProperties(Flags))
            {
                if (IsExposed(property) && IsNumeric(property.PropertyType))
                {
                    yield return (type, property.Name);
                }
            }
        }
    }

    private static bool IsNumeric(Type type) =>
        NumericTypes.Contains(Nullable.GetUnderlyingType(type) ?? type);

    private static bool ContainsForbiddenFragment(string memberName) =>
        ForbiddenNameFragments.Any(fragment => memberName.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool IsExposed(FieldInfo field) => field.IsPublic || field.IsAssembly || field.IsFamilyOrAssembly;

    private static bool IsExposed(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        return accessor is not null && (accessor.IsPublic || accessor.IsAssembly || accessor.IsFamilyOrAssembly);
    }
}
