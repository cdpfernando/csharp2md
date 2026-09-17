using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Semantics;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class LogicalEntityAccumulatorTests
{
    [Fact]
    [Trait("Requirement", "VAR-03")]
    public void Add_CompatibleOccurrencesAcrossTfms_ShareOneLogicalEntity()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var accumulator = new LogicalEntityAccumulator(solution);
        var entity = Entity(solution, "Orders");
        var net8 = Occurrence(solution, entity, "net8.0", "shape-a");
        var net10 = Occurrence(solution, entity, "net10.0", "shape-a");

        accumulator.Add(entity, net8);
        accumulator.Add(entity, net10);

        Assert.Single(accumulator.Entities());
        Assert.Equal(2, accumulator.Occurrences().Length);
        Assert.All(accumulator.Occurrences(), occurrence => Assert.Equal(entity.CanonicalKey, occurrence.EntityCanonicalKey));
    }

    [Fact]
    [Trait("Requirement", "VAR-04")]
    public void Add_ShapeDifferenceAcrossVariants_KeepsQualifiedOccurrences()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var accumulator = new LogicalEntityAccumulator(solution);
        var entity = Entity(solution, "Orders");

        accumulator.Add(entity, Occurrence(solution, entity, "net8.0", "shape-net8"));
        accumulator.Add(entity, Occurrence(solution, entity, "net10.0", "shape-net10"));

        Assert.Single(accumulator.Entities());
        Assert.Equal(
            new[] { "shape-net10", "shape-net8" },
            accumulator.Occurrences().Select(occurrence => occurrence.ShapeDigest).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "VAR-05")]
    [Trait("Requirement", "EDG-04")]
    public void Add_IncompatibleShapesWithinSameVariant_Collides()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var accumulator = new LogicalEntityAccumulator(solution);
        var entity = Entity(solution, "Orders");

        accumulator.Add(entity, Occurrence(solution, entity, "net10.0", "shape-a"));
        var exception = Assert.Throws<OccurrenceCollisionException>(
            () => accumulator.Add(entity, Occurrence(solution, entity, "net10.0", "shape-b")));

        Assert.Equal("occurrence-collision", exception.Code);
        Assert.Equal(entity.CanonicalKey, exception.EntityCanonicalKey);
        Assert.Contains("net10.0", exception.VariantKey, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "VAR-05")]
    public void Add_IdenticalShapeWithinSameVariant_IsIdempotent()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var accumulator = new LogicalEntityAccumulator(solution);
        var entity = Entity(solution, "Orders");
        var occurrence = Occurrence(solution, entity, "net10.0", "shape-a");

        accumulator.Add(entity, occurrence);
        accumulator.Add(entity, occurrence);

        Assert.Single(accumulator.Occurrences());
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    public void SeparateSolutions_DoNotShareIdentityOrDeduplicationState()
    {
        var acme = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var widgets = CanonicalIdentity.CreateSolution("widgets", "src/Widgets.sln");
        var left = new LogicalEntityAccumulator(acme);
        var right = new LogicalEntityAccumulator(widgets);
        var leftEntity = Entity(acme, "Orders");
        var rightEntity = Entity(widgets, "Orders");

        left.Add(leftEntity, Occurrence(acme, leftEntity, "net10.0", "shape-a"));
        right.Add(rightEntity, Occurrence(widgets, rightEntity, "net10.0", "shape-a"));

        Assert.NotEqual(left.Entities()[0].CanonicalKey, right.Entities()[0].CanonicalKey);
        Assert.Single(left.Occurrences());
        Assert.Single(right.Occurrences());
    }

    [Fact]
    [Trait("Requirement", "VAR-06")]
    public void Add_EntityFromAnotherSolution_IsRejected()
    {
        var acme = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var widgets = CanonicalIdentity.CreateSolution("widgets", "src/Widgets.sln");
        var accumulator = new LogicalEntityAccumulator(acme);
        var foreign = Entity(widgets, "Orders");

        Assert.Throws<InvalidOperationException>(
            () => accumulator.Add(foreign, Occurrence(widgets, foreign, "net10.0", "shape-a")));
    }

    [Fact]
    [Trait("Requirement", "VAR-03")]
    public void EntitiesAndOccurrences_AreDeterministicallyOrdered()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var accumulator = new LogicalEntityAccumulator(solution);
        var beta = Entity(solution, "Beta");
        var alpha = Entity(solution, "Alpha");

        accumulator.Add(beta, Occurrence(solution, beta, "net10.0", "b"));
        accumulator.Add(alpha, Occurrence(solution, alpha, "net8.0", "a8"));
        accumulator.Add(alpha, Occurrence(solution, alpha, "net10.0", "a10"));

        Assert.Equal(
            new[] { alpha.CanonicalKey, beta.CanonicalKey },
            accumulator.Entities().Select(entity => entity.CanonicalKey));
        Assert.Equal(
            new[] { "a10", "a8", "b" },
            accumulator.Occurrences().Select(occurrence => occurrence.ShapeDigest));
    }

    [Fact]
    [Trait("Requirement", "VAR-04")]
    public void Add_MismatchedOccurrenceEntityKey_IsRejected()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var accumulator = new LogicalEntityAccumulator(solution);
        var orders = Entity(solution, "Orders");
        var payments = Entity(solution, "Payments");
        var mismatched = Occurrence(solution, payments, "net10.0", "shape-a");

        Assert.Throws<ArgumentException>(() => accumulator.Add(orders, mismatched));
    }

    [Fact]
    [Trait("Requirement", "VAR-03")]
    public void Add_PreservesLogicalDisplayMetadataAcrossVariants()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var accumulator = new LogicalEntityAccumulator(solution);
        var entity = Entity(solution, "Orders");

        accumulator.Add(entity, Occurrence(solution, entity, "net8.0", "shape-a"));
        accumulator.Add(entity, Occurrence(solution, entity, "net10.0", "shape-a"));

        var retained = Assert.Single(accumulator.Entities());
        Assert.Equal(EntityKind.Component, retained.Kind);
        Assert.Equal("Orders", retained.DisplayName);
        Assert.Equal("Acme.Orders", retained.QualifiedName);
    }

    [Fact]
    [Trait("Requirement", "VAR-05")]
    public void Add_ChangingLogicalMetadataAcrossVariants_IsRejected()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var accumulator = new LogicalEntityAccumulator(solution);
        var first = Entity(solution, "Orders");
        var second = new LogicalEntity(EntityKind.Component, first.CanonicalKey, "OrdersService", "Acme.Orders");

        accumulator.Add(first, Occurrence(solution, first, "net8.0", "shape-a"));
        Assert.Throws<InvalidOperationException>(
            () => accumulator.Add(second, Occurrence(solution, first, "net10.0", "shape-a")));
    }

    private static LogicalEntity Entity(SolutionIdentity solution, string name) =>
        new(
            EntityKind.Component,
            CanonicalIdentity.CreateEntityKey(solution, EntityKind.Component, name),
            name,
            "Acme." + name);

    private static VariantOccurrence Occurrence(
        SolutionIdentity solution,
        LogicalEntity entity,
        string tfm,
        string shapeDigest)
    {
        var project = CanonicalIdentity.CreateProject(solution, "src/Orders/Orders.csproj");
        var variant = CanonicalIdentity.CreateVariant(tfm, "Release", ["TRACE"], "ci");
        var locator = CanonicalIdentity.CreateLocator("src/Orders/Orders.cs", new SourceSpan(1, 1, 2, 1), project);
        return new VariantOccurrence(
            entity.CanonicalKey,
            project,
            variant,
            locator,
            shapeDigest,
            ImmutableArray.Create("ev-1"));
    }
}
