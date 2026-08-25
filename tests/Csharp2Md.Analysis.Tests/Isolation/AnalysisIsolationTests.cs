using System.Reflection;
using System.Xml.Linq;
using Csharp2Md.Analysis;

namespace Csharp2Md.Analysis.Tests.Isolation;

public sealed class AnalysisIsolationTests
{
    private static readonly string[] ForbiddenAnalysisProjectReferences =
    [
        "Csharp2Md.Storage",
        "Csharp2Md.Projection",
        "Csharp2Md.Cli",
    ];

    private static readonly string[] ForbiddenSurfaceNamespaces =
    [
        "Microsoft.CodeAnalysis",
        "Microsoft.Build",
        "System.Text.Json",
    ];

    public static IEnumerable<object[]> ForbiddenAnalysisProjectReferenceCases() =>
        ForbiddenAnalysisProjectReferences.Select(project => new object[] { project });

    public static IEnumerable<object[]> ForbiddenSurfaceNamespaceCases() =>
        ForbiddenSurfaceNamespaces.Select(ns => new object[] { ns });

    [Fact]
    [Trait("Requirement", "STOR-11")]
    public void AnalysisCsproj_DeclaresProjectReferenceToDomain()
    {
        var csprojPath = AnalysisCsprojPath();

        Assert.True(File.Exists(csprojPath), $"Analysis project file was not found at '{csprojPath}'.");

        var domainReference = ReadProjectReferenceIncludes(csprojPath)
            .FirstOrDefault(include => ReferencesProject(include, "Csharp2Md.Domain"));

        Assert.False(
            string.IsNullOrWhiteSpace(domainReference),
            "Csharp2Md.Analysis must declare a project reference to Csharp2Md.Domain.");
    }

    [Theory]
    [Trait("Requirement", "ENG-03")]
    [MemberData(nameof(ForbiddenAnalysisProjectReferenceCases))]
    public void AnalysisCsproj_DeclaresNoProjectReferenceTo(string forbiddenProject)
    {
        var csprojPath = AnalysisCsprojPath();

        Assert.True(File.Exists(csprojPath), $"Analysis project file was not found at '{csprojPath}'.");

        var offending = ReadProjectReferenceIncludes(csprojPath)
            .FirstOrDefault(include => ReferencesProject(include, forbiddenProject));

        Assert.True(
            offending is null,
            $"Csharp2Md.Analysis must not declare a project reference to {forbiddenProject}, but found '{offending}'.");
    }

    [Fact]
    [Trait("Requirement", "ROSE-27")]
    public void AnalysisCsproj_DeclaresNoMicrosoftBuildPackageReference()
    {
        var csprojPath = AnalysisCsprojPath();

        Assert.True(File.Exists(csprojPath), $"Analysis project file was not found at '{csprojPath}'.");

        var offending = ReadPackageReferenceIncludes(csprojPath)
            .FirstOrDefault(packageId => packageId.StartsWith("Microsoft.Build", StringComparison.Ordinal));

        Assert.True(
            offending is null,
            $"Csharp2Md.Analysis must not declare a PackageReference to '{offending}' (forbidden prefix 'Microsoft.Build').");
    }

    [Fact]
    [Trait("Requirement", "ENG-05")]
    public void DomainProject_DeclaresNoPackageReferenceAndNoProjectReference()
    {
        var content = File.ReadAllText(
            Path.Combine(AnalysisTestPaths.RepoRoot, "src", "Csharp2Md.Domain", "Csharp2Md.Domain.csproj"));

        Assert.DoesNotContain("<PackageReference", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProjectReference", content, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Requirement", "ENG-07")]
    [MemberData(nameof(ForbiddenSurfaceNamespaceCases))]
    public void PublicSurface_DoesNotExposeForbiddenNamespace(string forbiddenNamespace)
    {
        var offendingType = typeof(AssemblyMarker).Assembly
            .GetExportedTypes()
            .SelectMany(ExposedPublicMemberTypes)
            .FirstOrDefault(exposed => BelongsToNamespace(exposed, forbiddenNamespace));

        Assert.True(
            offendingType is null,
            $"Type '{offendingType?.FullName}' from forbidden namespace '{forbiddenNamespace}' is exposed by a public member of Csharp2Md.Analysis.");
    }

    private static string AnalysisCsprojPath() =>
        Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Csharp2Md.Analysis.csproj");

    private static IReadOnlyList<string> ReadProjectReferenceIncludes(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> ReadPackageReferenceIncludes(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();
    }

    private static bool ReferencesProject(string include, string projectName)
    {
        var normalized = include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return string.Equals(Path.GetFileNameWithoutExtension(normalized), projectName, StringComparison.Ordinal);
    }

    private static IEnumerable<Type> ExposedPublicMemberTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var candidates = type.GetFields(flags).Where(field => field.IsPublic).Select(field => field.FieldType)
            .Concat(type.GetProperties(flags).Where(IsPublicProperty).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Where(method => method.IsPublic).SelectMany(SignatureTypes))
            .Concat(type.GetConstructors(flags).Where(ctor => ctor.IsPublic).SelectMany(ctor => ctor.GetParameters().Select(p => p.ParameterType)));

        return candidates.SelectMany(Flatten);
    }

    private static IEnumerable<Type> SignatureTypes(MethodInfo method) =>
        [method.ReturnType, .. method.GetParameters().Select(p => p.ParameterType)];

    private static bool IsPublicProperty(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        return accessor is not null && accessor.IsPublic;
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
