using System.Reflection;

namespace Csharp2Md.Domain.Tests.Isolation;

/// <summary>
/// Every test in this feature carries <c>[Trait("Requirement", "TAX-nn")]</c>, naming the spec.md acceptance
/// criterion it proves, so a failure can be traced back to a requirement without reading the test body.
/// </summary>
public sealed class DomainIsolationTests
{
    private static readonly string[] ForbiddenSurfaceNamespaces =
    [
        "Microsoft.CodeAnalysis",
        "Microsoft.Build",
        "System.Text.Json",
        "System.IO",
    ];

    private static readonly string[] ForbiddenReferencedAssemblies =
    [
        "Microsoft.CodeAnalysis",
        "Microsoft.Build",
        "System.Text.Json",
    ];

    private static Assembly DomainAssembly => typeof(AssemblyMarker).Assembly;

    public static IEnumerable<object[]> ForbiddenSurfaceNamespaceCases() =>
        ForbiddenSurfaceNamespaces.Select(ns => new object[] { ns });

    public static IEnumerable<object[]> ForbiddenReferencedAssemblyCases() =>
        ForbiddenReferencedAssemblies.Select(name => new object[] { name });

    [Theory]
    [Trait("Requirement", "TAX-03")]
    [MemberData(nameof(ForbiddenSurfaceNamespaceCases))]
    public void PublicOrInternalSurface_DoesNotExposeForbiddenNamespace(string forbiddenNamespace)
    {
        var offendingType = DomainAssembly.GetTypes()
            .Where(IsPublicOrInternal)
            .SelectMany(ExposedMemberTypes)
            .FirstOrDefault(exposed => BelongsToNamespace(exposed, forbiddenNamespace));

        Assert.True(
            offendingType is null,
            $"Type '{offendingType?.FullName}' from forbidden namespace '{forbiddenNamespace}' is exposed by a public or internal member of Csharp2Md.Domain.");
    }

    [Theory]
    [Trait("Requirement", "TAX-03")]
    [MemberData(nameof(ForbiddenReferencedAssemblyCases))]
    public void ReferencedAssemblies_DoNotIncludeForbiddenAssembly(string forbiddenAssemblyPrefix)
    {
        var offendingAssembly = DomainAssembly.GetReferencedAssemblies()
            .FirstOrDefault(reference => reference.Name is not null
                && reference.Name.StartsWith(forbiddenAssemblyPrefix, StringComparison.Ordinal));

        Assert.True(
            offendingAssembly?.Name is null,
            $"Assembly '{offendingAssembly?.Name}' from forbidden namespace '{forbiddenAssemblyPrefix}' is referenced by Csharp2Md.Domain.");
    }

    [Fact]
    [Trait("Requirement", "TAX-06")]
    public void Domain_DoesNotReferenceCore()
    {
        var reference = DomainAssembly.GetReferencedAssemblies()
            .FirstOrDefault(a => a.Name == "Csharp2Md.Core");

        Assert.True(reference?.Name is null, "Csharp2Md.Domain must not reference Csharp2Md.Core.");
    }

    [Fact]
    [Trait("Requirement", "ENG-47")]
    public void CoreTestsDirectory_Exists()
    {
        var path = Path.Combine(DomainTestPaths.RepoRoot, "tests", "Csharp2Md.Core.Tests");
        Assert.True(
            Directory.Exists(path),
            $"Repository must contain '{path}'.");
    }

    [Fact]
    [Trait("Requirement", "ENG-46")]
    public void CoreDirectory_Exists()
    {
        var path = Path.Combine(DomainTestPaths.RepoRoot, "src", "Csharp2Md.Core");
        Assert.True(
            Directory.Exists(path),
            $"Repository must contain '{path}'.");
    }

    [Fact]
    [Trait("Requirement", "ENG-48")]
    public void RetrievalIndexBenchmarksDirectory_DoesNotExist()
    {
        var path = Path.Combine(DomainTestPaths.RepoRoot, "benchmarks", "Csharp2Md.RetrievalIndex.Benchmarks");
        Assert.False(
            Directory.Exists(path),
            $"Repository must not contain '{path}'.");
    }

    [Fact]
    [Trait("Requirement", "ENG-48")]
    public void SchemasDirectory_DoesNotExist()
    {
        var path = Path.Combine(DomainTestPaths.RepoRoot, "schemas");
        Assert.False(
            Directory.Exists(path),
            $"Repository must not contain '{path}'.");
    }

    [Fact]
    public void RepoRoot_ResolvesToTheDirectoryContainingTheSolutionFile() =>
        Assert.True(File.Exists(Path.Combine(DomainTestPaths.RepoRoot, "csharp2md.slnx")));

    private static bool IsPublicOrInternal(Type type) =>
        !type.IsNested || type.IsNestedPublic || type.IsNestedAssembly || type.IsNestedFamORAssem;

    private static IEnumerable<Type> ExposedMemberTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var candidates = type.GetFields(flags).Where(IsExposedMember).Select(field => field.FieldType)
            .Concat(type.GetProperties(flags).Where(IsExposedMember).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Where(IsExposedMember).SelectMany(SignatureTypes))
            .Concat(type.GetConstructors(flags).Where(IsExposedMember).SelectMany(ctor => ctor.GetParameters().Select(p => p.ParameterType)));

        return candidates.SelectMany(Flatten);
    }

    private static IEnumerable<Type> SignatureTypes(MethodInfo method) =>
        [method.ReturnType, .. method.GetParameters().Select(p => p.ParameterType)];

    private static bool IsExposedMember(FieldInfo field) => field.IsPublic || field.IsAssembly || field.IsFamilyOrAssembly;

    private static bool IsExposedMember(MethodBase method) => method.IsPublic || method.IsAssembly || method.IsFamilyOrAssembly;

    private static bool IsExposedMember(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        return accessor is not null && IsExposedMember(accessor);
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        var unwrapped = type;
        while (unwrapped.IsByRef || unwrapped.IsArray || unwrapped.IsPointer)
        {
            unwrapped = unwrapped.GetElementType()!;
        }

        yield return unwrapped;

        if (unwrapped.IsGenericType)
        {
            foreach (var argument in unwrapped.GetGenericArguments())
            {
                foreach (var nested in Flatten(argument))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool BelongsToNamespace(Type type, string forbiddenNamespace) =>
        type.Namespace is not null
        && (type.Namespace == forbiddenNamespace || type.Namespace.StartsWith(forbiddenNamespace + ".", StringComparison.Ordinal));
}
