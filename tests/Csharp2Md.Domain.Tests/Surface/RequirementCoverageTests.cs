using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Csharp2Md.Domain.Tests;

namespace Csharp2Md.Domain.Tests.Surface;

/// <summary>
/// Closes requirement traceability mechanically: every <c>TAX-01</c> through <c>TAX-91</c> must be carried by
/// at least one <c>[Trait("Requirement", "TAX-nn")]</c> somewhere in this assembly, and every carried trait
/// value must itself be a well-formed, in-range ID, so a typo cannot silently masquerade as coverage.
///
/// Deviation from a hollow-gap default: TAX-01, TAX-02, TAX-04 and TAX-05 (assembly wiring facts asserted only
/// by the build gate per the Test Coverage Matrix's "none" entry for project/solution wiring) had no existing
/// test to re-tag, so this file adds direct, non-hollow assertions of the already-true production state for
/// each rather than inventing new production behaviour.
/// </summary>
public sealed class RequirementCoverageTests
{
    private const int LowestRequirementNumber = 1;
    private const int HighestRequirementNumber = 91;

    private static readonly Regex TaxIdPattern = new("^TAX-([0-9]{2})$", RegexOptions.None);

    private static class DecoyWithAnOutOfRangeTrait
    {
        [Trait("Requirement", "TAX-9")]
        public static void MethodCarryingATypoRequirementId()
        {
        }
    }

    [Fact]
    public void EveryRequirementId_TAX01ThroughTAX91_IsCarriedByAtLeastOneTestTrait()
    {
        var expected = ExpectedRequirementIds().ToHashSet(StringComparer.Ordinal);
        var actual = RequirementTraitsOn(TestAssemblyTypes()).Select(trait => trait.Value).ToHashSet(StringComparer.Ordinal);

        var uncovered = expected.Except(actual).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.True(uncovered.Length == 0, $"Uncovered requirement ID(s): {string.Join(", ", uncovered)}");
    }

    [Fact]
    public void EveryCarriedRequirementTrait_IsAWellFormedInRangeTaxId()
    {
        var malformed = FindMalformedTraits(RequirementTraitsOn(TestAssemblyTypes())).ToArray();

        Assert.True(malformed.Length == 0, $"Malformed or out-of-range requirement trait(s): {string.Join(", ", malformed)}");
    }

    [Fact]
    public void FormatScanner_FlagsATraitNamingAnIdOutsideTheValidFormat_ProvingItIsNotVacuous()
    {
        var malformed = FindMalformedTraits(RequirementTraitsOn([typeof(DecoyWithAnOutOfRangeTrait)])).ToArray();

        var violation = Assert.Single(malformed);
        Assert.Contains("TAX-9", violation, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-01")]
    public void DomainAssembly_TargetsNet10()
    {
        var attribute = typeof(AssemblyMarker).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(".NETCoreApp,Version=v10.0", attribute!.FrameworkName);
    }

    [Fact]
    [Trait("Requirement", "TAX-02")]
    public void DomainProject_DeclaresNoPackageReferenceAndNoProjectReference()
    {
        var content = File.ReadAllText(Path.Combine(DomainTestPaths.RepoRoot, "src", "Csharp2Md.Domain", "Csharp2Md.Domain.csproj"));

        Assert.DoesNotContain("<PackageReference", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProjectReference", content, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-04")]
    public void SolutionBuild_KeepsTreatWarningsAsErrorsEnabled()
    {
        var buildPropsContent = File.ReadAllText(Path.Combine(DomainTestPaths.RepoRoot, "Directory.Build.props"));
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", buildPropsContent, StringComparison.Ordinal);

        var domainCsprojContent = File.ReadAllText(Path.Combine(DomainTestPaths.RepoRoot, "src", "Csharp2Md.Domain", "Csharp2Md.Domain.csproj"));
        Assert.DoesNotContain("TreatWarningsAsErrors", domainCsprojContent, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-05")]
    public void DomainProject_IsListedInTheSolutionUnderTheSrcFolder()
    {
        var content = File.ReadAllText(Path.Combine(DomainTestPaths.RepoRoot, "csharp2md.slnx"));

        var srcFolderStart = content.IndexOf("<Folder Name=\"/src/\">", StringComparison.Ordinal);
        Assert.True(srcFolderStart >= 0, "The solution declares no '/src/' folder.");

        var srcFolderEnd = content.IndexOf("</Folder>", srcFolderStart, StringComparison.Ordinal);
        Assert.True(srcFolderEnd > srcFolderStart, "The '/src/' folder has no closing tag.");

        Assert.Contains("Csharp2Md.Domain.csproj", content[srcFolderStart..srcFolderEnd], StringComparison.Ordinal);
    }

    private static IEnumerable<string> ExpectedRequirementIds() =>
        Enumerable.Range(LowestRequirementNumber, HighestRequirementNumber - LowestRequirementNumber + 1)
            .Select(number => $"TAX-{number:D2}");

    private static IEnumerable<Type> TestAssemblyTypes() =>
        typeof(RequirementCoverageTests).Assembly.GetTypes()
            .Where(type => type != typeof(DecoyWithAnOutOfRangeTrait));

    private static IReadOnlyList<(string TypeName, string MethodName, string Value)> RequirementTraitsOn(IEnumerable<Type> types)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var traits = new List<(string TypeName, string MethodName, string Value)>();
        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(Flags))
            {
                foreach (var attributeData in CustomAttributeData.GetCustomAttributes(method))
                {
                    if (attributeData.AttributeType != typeof(TraitAttribute))
                    {
                        continue;
                    }

                    var name = (string)attributeData.ConstructorArguments[0].Value!;
                    if (name != "Requirement")
                    {
                        continue;
                    }

                    var value = (string)attributeData.ConstructorArguments[1].Value!;
                    traits.Add((type.FullName ?? type.Name, method.Name, value));
                }
            }
        }

        return traits;
    }

    private static IEnumerable<string> FindMalformedTraits(IEnumerable<(string TypeName, string MethodName, string Value)> traits)
    {
        foreach (var (typeName, methodName, value) in traits)
        {
            if (value.StartsWith("ENG-", StringComparison.Ordinal) ||
                value.StartsWith("RP-", StringComparison.Ordinal))
            {
                continue;
            }

            var match = TaxIdPattern.Match(value);
            if (!match.Success)
            {
                yield return $"{typeName}.{methodName}: '{value}' does not match the 'TAX-NN' format";
                continue;
            }

            var number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (number < LowestRequirementNumber || number > HighestRequirementNumber)
            {
                yield return $"{typeName}.{methodName}: '{value}' is outside the registered TAX-{LowestRequirementNumber:D2}..TAX-{HighestRequirementNumber:D2} range";
            }
        }
    }
}
