using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class IdentityPrimitivesTests
{
    [Fact]
    [Trait("Requirement", "VAR-06")]
    [Trait("Requirement", "STO-04")]
    public void SolutionKey_SameLogicalInputsUnderTwoAbsoluteRoots_AreIdentical()
    {
        var underCloneA = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var underCloneB = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");

        Assert.Equal(underCloneA.CanonicalKey, underCloneB.CanonicalKey);
        Assert.DoesNotContain("C:", underCloneA.CanonicalKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/Users", underCloneA.CanonicalKey, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", underCloneA.CanonicalKey, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    public void SolutionKey_DifferentLogicalNames_AreDistinct()
    {
        var acme = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var widgets = CanonicalIdentity.CreateSolution("widgets", "src/Acme.sln");

        Assert.NotEqual(acme.CanonicalKey, widgets.CanonicalKey);
        Assert.Contains("acme", acme.CanonicalKey, StringComparison.Ordinal);
        Assert.Contains("widgets", widgets.CanonicalKey, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    public void ProjectKey_IsScopedToTheSolutionAndIgnoresCheckoutRoot()
    {
        var first = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var second = CanonicalIdentity.CreateSolution("acme", "src/Other.sln");
        var projectUnderFirst = CanonicalIdentity.CreateProject(first, "src/Orders/Orders.csproj");
        var samePathUnderSecond = CanonicalIdentity.CreateProject(second, "src/Orders/Orders.csproj");
        var againUnderFirst = CanonicalIdentity.CreateProject(first, "src/Orders/Orders.csproj");

        Assert.Equal(projectUnderFirst.CanonicalKey, againUnderFirst.CanonicalKey);
        Assert.NotEqual(projectUnderFirst.CanonicalKey, samePathUnderSecond.CanonicalKey);
        Assert.Contains(first.CanonicalKey, projectUnderFirst.CanonicalKey, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetTempPath().Replace('\\', '/'), projectUnderFirst.CanonicalKey, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "VAR-03")]
    public void EntityKey_IsLogicalAndSharedAcrossVariants()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var net8 = CanonicalIdentity.CreateEntityKey(solution, EntityKind.Component, "Orders");
        var net10 = CanonicalIdentity.CreateEntityKey(solution, EntityKind.Component, "Orders");

        Assert.Equal(net8, net10);
        Assert.Contains(solution.CanonicalKey, net8, StringComparison.Ordinal);
        Assert.Contains("component", net8, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "VAR-03")]
    public void EntityKey_DifferentSolutions_DoNotShareIdentity()
    {
        var acme = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var widgets = CanonicalIdentity.CreateSolution("widgets", "src/Widgets.sln");

        Assert.NotEqual(
            CanonicalIdentity.CreateEntityKey(acme, EntityKind.Component, "Orders"),
            CanonicalIdentity.CreateEntityKey(widgets, EntityKind.Component, "Orders"));
    }

    [Fact]
    [Trait("Requirement", "VAR-04")]
    public void VariantIdentity_IsDeterministicUnderShuffledSymbols()
    {
        var forward = CanonicalIdentity.CreateVariant("net10.0", "Release", ["TRACE", "DEBUG", "BETA"], "ci");
        var shuffled = CanonicalIdentity.CreateVariant("net10.0", "Release", ["BETA", "TRACE", "DEBUG"], "ci");

        Assert.Equal(forward.TargetFramework, shuffled.TargetFramework);
        Assert.Equal<IEnumerable<string>>(forward.Symbols, shuffled.Symbols);
        Assert.Equal(
            CanonicalIdentity.VariantKey(forward),
            CanonicalIdentity.VariantKey(shuffled));
    }

    [Theory]
    [Trait("Requirement", "PKG-07")]
    [InlineData("/repo/Acme.sln")]
    [InlineData("C:/repo/Acme.sln")]
    [InlineData("src\\Acme.sln")]
    [InlineData("src/../Acme.sln")]
    [InlineData("src/./Acme.sln")]
    [InlineData("src//Acme.sln")]
    public void SolutionAndLocator_RootedEscapingOrNonNormalizedPaths_AreRejected(string path)
    {
        var solutionException = Assert.Throws<ArgumentException>(
            () => CanonicalIdentity.CreateSolution("acme", path));
        Assert.Equal("logicalRelativePath", solutionException.ParamName);

        var locatorException = Assert.Throws<ArgumentException>(
            () => CanonicalIdentity.CreateLocator(
                path,
                new SourceSpan(1, 1, 1, 2),
                CanonicalIdentity.CreateProject(
                    CanonicalIdentity.CreateSolution("acme", "src/Acme.sln"),
                    "src/Orders/Orders.csproj")));
        Assert.Equal("relativePath", locatorException.ParamName);
    }

    [Fact]
    [Trait("Requirement", "PKG-07")]
    public void LogicalLocator_ForwardSlashRelativePath_IsAcceptedAndHasNoAbsolutePrefix()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/Orders/Orders.csproj");
        var locator = CanonicalIdentity.CreateLocator("src/Orders/Orders.cs", new SourceSpan(4, 1, 10, 2), project);

        Assert.Equal("src/Orders/Orders.cs", locator.RelativePath);
        Assert.Equal(project, locator.Project);
        Assert.DoesNotContain(":", locator.RelativePath, StringComparison.Ordinal);
        Assert.False(Path.IsPathRooted(locator.RelativePath));
    }

    [Fact]
    [Trait("Requirement", "STO-04")]
    public void RepeatedIdentitiesWithinASolution_CompareEqualForDeduplication()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var first = CanonicalIdentity.CreateProject(solution, "src/Orders/Orders.csproj");
        var second = CanonicalIdentity.CreateProject(solution, "src/Orders/Orders.csproj");
        var document = CanonicalIdentity.CreateDocumentKey(solution, "src/Orders/Orders.cs");

        Assert.Equal(first, second);
        Assert.Contains(solution.CanonicalKey, document, StringComparison.Ordinal);
        Assert.Equal(document, CanonicalIdentity.CreateDocumentKey(solution, "src/Orders/Orders.cs"));
    }

    [Theory]
    [Trait("Requirement", "VAR-06")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" acme")]
    [InlineData("acme ")]
    public void CanonicalName_NonCanonicalText_IsRejected(string logicalName)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CanonicalIdentity.CreateSolution(logicalName, "src/Acme.sln"));
        Assert.Equal("logicalName", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "PKG-07")]
    public void CanonicalKeys_NeverEmbedAbsoluteCheckoutPaths()
    {
        var checkout = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "clone-a", "Acme.sln"));
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/Orders/Orders.csproj");
        var entity = CanonicalIdentity.CreateEntityKey(solution, EntityKind.Component, "Orders");

        Assert.DoesNotContain(checkout, solution.CanonicalKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(checkout, project.CanonicalKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(checkout, entity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clone-a", solution.CanonicalKey, StringComparison.Ordinal);
    }
}
