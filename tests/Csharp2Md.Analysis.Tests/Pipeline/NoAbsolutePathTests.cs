using System.Text;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class NoAbsolutePathTests
{
    [Fact]
    [Trait("Requirement", "ROSE-56")]
    public async Task AnalyzeAsync_CommittedPayloads_ContainNoAbsoluteFilesystemPath()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var cloneRoot = Path.GetFullPath(AnalysisTestPaths.RepoRoot);
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var payloads = publication.ArtifactsInPublicationOrder
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .ToArray();
        Assert.NotEmpty(payloads);

        var diagnostics = Assert.Single(payloads, fragment => fragment.CanonicalKey == "diagnostics.json");
        var envelope = CanonicalJson.Read<DiagnosticsEnvelope>(diagnostics.Payload.AsSpan());
        Assert.All(
            envelope.Records,
            record =>
            {
                if (record.IdentityOrKey is null)
                {
                    return;
                }

                Assert.False(
                    IsAbsoluteFilesystemPath(record.IdentityOrKey),
                    $"IdentityOrKey '{record.IdentityOrKey}' must be a relative path or opaque id.");
            });

        foreach (var fragment in payloads)
        {
            var text = Encoding.UTF8.GetString(fragment.Payload.AsSpan());
            AssertNoClonePath(text, cloneRoot, fragment.CanonicalKey);
            Assert.DoesNotContain("\\\\", text, StringComparison.Ordinal);

            var node = JsonNode.Parse(fragment.Payload.AsSpan());
            Assert.NotNull(node);
            ScanNode(node, fragment.CanonicalKey);
        }
    }

    [Fact]
    [Trait("Requirement", "MSC-07")]
    public async Task AnalyzeAsync_FilesystemCommittedBytes_ContainNoAbsoluteFilesystemPath()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var cloneRoot = Path.GetFullPath(AnalysisTestPaths.RepoRoot);
        var outputPath = Path.Combine(Path.GetTempPath(), "csharp2md-msc07-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputPath);
        try
        {
            var store = new FilesystemTransactionalStore(outputPath);
            var result = await new AnalysisEngine(store).AnalyzeAsync(
                AnalysisRequest.Create([solutionPath]),
                CancellationToken.None);

            Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
            var child = Assert.Single(Directory.GetDirectories(outputPath));
            var files = Directory.EnumerateFiles(child, "*", SearchOption.AllDirectories).ToArray();
            Assert.NotEmpty(files);

            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(child, file).Replace('\\', '/');
                var bytes = File.ReadAllBytes(file);
                var text = Encoding.UTF8.GetString(bytes);
                AssertNoClonePath(text, cloneRoot, relative);
                Assert.DoesNotContain("\\\\", text, StringComparison.Ordinal);
                if (!relative.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }

                var node = JsonNode.Parse(bytes);
                Assert.NotNull(node);
                ScanNode(node, relative);
            }
        }
        finally
        {
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
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
}
