using System.Reflection;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests;

public sealed class SolutionContributionBoundTests
{
    private static readonly Type[] ForbiddenTypes =
    [
        typeof(WireDocument),
        typeof(PublishedPackageView),
        typeof(ObservationDto),
        typeof(SymbolDto),
        typeof(ConfirmedRelation),
        typeof(CandidateLink),
        typeof(byte),
        typeof(byte[]),
        typeof(ImmutableArray<byte>),
        typeof(ReadOnlyMemory<byte>),
        typeof(Memory<byte>),
        typeof(IReadOnlyList<byte>),
        typeof(IEnumerable<byte>),
    ];

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void ForbiddenTypeList_DeclaresEveryNamedForbiddenTypeExplicitly()
    {
        Assert.Contains(typeof(WireDocument), ForbiddenTypes);
        Assert.Contains(typeof(PublishedPackageView), ForbiddenTypes);
        Assert.Contains(typeof(ObservationDto), ForbiddenTypes);
        Assert.Contains(typeof(SymbolDto), ForbiddenTypes);
        Assert.Contains(typeof(ConfirmedRelation), ForbiddenTypes);
        Assert.Contains(typeof(CandidateLink), ForbiddenTypes);
        Assert.Contains(typeof(byte), ForbiddenTypes);
        Assert.Contains(typeof(byte[]), ForbiddenTypes);
        Assert.Contains(typeof(ImmutableArray<byte>), ForbiddenTypes);
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void PropertyTypeClosure_ContainsNoneOfTheForbiddenTypes()
    {
        var reachable = WalkPropertyTypes(typeof(SolutionContribution));
        var hits = ForbiddenTypes
            .Where(forbidden => reachable.Contains(forbidden))
            .Select(forbidden => forbidden.FullName)
            .ToArray();

        Assert.True(
            hits.Length == 0,
            "SolutionContribution must not reach " + string.Join(", ", hits) + ".");
        Assert.DoesNotContain(reachable, IsByteSequence);
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void SolutionContribution_ExposesOnlyTheCatalogIdentitiesCompositionMayRead()
    {
        Assert.Equal(typeof(string), PropertyType(typeof(SolutionContribution), nameof(SolutionContribution.SolutionIdentity)));
        Assert.Equal(typeof(string), PropertyType(typeof(SolutionContribution), nameof(SolutionContribution.SolutionFileName)));
        Assert.Equal(typeof(string), PropertyType(typeof(SolutionContribution), nameof(SolutionContribution.PackageDirectory)));
        Assert.Equal(
            typeof(ImmutableArray<ContributedBoundaryOperation>),
            PropertyType(typeof(SolutionContribution), nameof(SolutionContribution.BoundaryOperations)));
        Assert.Equal(
            typeof(ImmutableArray<ContributedIdentity>),
            PropertyType(typeof(SolutionContribution), nameof(SolutionContribution.Contracts)));
        Assert.Equal(
            typeof(ImmutableArray<ContributedNamedIdentity>),
            PropertyType(typeof(SolutionContribution), nameof(SolutionContribution.Components)));
        Assert.Equal(
            typeof(ImmutableArray<ContributedNamedIdentity>),
            PropertyType(typeof(SolutionContribution), nameof(SolutionContribution.DeploymentUnits)));
        Assert.Equal(
            typeof(ImmutableArray<ContributedNamedIdentity>),
            PropertyType(typeof(SolutionContribution), nameof(SolutionContribution.ExternalSystems)));
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void ContributedBoundaryOperation_CarriesDirectionProtocolKeyScopeHttpAndLocator()
    {
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.FactId)));
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.FactType)));
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.Direction)));
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.Protocol)));
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.DestinationScope)));
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.HttpMethod)));
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.Route)));
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.ProtocolOperationKey)));
        Assert.Equal(typeof(string), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.ArtifactKey)));
        Assert.Equal(typeof(int), PropertyType(typeof(ContributedBoundaryOperation), nameof(ContributedBoundaryOperation.Ordinal)));
    }

    private static Type PropertyType(Type owner, string name)
    {
        var property = owner.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
    }

    private static HashSet<Type> WalkPropertyTypes(Type root)
    {
        var reachable = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = Unwrap(pending.Pop());
            if (current is null || !reachable.Add(current))
            {
                continue;
            }

            foreach (var argument in current.IsGenericType ? current.GetGenericArguments() : [])
            {
                pending.Push(argument);
            }

            if (current.IsArray)
            {
                var element = current.GetElementType();
                if (element is not null)
                {
                    pending.Push(element);
                }
            }

            if (current.Assembly != typeof(SolutionContribution).Assembly)
            {
                continue;
            }

            foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                pending.Push(property.PropertyType);
            }
        }

        return reachable;
    }

    private static Type Unwrap(Type type)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType() ?? type;
        }

        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static bool IsByteSequence(Type type)
    {
        if (type == typeof(byte) || type == typeof(byte[]))
        {
            return true;
        }

        if (type.IsArray && type.GetElementType() == typeof(byte))
        {
            return true;
        }

        return type.IsGenericType
            && type.GetGenericArguments() is [var argument]
            && argument == typeof(byte);
    }
}
