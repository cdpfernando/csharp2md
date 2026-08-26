using System.Reflection;
using Csharp2Md.Analysis.Classification.Topology;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class TopologyModelTests
{
    [Fact]
    [Trait("Requirement", "CDC-09")]
    [Trait("Requirement", "CDC-19")]
    public void EqualModels_WithIdenticalContent_AreEqual()
    {
        var left = Sample();
        var right = Sample();

        Assert.Equal(left.Groups[0].ComponentName, right.Groups[0].ComponentName);
        Assert.Equal(left.Groups[0].Evidence, right.Groups[0].Evidence);
        Assert.Equal(left.Groups[0].Projects.ToArray(), right.Groups[0].Projects.ToArray());
        Assert.Equal(left.Deployments[0].Name, right.Deployments[0].Name);
        Assert.Equal(left.Deployments[0].Application, right.Deployments[0].Application);
        Assert.Equal(left.Inclusions[0].ComponentName, right.Inclusions[0].ComponentName);
        Assert.Equal(left.Inclusions[0].DeploymentName, right.Inclusions[0].DeploymentName);
        Assert.Equal(left.Inclusions[0].Evidence, right.Inclusions[0].Evidence);
        Assert.Equal(left.Unreached[0].ComponentName, right.Unreached[0].ComponentName);
        Assert.Equal(left.Unreached[0].Evidence, right.Unreached[0].Evidence);
        Assert.Equal(left.Coverage, right.Coverage);
    }

    [Fact]
    [Trait("Requirement", "CDC-11")]
    public void Models_DifferWhenComponentNameDiffers()
    {
        var baseline = Sample();
        var other = baseline with
        {
            Groups = [baseline.Groups[0] with { ComponentName = "other/other.csproj" }],
        };

        Assert.NotEqual(baseline, other);
    }

    [Fact]
    [Trait("Requirement", "CDC-10")]
    [Trait("Requirement", "CDC-12")]
    public void GroupingEvidence_HasTheFourGroupingStates()
    {
        Assert.Equal(
            [GroupingEvidence.Deployable, GroupingEvidence.PrivateUse, GroupingEvidence.Shared, GroupingEvidence.Unreached],
            Enum.GetValues<GroupingEvidence>());
    }

    [Fact]
    [Trait("Requirement", "CDC-09")]
    public void Constructor_DefaultGroups_IsDistinguishableFromEmpty()
    {
        var uninitialized = new TopologyModel(
            default,
            [],
            [],
            [],
            new TopologyCoverage(0, 0, 0));
        var empty = new TopologyModel([], [], [], [], new TopologyCoverage(0, 0, 0));

        Assert.True(uninitialized.Groups.IsDefault);
        Assert.False(empty.Groups.IsDefault);
        Assert.Empty(empty.Groups);
        Assert.NotEqual(uninitialized, empty);
    }

    [Fact]
    [Trait("Requirement", "CDC-20")]
    public void Constructor_DefaultInclusions_IsDistinguishableFromEmpty()
    {
        var uninitialized = new TopologyModel([], [], default, [], new TopologyCoverage(0, 0, 0));
        var empty = new TopologyModel([], [], [], [], new TopologyCoverage(0, 0, 0));

        Assert.True(uninitialized.Inclusions.IsDefault);
        Assert.False(empty.Inclusions.IsDefault);
        Assert.NotEqual(uninitialized, empty);
    }

    [Fact]
    [Trait("Requirement", "CDC-09")]
    public void ComponentGroup_DefaultProjects_IsDistinguishableFromEmpty()
    {
        var uninitialized = new ComponentGroup("App/App.csproj", GroupingEvidence.Deployable, default);
        var empty = new ComponentGroup("App/App.csproj", GroupingEvidence.Deployable, []);

        Assert.True(uninitialized.Projects.IsDefault);
        Assert.False(empty.Projects.IsDefault);
        Assert.NotEqual(uninitialized, empty);
    }

    [Fact]
    [Trait("Requirement", "CDC-08")]
    public void TopologyTypes_DoNotReferenceRoslynOrConstructDomainFacts()
    {
        var types = typeof(TopologyModel).Assembly.GetTypes()
            .Where(type => type.Namespace == "Csharp2Md.Analysis.Classification.Topology")
            .ToArray();
        Assert.NotEmpty(types);

        var offending = types
            .SelectMany(type => type.GetMembers(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(MemberTypes)
            .FirstOrDefault(exposed =>
                exposed.Namespace is not null
                && exposed.Namespace.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal));
        Assert.True(offending is null, $"Topology type leaked Roslyn member '{offending?.FullName}'.");
        Assert.DoesNotContain(
            types,
            type => type.Name is "Component" or "DeploymentUnit" or "ConfirmedRelation");
    }

    [Fact]
    [Trait("Requirement", "CDC-18")]
    public void EmptyArrays_AreAValidModel()
    {
        var empty = new TopologyModel([], [], [], [], new TopologyCoverage(0, 0, 0));

        Assert.Empty(empty.Groups);
        Assert.Empty(empty.Deployments);
        Assert.Empty(empty.Inclusions);
        Assert.Empty(empty.Unreached);
        Assert.Equal(0, empty.Coverage.ProjectsGrouped);
        Assert.Equal(0, empty.Coverage.ApplicationsFound);
        Assert.Equal(0, empty.Coverage.ComponentsWithoutDeployment);
    }

    private static TopologyModel Sample()
    {
        var project = ProjectId.Create(
            SolutionId.Create(WorkspaceIdentity.Create("acme"), "App.slnx"),
            "App/App.csproj");
        var chain = EvidenceChain.Create(
            [new ObservationIdentity(
                Project.Create(project).Reference,
                ObservationKind.Configuration,
                NormalizedPayload.Create([]),
                1)]);
        return new TopologyModel(
            [new ComponentGroup("App/App.csproj", GroupingEvidence.Deployable, [project])],
            [new DeploymentNode("App/App.csproj", project)],
            [new InclusionEdge("App/App.csproj", "App/App.csproj", chain)],
            [new UnreachedComponent("Lib/Lib.csproj", chain)],
            new TopologyCoverage(1, 1, 1));
    }

    private static IEnumerable<Type> MemberTypes(MemberInfo member) =>
        member switch
        {
            FieldInfo field => [field.FieldType],
            PropertyInfo property => [property.PropertyType],
            MethodInfo method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType),
            ConstructorInfo ctor => ctor.GetParameters().Select(parameter => parameter.ParameterType),
            _ => [],
        };
}
