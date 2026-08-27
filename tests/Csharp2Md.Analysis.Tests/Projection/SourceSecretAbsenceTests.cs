using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Projection;

public sealed class SourceSecretAbsenceTests : IClassFixture<SourceSecretAbsenceTests.PublishedFixture>
{
    private const string ConfigurationSecret = "appsettings-fixture-secret";
    private const string Marker = "[REDACTED-SECRET]";

    private readonly PublishedFixture _fixture;

    public SourceSecretAbsenceTests(PublishedFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Requirement", "RP-12")]
    public void Analyze_Fixture_CommitsAPublishedPackage()
    {
        Assert.Equal(PublicationStatus.Committed, _fixture.Status);
        Assert.NotEmpty(_fixture.Files);
    }

    [Fact]
    [Trait("Requirement", "RP-12")]
    public void PublishedFragments_DoNotContainTheConfigurationSecretBytes()
    {
        var secret = Encoding.UTF8.GetBytes(ConfigurationSecret);
        foreach (var (key, bytes) in _fixture.Files)
        {
            Assert.False(
                Contains(bytes, secret),
                $"Published artifact '{key}' still contains the configuration secret.");
        }
    }

    [Fact]
    [Trait("Requirement", "RP-12")]
    public void PublishedFragments_DoNotContainAHashOfTheIndividualSecret()
    {
        var secretHash = Encoding.UTF8.GetBytes(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ConfigurationSecret))));
        foreach (var (key, bytes) in _fixture.Files)
        {
            Assert.False(
                Contains(bytes, secretHash),
                $"Published artifact '{key}' contains a hash of the configuration secret.");
        }
    }

    [Fact]
    [Trait("Requirement", "RP-12")]
    public void PublishedSource_MarkerOccupiesTheSecretSpan()
    {
        var source = Assert.Single(
            _fixture.Files,
            pair => pair.Key.StartsWith("source/", StringComparison.Ordinal)
                && pair.Key.EndsWith("appsettings.json", StringComparison.Ordinal)
                && !pair.Key.EndsWith(".meta.json", StringComparison.Ordinal)
                && !pair.Key.Contains("Development", StringComparison.Ordinal));
        var marker = Encoding.ASCII.GetBytes(Marker);

        Assert.True(Contains(source.Value, marker), "Published source does not contain the redaction marker.");
        Assert.False(Contains(source.Value, Encoding.UTF8.GetBytes(ConfigurationSecret)));
        Assert.True(
            marker.AsSpan().SequenceEqual(source.Value) || Contains(source.Value, marker),
            "The marker must occupy the suspected-secret span in the published source artifact.");
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        var span = haystack.AsSpan();
        for (var i = 0; i <= span.Length - needle.Length; i++)
        {
            if (span.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
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

            DirectoryPath = Path.Combine(Path.GetTempPath(), "csharp2md-secret-absence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            var store = new FilesystemTransactionalStore(DirectoryPath, new PackageProjector());
            var result = new AnalysisEngine(store)
                .AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Status = Assert.Single(result.Solutions).Status;
            var canonical = Path.GetFullPath(solutionPath);
            var identity = SolutionCoordinate.For(canonical).Identity.Value;
            var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
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
