using System.Text.RegularExpressions;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class ConfirmedRelationCreateGuardReachabilityTests
{
    public static IEnumerable<object[]> ShapeGuardNames() =>
    [
        [nameof(RelationShapeGuards.RequireCallableIfNeeded)],
        [nameof(RelationShapeGuards.RequireLegalTargetShape)],
        [nameof(RelationShapeGuards.RequireSufficientEvidence)],
    ];

    [Theory]
    [MemberData(nameof(ShapeGuardNames))]
    [Trait("Requirement", "ENG-59")]
    public void Create_InvokesEachRelationShapeGuardFromALiveCallSite(string guardName)
    {
        var createBody = CreateMethodSource();
        var invocation = $"RelationShapeGuards.{guardName}(";

        var liveCalls = createBody
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal) && !line.StartsWith("*", StringComparison.Ordinal))
            .Where(line => line.Contains(invocation, StringComparison.Ordinal))
            .ToArray();

        Assert.False(string.IsNullOrWhiteSpace(createBody), "Could not isolate ConfirmedRelation.Create source.");
        Assert.True(
            liveCalls.Length >= 1,
            $"ConfirmedRelation.Create has no live call site for RelationShapeGuards.{guardName}.");
    }

    private static string CreateMethodSource()
    {
        var path = Path.Combine(
            DomainTestPaths.RepoRoot, "src", "Csharp2Md.Domain", "Relations", "ConfirmedRelation.cs");
        var source = File.ReadAllText(path);
        var match = Regex.Match(
            source,
            @"public static ConfirmedRelation Create\s*\(.*?\n    \}",
            RegexOptions.Singleline);

        Assert.True(match.Success, "ConfirmedRelation.cs does not contain a Create method body.");
        return match.Value;
    }
}
