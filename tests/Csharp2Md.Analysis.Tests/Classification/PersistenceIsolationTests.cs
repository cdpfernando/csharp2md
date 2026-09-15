using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Classification.Persistence;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class PersistenceIsolationTests
{
    private const string InlineConnectionString =
        "Server=orders-db.internal.acme.local;Database=Orders;User Id=orders_app;Password=inline-fixture-secret;";

    private const string AppSettingsConnectionString =
        "Server=orders-db.internal.acme.local;Database=Orders;User Id=orders_app;Password=appsettings-fixture-secret;";

    private static readonly string[] SnapshotRoots = ["src", "tests", "contracts", "fixtures"];
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "TestResults",
    };

    [Fact]
    [Trait("Requirement", "PK-48")]
    public async Task AnalyzeAsync_CanonicalPayloads_OmitTheInlineConnectionStringValue()
    {
        var payloads = await CanonicalPayloadTextsAsync();
        Assert.NotEmpty(payloads);
        Assert.All(
            payloads,
            payload => Assert.DoesNotContain(InlineConnectionString, payload.Text, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "PK-48")]
    public async Task AnalyzeAsync_CanonicalPayloads_OmitTheAppSettingsOrdersDbValue()
    {
        var payloads = await CanonicalPayloadTextsAsync();
        Assert.NotEmpty(payloads);
        Assert.All(
            payloads,
            payload => Assert.DoesNotContain(AppSettingsConnectionString, payload.Text, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "PK-48")]
    public async Task AnalyzeAsync_SuspectedSecrets_CarryDocumentSpanHashAndRedactionOnly()
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);
            await new ClassificationAndPromotionStage([new PersistencePass()])
                .ExecuteAsync(context, CancellationToken.None);

            var secrets = context.Accumulator.ToSnapshot().SuspectedSecrets;
            Assert.NotEmpty(secrets);
            Assert.All(
                secrets,
                evidence =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(evidence.Document.Value));
                    Assert.False(Path.IsPathRooted(evidence.Document.Value));
                    Assert.True(evidence.Span.StartLine > 0 && evidence.Span.StartColumn > 0);
                    Assert.Equal(DocumentHash.Length, evidence.Hash.Value.Length);
                    Assert.True(
                        evidence.Excerpt.Value.Contains("***", StringComparison.Ordinal)
                        || evidence.Excerpt.Value.Contains("[REDACTED]", StringComparison.Ordinal),
                        "A suspected secret must carry a visible redaction marker.");
                    Assert.DoesNotContain(InlineConnectionString, evidence.Excerpt.Value, StringComparison.Ordinal);
                    Assert.DoesNotContain(AppSettingsConnectionString, evidence.Excerpt.Value, StringComparison.Ordinal);
                });
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "PK-49")]
    public void PersistenceClassifierSurface_DoesNotExposeMicrosoftCodeAnalysis()
    {
        var exported = typeof(AssemblyMarker).Assembly.GetExportedTypes();
        var exportedNames = exported.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("PersistencePass", exportedNames);
        Assert.DoesNotContain("PersistenceEmitter", exportedNames);
        Assert.DoesNotContain("PersistenceModelBuilder", exportedNames);

        var persistenceTypes = typeof(IClassifierPass).Assembly.GetTypes()
            .Where(type => type.Namespace is "Csharp2Md.Analysis.Classification"
                or "Csharp2Md.Analysis.Classification.Passes"
                or "Csharp2Md.Analysis.Classification.Persistence")
            .Where(type => !type.Name.Contains('<', StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(persistenceTypes, type => type == typeof(PersistencePass));
        Assert.Contains(persistenceTypes, type => type == typeof(PersistenceEmitter));

        var offending = persistenceTypes
            .SelectMany(ExposedMemberTypes)
            .SelectMany(Flatten)
            .FirstOrDefault(exposed => BelongsToNamespace(exposed, "Microsoft.CodeAnalysis"));
        Assert.True(
            offending is null,
            $"Type '{offending?.FullName}' from Microsoft.CodeAnalysis is exposed through the persistence classifier surface.");
    }

    [Fact]
    [Trait("Requirement", "PK-49")]
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
    [Trait("Requirement", "PK-50")]
    public async Task AnalyzeAsync_InMemoryAdapter_ProducesPersistenceFactsAndWritesNoFiles()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var before = FileSetHash();
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        var after = FileSetHash();

        Assert.Equal(before, after);
        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var persistence = CanonicalJson.Read<PersistenceFactsShard>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "facts/persistence.json").Payload.AsSpan());
        Assert.NotEmpty(persistence.DataStores);
        Assert.NotEmpty(persistence.DataObjects);
        // T52 made the derived ~32 KiB ceiling the live default: this flat record-array family may now
        // legitimately be sharded into "accesses-data.<bucket>.json" instead of staying at its base key.
        Assert.Contains(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/accesses-data.json"
                || artifact.CanonicalKey.StartsWith("relations/confirmed/accesses-data.", StringComparison.Ordinal));
    }

    private static async Task<IReadOnlyList<(string Key, string Text)>> CanonicalPayloadTextsAsync()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        return publication.ArtifactsInPublicationOrder
            .Where(static artifact => artifact.Role == ArtifactRole.Payload)
            .Select(artifact => (artifact.CanonicalKey, Text: Encoding.UTF8.GetString(artifact.Payload.ToArray())))
            .ToArray();
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
