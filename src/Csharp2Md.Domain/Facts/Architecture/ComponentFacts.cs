using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Facts;

public sealed record Component : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Architecture;

    public SolutionId Solution { get; }

    public string Name { get; }

    public ImmutableArray<FactReference> Owners { get; }

    private Component(FactReference reference, SolutionId solution, string name, ImmutableArray<FactReference> owners)
    {
        Reference = reference;
        Solution = solution;
        Name = name;
        Owners = owners;
    }

    public static Component Create(SolutionId solution, string name, IEnumerable<FactReference> owners)
    {
        FactGuards.RequireInitialized(solution, nameof(solution));
        var canonicalName = FactIdGrammar.RequireCanonicalText(name, nameof(name));
        ArgumentNullException.ThrowIfNull(owners);

        var orderedOwners = owners
            .Distinct()
            .OrderBy(owner => owner.Id.Value, StringComparer.Ordinal)
            .ToImmutableArray();

        var id = FactIdGrammar.Create("component", ("solution", solution.Value), ("name", canonicalName));
        var reference = new FactReference(id, nameof(Component));
        return new Component(reference, solution, canonicalName, orderedOwners);
    }

    public bool Equals(Component? other) =>
        other is not null
        && Reference.Equals(other.Reference)
        && Solution.Equals(other.Solution)
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && Owners.AsSpan().SequenceEqual(other.Owners.AsSpan());

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Reference);
        hash.Add(Solution);
        hash.Add(Name);
        foreach (var owner in Owners)
        {
            hash.Add(owner);
        }

        return hash.ToHashCode();
    }
}

public sealed record DeploymentUnit : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Architecture;

    public SolutionId Solution { get; }

    public string Name { get; }

    private DeploymentUnit(FactReference reference, SolutionId solution, string name)
    {
        Reference = reference;
        Solution = solution;
        Name = name;
    }

    public static DeploymentUnit Create(SolutionId solution, string name)
    {
        FactGuards.RequireInitialized(solution, nameof(solution));
        var canonicalName = FactIdGrammar.RequireCanonicalText(name, nameof(name));

        var id = FactIdGrammar.Create("deployment-unit", ("solution", solution.Value), ("name", canonicalName));
        var reference = new FactReference(id, nameof(DeploymentUnit));
        return new DeploymentUnit(reference, solution, canonicalName);
    }
}

public sealed record ExternalSystem : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Architecture;

    public SolutionId Solution { get; }

    public StructuralLiteral Name { get; }

    private ExternalSystem(FactReference reference, SolutionId solution, StructuralLiteral name)
    {
        Reference = reference;
        Solution = solution;
        Name = name;
    }

    public static ExternalSystem Create(SolutionId solution, StructuralLiteral name)
    {
        FactGuards.RequireInitialized(solution, nameof(solution));
        FactGuards.RequireInitialized(name, nameof(name));
        if (name.Role != LiteralRole.ClientName)
        {
            throw new ArgumentException(
                $"An external system's name must be a structural literal with role '{nameof(LiteralRole.ClientName)}', but was '{name.Role}'.",
                nameof(name));
        }

        var id = FactIdGrammar.Create("external-system", ("solution", solution.Value), ("name", name.Value));
        var reference = new FactReference(id, nameof(ExternalSystem));
        return new ExternalSystem(reference, solution, name);
    }
}
