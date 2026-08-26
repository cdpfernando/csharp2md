using System.Reflection;
using Csharp2Md.Analysis.Classification.Configuration;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ConfigurationModelTests
{
    [Fact]
    [Trait("Requirement", "CDC-35")]
    [Trait("Requirement", "CDC-43")]
    public void EqualModels_WithIdenticalContent_AreEqual()
    {
        var left = Sample();
        var right = Sample();

        Assert.Equal(left.Keys[0].KeyPath, right.Keys[0].KeyPath);
        Assert.Equal(left.Keys[0].Resolution, right.Keys[0].Resolution);
        Assert.Equal(left.Keys[0].Address, right.Keys[0].Address);
        Assert.Equal(left.Keys[0].OwningComponent, right.Keys[0].OwningComponent);
        Assert.Equal(left.Keys[0].Evidence, right.Keys[0].Evidence);
        Assert.Equal(left.Edges[0].Source, right.Edges[0].Source);
        Assert.Equal(left.Edges[0].KeyPath, right.Edges[0].KeyPath);
        Assert.Equal(left.Edges[0].Evidence, right.Edges[0].Evidence);
        Assert.Equal(left.Targets[0].Outcome, right.Targets[0].Outcome);
        Assert.Equal(left.Targets[0].Candidate, right.Targets[0].Candidate);
        Assert.Equal(left.UnboundReads[0].Symbol, right.UnboundReads[0].Symbol);
        Assert.Equal(left.Coverage, right.Coverage);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Models_DifferWhenKeyPathDiffers()
    {
        var baseline = Sample();
        var other = baseline with
        {
            Keys = [baseline.Keys[0] with { KeyPath = "Services:Other" }],
        };

        Assert.NotEqual(baseline, other);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void KeyResolution_HasLiteralDynamicAndUnknown()
    {
        Assert.Equal(
            [KeyResolution.Literal, KeyResolution.Dynamic, KeyResolution.Unknown],
            Enum.GetValues<KeyResolution>());
    }

    [Fact]
    [Trait("Requirement", "CDC-43")]
    public void TargetOutcome_HasPromoteFrontierAndLeave()
    {
        Assert.Equal(
            [TargetOutcome.Promote, TargetOutcome.Frontier, TargetOutcome.Leave],
            Enum.GetValues<TargetOutcome>());
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Constructor_DefaultKeys_IsDistinguishableFromEmpty()
    {
        var uninitialized = new ConfigurationModel(
            default,
            [],
            [],
            [],
            new ConfigurationCoverage(0, 0, 0));
        var empty = new ConfigurationModel([], [], [], [], new ConfigurationCoverage(0, 0, 0));

        Assert.True(uninitialized.Keys.IsDefault);
        Assert.False(empty.Keys.IsDefault);
        Assert.Empty(empty.Keys);
        Assert.NotEqual(uninitialized, empty);
    }

    [Fact]
    [Trait("Requirement", "CDC-43")]
    public void Constructor_DefaultTargets_IsDistinguishableFromEmpty()
    {
        var uninitialized = new ConfigurationModel([], [], default, [], new ConfigurationCoverage(0, 0, 0));
        var empty = new ConfigurationModel([], [], [], [], new ConfigurationCoverage(0, 0, 0));

        Assert.True(uninitialized.Targets.IsDefault);
        Assert.False(empty.Targets.IsDefault);
        Assert.NotEqual(uninitialized, empty);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Constructor_DefaultEdges_IsDistinguishableFromEmpty()
    {
        var uninitialized = new ConfigurationModel([], default, [], [], new ConfigurationCoverage(0, 0, 0));
        var empty = new ConfigurationModel([], [], [], [], new ConfigurationCoverage(0, 0, 0));

        Assert.True(uninitialized.Edges.IsDefault);
        Assert.False(empty.Edges.IsDefault);
        Assert.NotEqual(uninitialized, empty);
    }

    [Fact]
    [Trait("Requirement", "CDC-08")]
    [Trait("Requirement", "CDC-35")]
    public void ConfigurationTypes_DoNotReferenceRoslynOrConstructDomainFacts()
    {
        var types = typeof(ConfigurationModel).Assembly.GetTypes()
            .Where(type => type.Namespace == "Csharp2Md.Analysis.Classification.Configuration")
            .ToArray();
        Assert.NotEmpty(types);

        var offending = types
            .SelectMany(type => type.GetMembers(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(MemberTypes)
            .FirstOrDefault(exposed =>
                exposed.Namespace is not null
                && exposed.Namespace.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal));
        Assert.True(offending is null, $"Configuration type leaked Roslyn member '{offending?.FullName}'.");
        Assert.DoesNotContain(
            types,
            type => type.Name is "ConfigurationBinding" or "ConfirmedRelation" or "ExternalSystem");
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void EmptyArrays_AreAValidModel()
    {
        var empty = new ConfigurationModel([], [], [], [], new ConfigurationCoverage(0, 0, 0));

        Assert.Empty(empty.Keys);
        Assert.Empty(empty.Edges);
        Assert.Empty(empty.Targets);
        Assert.Empty(empty.UnboundReads);
        Assert.Equal(0, empty.Coverage.KeysDeclared);
        Assert.Equal(0, empty.Coverage.KeysBound);
        Assert.Equal(0, empty.Coverage.KeysReadButNotDeclared);
    }

    private static ConfigurationModel Sample()
    {
        var project = ProjectId.Create(
            SolutionId.Create(WorkspaceIdentity.Create("acme"), "App.slnx"),
            "App/App.csproj");
        var owner = Project.Create(project).Reference;
        var identity = new ObservationIdentity(
            owner,
            ObservationKind.Configuration,
            NormalizedPayload.Create([]),
            1);
        var chain = EvidenceChain.Create([identity]);
        var candidate = CandidateLink.Create(RelationKind.Targets, owner, owner, chain);
        return new ConfigurationModel(
            [new DeclaredKey("Services:PaymentService", KeyResolution.Literal, "https://payments.example", owner, identity)],
            [new ConfiguredEdge(owner, "Services:PaymentService", chain)],
            [new TargetDecision(candidate, TargetOutcome.Promote, chain)],
            [new UnboundKeyRead(owner, identity)],
            new ConfigurationCoverage(1, 1, 1));
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
