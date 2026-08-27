using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Projection;

public sealed class CitationResolutionTests : IClassFixture<CitationResolutionTests.PublishedFixture>
{
    private readonly PublishedFixture _fixture;

    public CitationResolutionTests(PublishedFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Requirement", "RP-20")]
    [Trait("Requirement", "RP-27")]
    public void Analyze_Fixture_PublishesCatalogsAndPostings()
    {
        Assert.Equal(PublicationStatus.Committed, _fixture.Status);
        Assert.Contains(_fixture.Files.Keys, key => key.StartsWith("catalogs/", StringComparison.Ordinal));
        Assert.Contains(_fixture.Files.Keys, key => key.StartsWith("postings/", StringComparison.Ordinal));
        Assert.True(_fixture.Files.ContainsKey("relations/confirmed/invokes.json"));
        Assert.True(_fixture.Files.ContainsKey("postings/callers.json"));
    }

    [Fact]
    [Trait("Requirement", "RP-49")]
    public void Analyze_Fixture_ProjectionArtifactsContainNoAbsolutePath()
    {
        var projections = _fixture.Files
            .Where(static pair => IsProjection(pair.Key))
            .ToArray();
        Assert.NotEmpty(projections);
        foreach (var (key, bytes) in projections)
        {
            Assert.False(Path.IsPathRooted(key), key);
            Assert.DoesNotContain(":\\", key, StringComparison.Ordinal);
            Assert.False(key.StartsWith("/", StringComparison.Ordinal), key);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain(":\\", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/home/", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/opt/", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/Users/", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-20")]
    public void Analyze_Fixture_EveryCatalogCitationResolvesToClaimedFact()
    {
        var catalogs = _fixture.Files
            .Where(pair => pair.Key.StartsWith("catalogs/", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(catalogs);

        foreach (var (key, bytes) in catalogs)
        {
            var entries = CanonicalJson.Read<ImmutableArray<CatalogEntryDto>>(bytes);
            Assert.NotEmpty(entries);
            foreach (var entry in entries)
            {
                var cited = ElementAt(entry.ArtifactKey, entry.Ordinal);
                Assert.Equal(entry.FactId, ClaimedId(cited));
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RP-27")]
    public void Analyze_Fixture_EveryPostingCitationResolvesToClaimedRelation()
    {
        var postings = _fixture.Files
            .Where(pair => pair.Key.StartsWith("postings/", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(postings);

        foreach (var (key, bytes) in postings)
        {
            var groups = CanonicalJson.Read<ImmutableArray<PostingGroupDto>>(bytes);
            Assert.NotEmpty(groups);
            foreach (var group in groups)
            {
                Assert.NotEmpty(group.Entries);
                foreach (var entry in group.Entries)
                {
                    var cited = ElementAt(entry.ArtifactKey, entry.Ordinal);
                    if (entry.ArtifactKey.StartsWith("relations/confirmed/", StringComparison.Ordinal))
                    {
                        Assert.True(
                            string.Equals(group.FactId, Text(cited, "source", "id"), StringComparison.Ordinal)
                            || string.Equals(group.FactId, Text(cited, "target", "id"), StringComparison.Ordinal),
                            $"{key} group '{group.FactId}' does not match cited relation endpoints.");
                    }
                    else
                    {
                        Assert.Equal(group.FactId, ClaimedId(cited));
                    }
                }
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RP-30")]
    public void Analyze_Fixture_PostingGroupsAreOrderedBySubjectFactId()
    {
        foreach (var (key, bytes) in PostingFiles())
        {
            var groups = CanonicalJson.Read<ImmutableArray<PostingGroupDto>>(bytes);
            var factIds = groups.Select(static group => group.FactId).ToArray();
            Assert.Equal(factIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(), factIds);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-30")]
    public void Analyze_Fixture_PostingEntriesAreOrderedByArtifactKeyThenOrdinal()
    {
        foreach (var (_, bytes) in PostingFiles())
        {
            foreach (var group in CanonicalJson.Read<ImmutableArray<PostingGroupDto>>(bytes))
            {
                var actual = group.Entries.Select(static entry => (entry.ArtifactKey, entry.Ordinal)).ToArray();
                var expected = actual
                    .OrderBy(static entry => entry.ArtifactKey, StringComparer.Ordinal)
                    .ThenBy(static entry => entry.Ordinal)
                    .ToArray();
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RP-27")]
    [Trait("Requirement", "RP-28")]
    [Trait("Requirement", "RP-29")]
    public void Analyze_Fixture_CallersPostingMatchesConfirmedInvokesAndExcludesCandidates()
    {
        var invokes = CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(
            _fixture.Files["relations/confirmed/invokes.json"]);
        var callers = CanonicalJson.Read<ImmutableArray<PostingGroupDto>>(
            _fixture.Files["postings/callers.json"]);
        var callee = Assert.Single(
            invokes
                .Where(static relation =>
                    relation.Target.Id.Contains("PaymentClient", StringComparison.Ordinal)
                    && relation.Target.Id.Contains("Authorize", StringComparison.Ordinal)
                    && !relation.Target.Id.Contains("AuthorizeViaPaymentClientAsync", StringComparison.Ordinal))
                .Select(static relation => relation.Target.Id)
                .Distinct(StringComparer.Ordinal));
        var expected = invokes
            .Where(relation => relation.Target.Id == callee)
            .Select(static relation => relation.Source.Id)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        var group = Assert.Single(callers, candidate => candidate.FactId == callee);
        var actual = group.Entries
            .Select(entry =>
            {
                var relation = CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(
                    _fixture.Files[entry.ArtifactKey])[entry.Ordinal];
                Assert.Equal("invokes", relation.Kind);
                Assert.Equal(callee, relation.Target.Id);
                return relation.Source.Id;
            })
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);
        if (_fixture.Files.TryGetValue("relations/candidates.json", out var candidateBytes))
        {
            var candidates = CanonicalJson.Read<ImmutableArray<CandidateLinkDto>>(candidateBytes);
            var candidateSources = candidates
                .Where(static link => link.Kind == "invokes")
                .Select(static link => link.Source.Id)
                .ToHashSet(StringComparer.Ordinal);
            Assert.DoesNotContain(actual, id => candidateSources.Contains(id));
        }
    }

    private IEnumerable<KeyValuePair<string, byte[]>> PostingFiles()
    {
        var files = _fixture.Files
            .Where(pair => pair.Key.StartsWith("postings/", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(files);
        return files;
    }

    private static bool IsProjection(string key) =>
        key.StartsWith("catalogs/", StringComparison.Ordinal)
        || key.StartsWith("postings/", StringComparison.Ordinal)
        || key.StartsWith("markdown/", StringComparison.Ordinal)
        || key.StartsWith("source/", StringComparison.Ordinal)
        || key is "retrieval.md" or "AGENTS.md";

    private JsonNode ElementAt(string artifactKey, int ordinal)
    {
        Assert.True(_fixture.Files.TryGetValue(artifactKey, out var bytes), $"Missing cited artifact '{artifactKey}'.");
        var node = JsonNode.Parse(bytes);
        Assert.NotNull(node);
        switch (node)
        {
            case JsonArray array:
                Assert.InRange(ordinal, 0, array.Count - 1);
                return array[ordinal]!;
            case JsonObject obj:
                var items = new List<JsonNode?>();
                foreach (var property in obj)
                {
                    if (property.Value is JsonArray family)
                    {
                        items.AddRange(family);
                    }
                }

                Assert.InRange(ordinal, 0, items.Count - 1);
                return items[ordinal]!;
            default:
                Assert.Equal(0, ordinal);
                return node;
        }
    }

    private static string ClaimedId(JsonNode node)
    {
        var identity = Text(node, "identity", "id");
        if (identity.Length > 0)
        {
            return identity;
        }

        var source = Text(node, "source", "id");
        if (source.Length > 0)
        {
            return source;
        }

        return Text(node, "occurrence", "owner", "id");
    }

    private static string Text(JsonNode node, params string[] path)
    {
        JsonNode? current = node;
        foreach (var segment in path)
        {
            current = current is JsonObject obj && obj.TryGetPropertyValue(segment, out var next)
                ? next
                : null;
            if (current is null)
            {
                return string.Empty;
            }
        }

        return current is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
    }

    public sealed class PublishedFixture : IDisposable
    {
        public PublicationStatus Status { get; }

        public IReadOnlyDictionary<string, byte[]> Files { get; }

        public PublishedFixture()
        {
            var solutionPath = Path.Combine(
                AnalysisTestPaths.RepoRoot,
                "fixtures",
                "SyntheticSolution",
                "Acme.Orders",
                "Acme.Orders.slnx");
            Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

            DirectoryPath = Path.Combine(Path.GetTempPath(), "csharp2md-citation-resolution-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            var store = new FilesystemTransactionalStore(DirectoryPath, new PackageProjector());
            var result = new AnalysisEngine(store)
                .AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Status = Assert.Single(result.Solutions).Status;
            var canonical = Path.GetFullPath(solutionPath);
            var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..32];
            var child = Path.Combine(DirectoryPath, "s-" + hex);
            Files = Directory.Exists(child)
                ? Directory.EnumerateFiles(child, "*", SearchOption.AllDirectories)
                    .ToDictionary(
                        path => Path.GetRelativePath(child, path).Replace('\\', '/'),
                        File.ReadAllBytes,
                        StringComparer.Ordinal)
                : new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        private string DirectoryPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch (IOException exception)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete '{DirectoryPath}': {exception.Message}");
            }
        }
    }
}
