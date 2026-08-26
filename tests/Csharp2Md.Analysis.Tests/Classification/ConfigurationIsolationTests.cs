using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Configuration;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Classification.Persistence;
using Csharp2Md.Analysis.Classification.Topology;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ConfigurationIsolationTests
{
    private const string AppSettingsPassword = "appsettings-fixture-secret";
    private const string InlinePassword = "inline-fixture-secret";

    private static readonly string[] SecretValues =
    [
        AppSettingsPassword,
        InlinePassword,
        "Server=orders-db.internal.acme.local;Database=Orders;User Id=orders_app;Password=appsettings-fixture-secret;",
        "Server=orders-db.internal.acme.local;Database=Orders;User Id=orders_app;Password=inline-fixture-secret;",
    ];

    [Fact]
    [Trait("Requirement", "CDC-52")]
    public async Task AnalyzeAsync_CommittedArtifacts_OmitFixturePasswords()
    {
        var payloads = await CanonicalPayloadTextsAsync();
        Assert.NotEmpty(payloads);
        Assert.All(
            payloads,
            payload =>
            {
                foreach (var secret in SecretValues)
                {
                    Assert.DoesNotContain(secret, payload.Text, StringComparison.Ordinal);
                }
            });
    }

    [Fact]
    [Trait("Requirement", "CDC-52")]
    public async Task AnalyzeAsync_SuspectedSecretEvidence_CarriesRedactedExcerptForTheFixturePassword()
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);
            await new ClassificationAndPromotionStage(DefaultPasses())
                .ExecuteAsync(context, CancellationToken.None);

            var secrets = context.Accumulator.ToSnapshot().SuspectedSecrets;
            Assert.NotEmpty(secrets);
            Assert.Contains(
                secrets,
                evidence =>
                    evidence.Excerpt.Value.Contains("***", StringComparison.Ordinal)
                    || evidence.Excerpt.Value.Contains("[REDACTED]", StringComparison.Ordinal));
            Assert.All(
                secrets,
                evidence =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(evidence.Document.Value));
                    Assert.False(Path.IsPathRooted(evidence.Document.Value));
                    Assert.True(evidence.Span.StartLine > 0 && evidence.Span.StartColumn > 0);
                    Assert.DoesNotContain(AppSettingsPassword, evidence.Excerpt.Value, StringComparison.Ordinal);
                    Assert.DoesNotContain(InlinePassword, evidence.Excerpt.Value, StringComparison.Ordinal);
                });
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-50")]
    public async Task AnalyzeAsync_EmittedRecords_ContainNoAbsolutePathOrDrivePrefix()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var cloneRoot = Path.GetFullPath(AnalysisTestPaths.RepoRoot);
        var publication = await PublishAsync(solutionPath);

        var payloads = publication.ArtifactsInPublicationOrder
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .ToArray();
        Assert.NotEmpty(payloads);

        foreach (var fragment in payloads)
        {
            var text = Encoding.UTF8.GetString(fragment.Payload.AsSpan());
            AssertNoClonePath(text, cloneRoot, fragment.CanonicalKey);

            var node = JsonNode.Parse(fragment.Payload.AsSpan());
            Assert.NotNull(node);
            ScanNode(node, fragment.CanonicalKey);
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-08")]
    public void ClassificationTypes_DoNotReferenceMicrosoftCodeAnalysis()
    {
        var exported = typeof(AssemblyMarker).Assembly.GetExportedTypes();
        var exportedNames = exported.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("ConfigurationPass", exportedNames);
        Assert.DoesNotContain("ConfigurationEmitter", exportedNames);
        Assert.DoesNotContain("ConfigurationModelBuilder", exportedNames);
        Assert.DoesNotContain("TopologyEmitter", exportedNames);
        Assert.DoesNotContain("TopologyModelBuilder", exportedNames);

        var classificationTypes = typeof(IClassifierPass).Assembly.GetTypes()
            .Where(type => type.Namespace is not null
                && type.Namespace.StartsWith("Csharp2Md.Analysis.Classification", StringComparison.Ordinal))
            .Where(type => !type.Name.Contains('<', StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(classificationTypes, type => type == typeof(ConfigurationPass));
        Assert.Contains(classificationTypes, type => type == typeof(ConfigurationEmitter));
        Assert.Contains(classificationTypes, type => type == typeof(TopologyEmitter));
        Assert.Contains(classificationTypes, type => type == typeof(PersistenceEmitter));

        var offending = classificationTypes
            .SelectMany(ExposedMemberTypes)
            .SelectMany(Flatten)
            .FirstOrDefault(exposed => BelongsToNamespace(exposed, "Microsoft.CodeAnalysis"));
        Assert.True(
            offending is null,
            $"Type '{offending?.FullName}' from Microsoft.CodeAnalysis is exposed through the classification surface.");
    }

    [Fact]
    [Trait("Requirement", "CDC-53")]
    public async Task AnalyzeAsync_PublishedTaxonomyRegistry_IsByteIdenticalToTheCommittedFile()
    {
        var committedPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "contracts",
            "taxonomy-registry.json");
        Assert.True(File.Exists(committedPath), $"Expected committed registry at '{committedPath}'.");
        var committed = File.ReadAllBytes(committedPath);

        var publication = await PublishAsync(AcmeOrdersSolutionPath());
        var published = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "contracts/taxonomy-registry.json");
        Assert.True(
            committed.AsSpan().SequenceEqual(published.Payload.AsSpan()),
            "The published taxonomy-registry.json is not byte-identical to contracts/taxonomy-registry.json.");
    }

    [Fact]
    [Trait("Requirement", "CDC-54")]
    public void AnalysisCsproj_DeclaresNoProjectReferenceToStorageProjectionOrCli()
    {
        var references = ProjectReferenceNames(Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Csharp2Md.Analysis.csproj"));
        Assert.DoesNotContain("Csharp2Md.Storage", references);
        Assert.DoesNotContain("Csharp2Md.Projection", references);
        Assert.DoesNotContain("Csharp2Md.Cli", references);
        Assert.Contains("Csharp2Md.Domain", references);
    }

    [Fact]
    [Trait("Requirement", "CDC-54")]
    public void CliCsproj_DeclaresNoProjectReferenceToDomain()
    {
        var references = ProjectReferenceNames(Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Cli",
            "Csharp2Md.Cli.csproj"));
        Assert.DoesNotContain("Csharp2Md.Domain", references);
        Assert.Contains("Csharp2Md.Analysis", references);
    }

    private static ImmutableArray<IClassifierPass> DefaultPasses() =>
    [
        new ComponentPass(),
        new EntryPointPass(),
        new BoundaryPass(),
        new ContractPass(),
        new PersistencePass(),
        new ConfigurationPass(),
        new RelationPass(),
        new InvokesPass(),
        new ExecutesPass(),
    ];

    private static async Task<IReadOnlyList<(string Key, string Text)>> CanonicalPayloadTextsAsync()
    {
        var publication = await PublishAsync(AcmeOrdersSolutionPath());
        return publication.ArtifactsInPublicationOrder
            .Where(static artifact => artifact.Role == ArtifactRole.Payload)
            .Select(artifact => (artifact.CanonicalKey, Text: Encoding.UTF8.GetString(artifact.Payload.ToArray())))
            .ToArray();
    }

    private static async Task<CommittedPublication> PublishAsync(string solutionPath)
    {
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return publication;
    }

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static string[] ProjectReferenceNames(string csprojPath)
    {
        Assert.True(File.Exists(csprojPath), $"Project file was not found at '{csprojPath}'.");
        return XDocument.Load(csprojPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(
                include!.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            .ToArray();
    }

    private static void AssertNoClonePath(string text, string cloneRoot, string canonicalKey)
    {
        var slash = cloneRoot.Replace('\\', '/');
        Assert.False(
            text.Contains(cloneRoot, StringComparison.OrdinalIgnoreCase)
            || text.Contains(slash, StringComparison.OrdinalIgnoreCase),
            $"Canonical payload '{canonicalKey}' contains the clone path.");
    }

    private static void ScanNode(JsonNode? node, string canonicalKey)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                Assert.False(
                    IsAbsoluteFilesystemPath(text),
                    $"Canonical payload '{canonicalKey}' contains rooted path '{text}'.");
                Assert.DoesNotContain('\\', text);
                Assert.False(HasDrivePrefix(text), $"Canonical payload '{canonicalKey}' contains drive prefix '{text}'.");
                break;
            case JsonObject obj:
                foreach (var property in obj)
                {
                    ScanNode(property.Value, canonicalKey);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    ScanNode(item, canonicalKey);
                }

                break;
        }
    }

    private static bool IsAbsoluteFilesystemPath(string value) =>
        Path.IsPathRooted(value)
        || HasDrivePrefix(value)
        || value.StartsWith("\\\\", StringComparison.Ordinal)
        || value.StartsWith("//", StringComparison.Ordinal);

    private static bool HasDrivePrefix(string value) =>
        value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':';

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
}
