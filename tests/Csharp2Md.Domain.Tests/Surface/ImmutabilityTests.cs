using System.Reflection;
using System.Runtime.CompilerServices;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Domain.Tests.Surface;

/// <summary>
/// Proves TAX-91 over the whole assembly, not a hand-listed subset of taxonomy types: no property may expose a
/// plain (non-<c>init</c>) setter, no field may be publicly or internally mutable, and no collection-typed
/// member may be a mutable shape (<c>List&lt;T&gt;</c>, an array, or a mutable <c>IList</c>/<c>ICollection</c>)
/// rather than an <c>ImmutableArray</c> or an immutable set. <see cref="Observation"/> is asserted separately,
/// closing TAX-41 at its own criterion.
/// </summary>
public sealed class ImmutabilityTests
{
    private static readonly Type[] MutableCollectionGenericDefinitions =
    [
        typeof(List<>),
        typeof(IList<>),
        typeof(ICollection<>),
        typeof(HashSet<>),
        typeof(ISet<>),
        typeof(Dictionary<,>),
        typeof(IDictionary<,>),
    ];

    private static Assembly DomainAssembly => typeof(AssemblyMarker).Assembly;

    private sealed class DecoyWithSettableProperty
    {
        public int MutableValue { get; set; }
    }

    [Fact]
    [Trait("Requirement", "TAX-91")]
    public void Domain_HasNoSettablePropertyNoMutableFieldAndNoMutableCollectionMember()
    {
        var violations = ScanMutabilityViolations(DomainTypes()).ToArray();

        Assert.True(
            violations.Length == 0,
            $"Mutable member(s) found on the taxonomy: {string.Join(", ", violations)}");
    }

    [Fact]
    [Trait("Requirement", "TAX-41")]
    public void Observation_AndItsIdentity_ExposeNoMutatingMember()
    {
        var violations = ScanMutabilityViolations([typeof(Observation), typeof(ObservationIdentity)]).ToArray();

        Assert.True(
            violations.Length == 0,
            $"Observation contract is not immutable: {string.Join(", ", violations)}");
    }

    [Fact]
    [Trait("Requirement", "TAX-91")]
    public void Scanner_FlagsASettableProperty_OnADecoyType_ProvingItIsNotVacuous()
    {
        var violations = ScanMutabilityViolations([typeof(DecoyWithSettableProperty)]).ToArray();

        var violation = Assert.Single(violations);
        Assert.Contains(nameof(DecoyWithSettableProperty.MutableValue), violation, StringComparison.Ordinal);
    }

    private static IEnumerable<Type> DomainTypes() =>
        DomainAssembly.GetTypes()
            .Where(type => type.Namespace is not null && type.Namespace.StartsWith("Csharp2Md.Domain", StringComparison.Ordinal))
            .Where(type => !Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute)))
            .Where(type => !type.Name.Contains('<', StringComparison.Ordinal));

    private static IEnumerable<string> ScanMutabilityViolations(IEnumerable<Type> types)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in types)
        {
            foreach (var property in type.GetProperties(Flags))
            {
                if (!IsExposed(property))
                {
                    continue;
                }

                if (HasNonInitSetter(property))
                {
                    yield return $"{type.FullName}.{property.Name} has a settable (non-init) property setter";
                }

                if (IsMutableCollectionType(property.PropertyType))
                {
                    yield return $"{type.FullName}.{property.Name} is a mutable collection type '{property.PropertyType}'";
                }
            }

            foreach (var field in type.GetFields(Flags))
            {
                if (field.IsSpecialName || !IsExposed(field))
                {
                    continue;
                }

                if (!field.IsLiteral && !field.IsInitOnly)
                {
                    yield return $"{type.FullName}.{field.Name} is a mutable field";
                }

                if (IsMutableCollectionType(field.FieldType))
                {
                    yield return $"{type.FullName}.{field.Name} is a mutable collection type '{field.FieldType}'";
                }
            }
        }
    }

    private static bool HasNonInitSetter(PropertyInfo property)
    {
        var setter = property.SetMethod;
        if (setter is null || !(setter.IsPublic || setter.IsAssembly || setter.IsFamilyOrAssembly))
        {
            return false;
        }

        var isInitOnly = setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => modifier == typeof(IsExternalInit));

        return !isInitOnly;
    }

    private static bool IsMutableCollectionType(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return MutableCollectionGenericDefinitions.Contains(definition);
    }

    private static bool IsExposed(FieldInfo field) => field.IsPublic || field.IsAssembly || field.IsFamilyOrAssembly;

    private static bool IsExposed(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        return accessor is not null && (accessor.IsPublic || accessor.IsAssembly || accessor.IsFamilyOrAssembly);
    }
}
