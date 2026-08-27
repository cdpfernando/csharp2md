using System.Globalization;
using System.Text.RegularExpressions;

namespace Csharp2Md.Analysis.Tests.Projection;

/// <summary>
/// Closes RP traceability mechanically: every RP-01 through RP-57 must be carried by
/// at least one Requirement trait across the test assemblies, and every spec
/// edge case must have a named test method.
/// </summary>
public sealed class RequirementCoverageTests
{
    private const int LowestRequirementNumber = 1;
    private const int HighestRequirementNumber = 57;

    private static readonly Regex RpIdPattern = new("^RP-([0-9]{2})$", RegexOptions.None);
    private static readonly Regex TraitPattern = new(@"\[Trait\(""Requirement"", ""([^""]+)""\)\]", RegexOptions.None);
    private static readonly Regex MethodPattern = new(@"\b(?:public|internal)\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\(", RegexOptions.None);

    private static readonly string[] TestProjects =
    [
        "Csharp2Md.Domain.Tests",
        "Csharp2Md.Analysis.Tests",
        "Csharp2Md.Storage.Tests",
        "Csharp2Md.Cli.Tests",
        "Csharp2Md.Projection.Tests",
    ];

    private static readonly string[] NamedEdgeCaseMethods =
    [
        "Project_NonUtf8Bytes_PassThroughUnchanged",
        "Project_SharedRelativePathInDifferentProjects_ProducesDistinctKeys",
        "Project_WholeDocumentSecret_PublishesOnlyTheMarkerAndDeclaresRedaction",
        "Redact_OverlappingSpans_MergeIntoOneMarker",
        "Redact_OverlappingSpans_DoNotNestMarkers",
        "Project_FactOutsideCatalogFamilies_DoesNotFailProjectionValidation",
        "Project_SelfRelation_AppearsInBothOutgoingAndIncomingForThatFact",
        "Rank_OwnerWithZeroRelations_RanksLastNotOmitted",
        "Emit_DeclarationWithoutDocumentFact_YieldsNoLocator",
        "PackageProjector_NoArchitectureFacts_PublishesGuidesWithoutMarkdownPages",
    ];

    private static class DecoyWithAnOutOfRangeTrait
    {
        public static void MethodCarryingATypoRequirementId()
        {
        }
    }

    [Fact]
    public void EveryRequirementId_RP01ThroughRP57_IsCarriedByAtLeastOneTestTrait()
    {
        var expected = ExpectedRequirementIds().ToHashSet(StringComparer.Ordinal);
        var actual = RequirementTraitsFromSource()
            .Select(static trait => trait.Value)
            .Where(static value => value.StartsWith("RP-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var uncovered = expected.Except(actual).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.True(uncovered.Length == 0, $"Uncovered requirement ID(s): {string.Join(", ", uncovered)}");
    }

    [Fact]
    public void EveryCarriedRpRequirementTrait_IsAWellFormedInRangeId()
    {
        var malformed = FindMalformedTraits(RequirementTraitsFromSource()).ToArray();

        Assert.True(malformed.Length == 0, $"Malformed or out-of-range requirement trait(s): {string.Join(", ", malformed)}");
    }

    [Fact]
    public void FormatScanner_FlagsATraitNamingAnIdOutsideTheValidFormat_ProvingItIsNotVacuous()
    {
        var malformed = FindMalformedTraits(
        [
            (typeof(DecoyWithAnOutOfRangeTrait).FullName ?? nameof(DecoyWithAnOutOfRangeTrait),
                nameof(DecoyWithAnOutOfRangeTrait.MethodCarryingATypoRequirementId),
                "RP-9"),
        ]).ToArray();

        var violation = Assert.Single(malformed);
        Assert.Contains("RP-9", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void RangeScanner_FlagsATraitNamingAnIdOutsideTheRegisteredRange_ProvingItIsNotVacuous()
    {
        var malformed = FindMalformedTraits(
        [
            (typeof(DecoyWithAnOutOfRangeTrait).FullName ?? nameof(DecoyWithAnOutOfRangeTrait),
                nameof(DecoyWithAnOutOfRangeTrait.MethodCarryingATypoRequirementId),
                "RP-99"),
        ]).ToArray();

        var violation = Assert.Single(malformed);
        Assert.Contains("RP-99", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySpecEdgeCase_HasANamedTest()
    {
        var methods = NamedTestMethods().ToHashSet(StringComparer.Ordinal);
        var missing = NamedEdgeCaseMethods.Where(name => !methods.Contains(name)).ToArray();

        Assert.True(missing.Length == 0, $"Missing named edge-case test(s): {string.Join(", ", missing)}");
    }

    private static IEnumerable<string> ExpectedRequirementIds() =>
        Enumerable.Range(LowestRequirementNumber, HighestRequirementNumber - LowestRequirementNumber + 1)
            .Select(number => $"RP-{number:D2}");

    private static IReadOnlyList<(string TypeName, string MethodName, string Value)> RequirementTraitsFromSource()
    {
        var traits = new List<(string TypeName, string MethodName, string Value)>();
        foreach (var path in EnumerateTestSources())
        {
            var relative = Path.GetRelativePath(AnalysisTestPaths.RepoRoot, path).Replace('\\', '/');
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

    private static IEnumerable<string> NamedTestMethods()
    {
        foreach (var path in EnumerateTestSources())
        {
            foreach (var line in File.ReadLines(path))
            {
                var match = MethodPattern.Match(line);
                if (match.Success)
                {
                    yield return match.Groups[1].Value;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateTestSources()
    {
        foreach (var project in TestProjects)
        {
            var root = Path.Combine(AnalysisTestPaths.RepoRoot, "tests", project);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildOutputPath(path))
                {
                    continue;
                }

                yield return path;
            }
        }
    }

    private static bool IsBuildOutputPath(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static IEnumerable<string> FindMalformedTraits(
        IEnumerable<(string TypeName, string MethodName, string Value)> traits)
    {
        foreach (var (typeName, methodName, value) in traits)
        {
            if (!value.StartsWith("RP-", StringComparison.Ordinal))
            {
                continue;
            }

            var match = RpIdPattern.Match(value);
            if (!match.Success)
            {
                yield return $"{typeName}.{methodName}: '{value}' does not match the 'RP-NN' format";
                continue;
            }

            var number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (number < LowestRequirementNumber || number > HighestRequirementNumber)
            {
                yield return $"{typeName}.{methodName}: '{value}' is outside the registered RP-{LowestRequirementNumber:D2}..RP-{HighestRequirementNumber:D2} range";
            }
        }
    }
}
