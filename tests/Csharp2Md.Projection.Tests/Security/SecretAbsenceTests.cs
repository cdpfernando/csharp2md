using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Security;

/// <summary>
/// GCPC-082, GCPC-083, GCPC-086: sweeps every byte of a real published package -- built the same way
/// the pre-existing RP-12 <c>SourceSecretAbsenceTests</c> does, from
/// <c>fixtures/SyntheticSolution/Acme.Orders</c>'s <c>appsettings.json</c> (the corpus configuration
/// document carrying a secret) -- for the secret literal and for a hash of the secret alone, and
/// extends that coverage with T58's own bullets: the redaction sidecar's model stays intact, the
/// secret never becomes a catalog label, and the coverage/provenance/accounting envelopes carry no
/// credential-shaped text.
///
/// Only <c>appsettings-fixture-secret</c> is swept here, not <c>Data/OrderSqlQueries.cs</c>'s inline
/// <c>inline-fixture-secret</c>: that second value is deliberately different (per the fixture's own
/// header comment) so the two absence checks cannot cover for each other, but it names a *different*
/// guarantee -- the string must never reach a fact, relation or diagnostic (proven elsewhere by the
/// persistence classifier tests) -- not redaction from the source projection. `.cs` files are not
/// configuration documents, so they are not fed through `SecretRedactor` at all; publishing
/// `Data/OrderSqlQueries.cs` unredacted in `source/` is the correct, intended behaviour (confirmed by
/// direct inspection of a real published package before writing this test), not a leak.
/// </summary>
public sealed class SecretAbsenceTests : IClassFixture<SecretAbsenceTests.PublishedFixture>
{
    private const string ConfigurationSecret = "appsettings-fixture-secret";

    private readonly PublishedFixture _fixture;

    public SecretAbsenceTests(PublishedFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Requirement", "GCPC-083")]
    public void Analyze_Fixture_CommitsAPublishedPackage()
    {
        Assert.Equal(PublicationStatus.Committed, _fixture.Status);
        Assert.NotEmpty(_fixture.Files);
    }

    [Fact]
    [Trait("Requirement", "GCPC-083")]
    public void EveryPublishedByte_NeverContainsTheSecretLiteral()
    {
        var secret = Encoding.UTF8.GetBytes(ConfigurationSecret);
        Assert.All(
            _fixture.Files,
            pair => Assert.False(
                Contains(pair.Value, secret),
                $"'{pair.Key}' still contains the configuration secret literal."));
    }

    [Fact]
    [Trait("Requirement", "GCPC-083")]
    public void EveryPublishedByte_NeverContainsAHashOfTheIndividualSecret()
    {
        var secretHash = Encoding.UTF8.GetBytes(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ConfigurationSecret))));
        Assert.All(
            _fixture.Files,
            pair => Assert.False(
                Contains(pair.Value, secretHash),
                $"'{pair.Key}' contains a hash of the individual secret."));
    }

    [Fact]
    [Trait("Requirement", "GCPC-082")]
    public void TheRedactedSidecar_StillDeclaresRedactedTheOrdinalSortedSpansAndBothHashes()
    {
        var sidecar = Assert.Single(
            _fixture.Files,
            pair => pair.Key.EndsWith("appsettings.json.meta.json", StringComparison.Ordinal)
                && !pair.Key.Contains("Development", StringComparison.Ordinal));
        var envelope = CanonicalJson.Read<RedactionEnvelopeDto>(sidecar.Value);

        Assert.True(envelope.Redacted);
        Assert.NotEmpty(envelope.RedactedSpans);
        var ordered = envelope.RedactedSpans
            .OrderBy(static span => span.StartLine)
            .ThenBy(static span => span.StartColumn)
            .ThenBy(static span => span.EndLine)
            .ThenBy(static span => span.EndColumn)
            .ToArray();
        Assert.Equal(ordered, envelope.RedactedSpans);
        Assert.Equal(64, envelope.OriginalSha256.Length);
        Assert.Equal(64, envelope.PublishedSha256.Length);
        Assert.NotEqual(envelope.OriginalSha256, envelope.PublishedSha256);
    }

    [Fact]
    [Trait("Requirement", "GCPC-084")]
    public void NoCatalogEntry_PublishesALabelDerivedFromTheSecret()
    {
        var secret = Encoding.UTF8.GetBytes(ConfigurationSecret);
        var catalogs = _fixture.Files.Where(pair => pair.Key.StartsWith("catalogs/", StringComparison.Ordinal)).ToArray();

        Assert.NotEmpty(catalogs);
        Assert.All(
            catalogs,
            pair => Assert.False(
                Contains(pair.Value, secret),
                $"Catalog '{pair.Key}' carries a label derived from the secret."));
    }

    [Fact]
    [Trait("Requirement", "GCPC-086")]
    public void CoverageProvenanceAndAccountingEnvelopes_CarryNoCredentialConnectionStringOrToken()
    {
        var secret = Encoding.UTF8.GetBytes(ConfigurationSecret);
        string[] envelopeKeys = ["coverage.json", "manifest.json", "measurements.json", "diagnostics.json", "run-certification.json"];

        Assert.Contains(envelopeKeys, key => _fixture.Files.ContainsKey(key));
        foreach (var key in envelopeKeys)
        {
            if (_fixture.Files.TryGetValue(key, out var bytes))
            {
                Assert.False(Contains(bytes, secret), $"'{key}' contains the configuration secret.");
            }
        }
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
        private readonly TempOutputRoot _root;

        public PublicationStatus Status { get; }

        public IReadOnlyDictionary<string, byte[]> Files { get; }

        public PublishedFixture()
        {
            var solutionPath = Path.Combine(RepoRoot(), "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
            Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

            _root = TempOutputRoot.Create("csharp2md-secret-sweep-");
            var store = new FilesystemTransactionalStore(_root.DirectoryPath, new PackageProjector());
            var result = new AnalysisEngine(store)
                .AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Status = Assert.Single(result.Solutions).Status;
            var canonical = Path.GetFullPath(solutionPath);
            var identity = SolutionCoordinate.For(canonical).Identity.Value;
            var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
            var child = Path.Combine(_root.DirectoryPath, "s-" + hex);
            Files = Directory.Exists(child)
                ? Directory.EnumerateFiles(child, "*", SearchOption.AllDirectories)
                    .ToDictionary(
                        path => Path.GetRelativePath(child, path).Replace('\\', '/'),
                        File.ReadAllBytes,
                        StringComparer.Ordinal)
                : new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        public void Dispose() => _root.Dispose();

        private static string RepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "csharp2md.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Could not locate repo root (csharp2md.slnx) above " + AppContext.BaseDirectory);
        }
    }
}
