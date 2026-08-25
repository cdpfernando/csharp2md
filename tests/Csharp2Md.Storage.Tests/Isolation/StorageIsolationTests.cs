using System.Reflection;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Isolation;

public sealed class StorageIsolationTests
{
    private const string DomainNamespace = "Csharp2Md.Domain";

    private static Assembly StorageAssembly => typeof(AssemblyMarker).Assembly;

    [Fact]
    [Trait("Requirement", "ENG-28")]
    public void PublicOrInternalSurface_DoesNotExposeDomainType()
    {
        var offendingType = StorageAssembly.GetTypes()
            .Where(IsPublicOrInternal)
            .SelectMany(ExposedMemberTypes)
            .FirstOrDefault(exposed => BelongsToNamespace(exposed, DomainNamespace));

        Assert.True(
            offendingType is null,
            $"Type '{offendingType?.FullName}' from Csharp2Md.Domain is exposed by a public or internal member of Csharp2Md.Storage.");
    }

    [Fact]
    [Trait("Requirement", "ENG-28")]
    public void ReferencedAssemblies_DoNotIncludeDomain()
    {
        var offendingAssembly = StorageAssembly.GetReferencedAssemblies()
            .FirstOrDefault(reference => reference.Name == "Csharp2Md.Domain");

        Assert.True(
            offendingAssembly?.Name is null,
            $"Assembly '{offendingAssembly?.Name}' is referenced by Csharp2Md.Storage.");
    }

    [Fact]
    [Trait("Requirement", "ENG-28")]
    public void Commit_RoundTripsArbitraryPayloadBytesUnchanged()
    {
        ImmutableArray<byte> taxonomyShapedJson =
        [
            .. "{\"kind\":\"ConfirmedRelation\",\"factType\":\"symbol\",\"taxonomy\":\"executes\"}"u8,
            0x00,
            0xFF,
            0x7B,
            0x7D,
            0x0A,
        ];
        ImmutableArray<byte> manifestBytes = [.. "{\"role\":\"manifest\"}"u8];

        var store = new InMemoryTransactionalStore();
        var session = store.Open("solution-a");
        session.Stage(new StagedFragment(ArtifactRole.Payload, "opaque.bin", taxonomyShapedJson));
        session.Stage(new StagedFragment(ArtifactRole.Manifest, "manifest", manifestBytes));

        var publication = session.Commit();

        Assert.Equal(2, publication.ArtifactsInPublicationOrder.Length);
        AssertUninterpreted(
            publication.ArtifactsInPublicationOrder[0],
            ArtifactRole.Payload,
            "opaque.bin",
            taxonomyShapedJson,
            "Commit must store payload bytes unchanged, including bytes that look like JSON taxonomy.");
        AssertUninterpreted(
            publication.ArtifactsInPublicationOrder[1],
            ArtifactRole.Manifest,
            "manifest",
            manifestBytes,
            "Commit must store manifest bytes unchanged.");

        Assert.True(store.TryGetPublication("solution-a", out var stored));
        Assert.Equal(2, stored.ArtifactsInPublicationOrder.Length);
        AssertUninterpreted(
            stored.ArtifactsInPublicationOrder[0],
            ArtifactRole.Payload,
            "opaque.bin",
            taxonomyShapedJson,
            "Published payload bytes must round-trip unchanged.");
        AssertUninterpreted(
            stored.ArtifactsInPublicationOrder[1],
            ArtifactRole.Manifest,
            "manifest",
            manifestBytes,
            "Published manifest bytes must round-trip unchanged.");
    }

    private static void AssertUninterpreted(
        StagedFragment fragment,
        ArtifactRole role,
        string canonicalKey,
        ImmutableArray<byte> payload,
        string payloadMessage)
    {
        Assert.Equal(role, fragment.Role);
        Assert.Equal(canonicalKey, fragment.CanonicalKey);
        Assert.True(fragment.Payload.AsSpan().SequenceEqual(payload.AsSpan()), payloadMessage);
    }

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
