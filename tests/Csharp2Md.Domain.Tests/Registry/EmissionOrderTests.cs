using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class EmissionOrderTests
{
    private static readonly TaxonomyTables Tables = TaxonomyTables.Default;

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void TaxonomyTables_EveryPublicPropertyType_IsAnImmutableArrayOrAPlainValueRecord()
    {
        var properties = typeof(TaxonomyTables).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var type = property.PropertyType;
            var isOk = IsImmutableArray(type) || type == typeof(TaxonomyVersions);

            Assert.True(isOk, $"'{property.Name}' has type '{type}', which is neither ImmutableArray<T> nor a recognized plain value record.");
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void NoFrozenSetOrFrozenDictionary_IsReachableFromTheWritersProjectionPath()
    {
        var frozenMembers = ReachableTypes(typeof(TaxonomyTables))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => IsFrozenCollection(property.PropertyType))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}: {property.PropertyType}")
            .ToArray();

        Assert.Empty(frozenMembers);
    }

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_FactTypesAppearInDeclaredOrder() =>
        AssertObjectArrayOrder("fact_types", "name", [.. Tables.FactTypes.Select(f => f.Name)]);

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_RelationsAppearInDeclaredOrder() =>
        AssertObjectArrayOrder("relations", "kind", [.. Tables.Relations.Select(r => r.Kind.ToString())]);

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_ObservationKindsAppearInDeclaredOrder() =>
        AssertObjectArrayOrder("observation_kinds", "kind", [.. Tables.ObservationKinds.Select(k => k.Kind.ToString())]);

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_FacetAxesAppearInDeclaredOrder() =>
        AssertObjectArrayOrder("facet_axes", "name", [.. Tables.FacetAxes.Select(a => a.Name)]);

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_ProofAxesAppearInDeclaredOrder() =>
        AssertObjectArrayOrder("proof_axes", "name", [.. Tables.ProofAxes.Select(a => a.Name)]);

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_MappingRolesAppearInDeclaredOrder() =>
        AssertStringArrayOrder("mapping_roles", Tables.MappingRoles.ToArray());

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_PayloadRolesAppearInDeclaredOrder() =>
        AssertStringArrayOrder("payload_roles", Tables.PayloadRoles.ToArray());

    private static void AssertObjectArrayOrder(string arrayProperty, string keyProperty, string[] declaredKeys)
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));
        var emittedKeys = document.RootElement.GetProperty(arrayProperty).EnumerateArray()
            .Select(element => element.GetProperty(keyProperty).GetString())
            .ToArray();

        Assert.Equal(declaredKeys.Length, emittedKeys.Length);
        for (var i = 0; i < declaredKeys.Length; i++)
        {
            Assert.Equal(declaredKeys[i], emittedKeys[i]);
        }
    }

    private static void AssertStringArrayOrder(string arrayProperty, string[] declaredValues)
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));
        var emittedValues = document.RootElement.GetProperty(arrayProperty).EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();

        Assert.Equal(declaredValues.Length, emittedValues.Length);
        for (var i = 0; i < declaredValues.Length; i++)
        {
            Assert.Equal(declaredValues[i], emittedValues[i]);
        }
    }

    private static IEnumerable<Type> ReachableTypes(Type root)
    {
        var visited = new HashSet<Type>();
        var pending = new Queue<Type>();
        pending.Enqueue(root);

        while (pending.Count > 0)
        {
            var type = pending.Dequeue();
            if (!visited.Add(type))
            {
                continue;
            }

            yield return type;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyType = property.PropertyType;
                var nextType = IsImmutableArray(propertyType) ? propertyType.GetGenericArguments()[0] : propertyType;

                if (!IsPlainValue(nextType))
                {
                    pending.Enqueue(nextType);
                }
            }
        }
    }

    private static bool IsImmutableArray(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>);

    private static bool IsFrozenCollection(Type type) =>
        type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(FrozenSet<>) || type.GetGenericTypeDefinition() == typeof(FrozenDictionary<,>));

    private static bool IsPlainValue(Type type) =>
        type.IsPrimitive || type == typeof(string) || type.IsEnum;
}
