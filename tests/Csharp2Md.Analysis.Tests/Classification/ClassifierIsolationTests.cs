using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ClassifierIsolationTests
{
    private static readonly string[] SnapshotRoots = ["src", "tests", "contracts", "fixtures"];
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "TestResults",
    };

    private static readonly string[] SecretValues =
    [
        "appsettings-fixture-secret",
        "inline-fixture-secret",
        "Password=secret",
    ];

    private static readonly string[] ClassifierPayloadKeys =
    [
        "facts/architecture.json",
        "facts/contract.json",
        "relations/confirmed/implements-operation.json",
        "relations/confirmed/uses-contract.json",
        "relations/candidates.json",
        "relations/unresolved.json",
        "diagnostics.json",
    ];

    [Fact]
    [Trait("Requirement", "EBC-42")]
    public void PublicSurface_DoesNotExposeClassifierTypesOrMicrosoftCodeAnalysisThroughThem()
    {
        var exported = typeof(AssemblyMarker).Assembly.GetExportedTypes();
        var exportedNames = exported.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("IClassifierPass", exportedNames);
        Assert.DoesNotContain("ClassifierContext", exportedNames);
        Assert.DoesNotContain("ClassifierPassResult", exportedNames);
        Assert.DoesNotContain("ClassificationAndPromotionStage", exportedNames);
        Assert.DoesNotContain("ComponentPass", exportedNames);
        Assert.DoesNotContain("EntryPointPass", exportedNames);
        Assert.DoesNotContain("BoundaryPass", exportedNames);
        Assert.DoesNotContain("ContractPass", exportedNames);
        Assert.DoesNotContain("RelationPass", exportedNames);
        Assert.DoesNotContain("InvokesPass", exportedNames);
        Assert.DoesNotContain("ExecutesPass", exportedNames);

        var classifierTypes = typeof(IClassifierPass).Assembly.GetTypes()
            .Where(type => type.Namespace is "Csharp2Md.Analysis.Classification"
                or "Csharp2Md.Analysis.Classification.Passes")
            .Where(type => !type.Name.Contains('<', StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(classifierTypes);

        var offending = classifierTypes
            .SelectMany(ExposedMemberTypes)
            .SelectMany(Flatten)
            .FirstOrDefault(exposed => BelongsToNamespace(exposed, "Microsoft.CodeAnalysis"));
        Assert.True(
            offending is null,
            $"Type '{offending?.FullName}' from Microsoft.CodeAnalysis is exposed through a classifier member.");
    }

    [Fact]
    [Trait("Requirement", "EBC-43")]
    public void CliCsproj_DeclaresNoProjectReferenceToDomain()
    {
        var csprojPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Cli",
            "Csharp2Md.Cli.csproj");
        Assert.True(File.Exists(csprojPath), $"CLI project file was not found at '{csprojPath}'.");

        var references = XDocument.Load(csprojPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(
                include!.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            .ToArray();

        Assert.DoesNotContain("Csharp2Md.Domain", references);
        Assert.Contains("Csharp2Md.Analysis", references);
    }

    [Fact]
    [Trait("Requirement", "EBC-44")]
    public async Task AnalyzeAsync_InMemoryAdapter_ProducesClassifierFactsAndRelationsWithoutFilesystemWrites()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var before = FileSetHash();
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        var after = FileSetHash();

        Assert.Equal(before, after);
        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(outcome.Stages[3].FactCount > 0, $"Classification fact count was {outcome.Stages[3].FactCount}.");
        Assert.True(outcome.Stages[3].RelationCount > 0, $"Classification relation count was {outcome.Stages[3].RelationCount}.");
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var architecture = ShardedFactsReader.Read<ArchitectureFactsShard>(
            publication.ArtifactsInPublicationOrder, "facts/architecture.json");
        Assert.NotEmpty(architecture.Components);
        Assert.NotEmpty(architecture.EntryPoints);
        Assert.Contains(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/implements-operation.json");
        Assert.Contains(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/uses-contract.json");
    }

    [Fact]
    [Trait("Requirement", "EBC-45")]
    public async Task AnalyzeAsync_ClassifierPayloads_ContainNoSecretValues()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var payloads = publication.ArtifactsInPublicationOrder
            .Where(artifact => ClassifierPayloadKeys.Any(baseKey => ShardedFactsReader.IsFamilyMember(artifact.CanonicalKey, baseKey)))
            .Select(artifact => (artifact.CanonicalKey, Text: Encoding.UTF8.GetString(artifact.Payload.ToArray())))
            .ToArray();
        Assert.NotEmpty(payloads);
        Assert.All(
            payloads,
            payload =>
            {
                foreach (var token in SecretValues)
                {
                    Assert.DoesNotContain(token, payload.Text, StringComparison.OrdinalIgnoreCase);
                }
            });
    }

    private static IEnumerable<Type> ExposedMemberTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return type.GetFields(flags).Select(field => field.FieldType)
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).SelectMany(SignatureTypes))
            .Concat(type.GetConstructors(flags).SelectMany(ctor => ctor.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    private static IEnumerable<Type> SignatureTypes(MethodInfo method) =>
        method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType);

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

    private static bool BelongsToNamespace(Type type, string prefix) =>
        type.Namespace is not null
        && (type.Namespace == prefix || type.Namespace.StartsWith(prefix + ".", StringComparison.Ordinal));

    private static string FileSetHash()
    {
        var repo = AnalysisTestPaths.RepoRoot;
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var relative in EnumerateSnapshotFiles(repo))
        {
            incremental.AppendData(Encoding.UTF8.GetBytes(relative));
            incremental.AppendData([0]);
            incremental.AppendData(File.ReadAllBytes(Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar))));
            incremental.AppendData([0]);
        }

        return Convert.ToHexString(incremental.GetCurrentHash());
    }

    private static IReadOnlyList<string> EnumerateSnapshotFiles(string repo)
    {
        var files = new List<string>();
        foreach (var root in SnapshotRoots)
        {
            var fullRoot = Path.Combine(repo, root);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repo, file);
                var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (segments.Any(ExcludedSegments.Contains))
                {
                    continue;
                }

                files.Add(string.Join('/', segments));
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }
}
