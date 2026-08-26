using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Csharp2Md.Analysis.Tests.Surface;

/// <summary>
/// Closes requirement traceability mechanically: every <c>ROSE-01</c> through <c>ROSE-64</c> must be carried by
/// at least one <c>[Trait("Requirement", "ROSE-nn")]</c> in the Analysis or CLI test assemblies, and
/// every carried ROSE trait value must itself be a well-formed, in-range ID, so a typo cannot silently
/// masquerade as coverage.
/// </summary>
public sealed class RequirementCoverageTests
{
    private const int LowestRequirementNumber = 1;
    private const int HighestRequirementNumber = 64;

    private static readonly Regex RoseIdPattern = new("^ROSE-([0-9]{2})$", RegexOptions.None);
    private static readonly Regex TraitPattern = new(@"\[Trait\(""Requirement"", ""([^""]+)""\)\]", RegexOptions.None);

    private static readonly string[] OwnedSourceTestProjects =
    [
        "Csharp2Md.Cli.Tests",
    ];

    private static class DecoyWithAnOutOfRangeTrait
    {
        [Trait("Requirement", "ROSE-9")]
        public static void MethodCarryingATypoRequirementId()
        {
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-64")]
    public void EveryRequirementId_ROSE01ThroughROSE64_IsCarriedByAtLeastOneTestTrait()
    {
        var expected = ExpectedRequirementIds().ToHashSet(StringComparer.Ordinal);
        var actual = OwnedRequirementTraits().Select(trait => trait.Value).ToHashSet(StringComparer.Ordinal);

        var uncovered = expected.Except(actual).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.True(uncovered.Length == 0, $"Uncovered requirement ID(s): {string.Join(", ", uncovered)}");
    }

    [Fact]
    [Trait("Requirement", "ROSE-64")]
    public void EveryCarriedRequirementTrait_IsAWellFormedInRangeRoseId()
    {
        var malformed = FindMalformedTraits(OwnedRequirementTraits()).ToArray();

        Assert.True(malformed.Length == 0, $"Malformed or out-of-range requirement trait(s): {string.Join(", ", malformed)}");
    }

    [Fact]
    [Trait("Requirement", "ROSE-64")]
    public void FormatScanner_FlagsATraitNamingAnIdOutsideTheValidFormat_ProvingItIsNotVacuous()
    {
        var malformed = FindMalformedTraits(RequirementTraitsOn([typeof(DecoyWithAnOutOfRangeTrait)])).ToArray();

        var violation = Assert.Single(malformed);
        Assert.Contains("ROSE-9", violation, StringComparison.Ordinal);
    }

    private static IEnumerable<string> ExpectedRequirementIds() =>
        Enumerable.Range(LowestRequirementNumber, HighestRequirementNumber - LowestRequirementNumber + 1)
            .Select(number => $"ROSE-{number:D2}");

    private static IReadOnlyList<(string TypeName, string MethodName, string Value)> OwnedRequirementTraits() =>
        RequirementTraitsOn(TestAssemblyTypes())
            .Concat(OwnedSourceTestProjects.SelectMany(RequirementTraitsFromSource))
            .ToArray();

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

    private static IReadOnlyList<(string TypeName, string MethodName, string Value)> RequirementTraitsFromSource(string testProjectName)
    {
        var root = Path.Combine(AnalysisTestPaths.RepoRoot, "tests", testProjectName);
        var traits = new List<(string TypeName, string MethodName, string Value)>();

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutputPath(path))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var match = TraitPattern.Match(lines[index]);
                if (!match.Success)
                {
                    continue;
                }

                traits.Add(($"{relative}:{index + 1}", "trait", match.Groups[1].Value));
            }
        }

        return traits;
    }

    private static bool IsBuildOutputPath(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static IEnumerable<string> FindMalformedTraits(IEnumerable<(string TypeName, string MethodName, string Value)> traits)
    {
        foreach (var (typeName, methodName, value) in traits)
        {
            if (value.StartsWith("ENG-", StringComparison.Ordinal)
                || value.StartsWith("STOR-", StringComparison.Ordinal)
                || value.StartsWith("EBC-", StringComparison.Ordinal)
                || value.StartsWith("PK-", StringComparison.Ordinal)
                || value.StartsWith("CLLF-", StringComparison.Ordinal)
                || value.StartsWith("CDC-", StringComparison.Ordinal))
            {
                continue;
            }

            var match = RoseIdPattern.Match(value);
            if (!match.Success)
            {
                yield return $"{typeName}.{methodName}: '{value}' does not match the 'ROSE-NN' format";
                continue;
            }

            var number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (number < LowestRequirementNumber || number > HighestRequirementNumber)
            {
                yield return $"{typeName}.{methodName}: '{value}' is outside the registered ROSE-{LowestRequirementNumber:D2}..ROSE-{HighestRequirementNumber:D2} range";
            }
        }
    }
}
