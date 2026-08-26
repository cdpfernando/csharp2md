using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class SuspectedSecretExtractionTests
{
    private const string PlantedSecret = "secret";
    private const string PlantedLiteral = "Password=" + PlantedSecret;

    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public async Task AnalyzeAsync_PlantedConfigurationPassword_OmitsSecretAndRecordsMaskedExcerpt()
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");

        var tree = Directory.CreateTempSubdirectory("csharp2md-planted-secret-");
        try
        {
            var clone = Path.Combine(tree.FullName, "clone");
            CopyClone(fixture, clone);
            var controller = Path.Combine(clone, "SyntheticSolution", "Acme.Orders", "Api", "OrdersController.cs");
            var original = File.ReadAllText(controller);
            Assert.Contains("_configuration[\"Logging:Level\"]", original, StringComparison.Ordinal);
            File.WriteAllText(
                controller,
                original.Replace(
                    "_configuration[\"Logging:Level\"]",
                    "_configuration[\"" + PlantedLiteral + "\"]",
                    StringComparison.Ordinal));
            var plantedBytes = File.ReadAllBytes(controller);
            var fileHash = Convert.ToHexStringLower(SHA256.HashData(plantedBytes));
            var secretHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(PlantedSecret)));
            Assert.NotEqual(secretHash, fileHash);

            var solutionPath = Path.Combine(clone, "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
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

            foreach (var fragment in payloads)
            {
                var text = Encoding.UTF8.GetString(fragment.Payload.AsSpan());
                Assert.DoesNotContain(PlantedLiteral, text, StringComparison.Ordinal);
                AssertNoPlantedSecretValue(JsonNode.Parse(fragment.Payload.AsSpan()), fragment.CanonicalKey);
            }

            var diagnostics = Assert.Single(payloads, fragment => fragment.CanonicalKey == "diagnostics.json");
            var envelope = CanonicalJson.Read<DiagnosticsEnvelope>(diagnostics.Payload.AsSpan());
            var suspected = envelope.Records
                .Where(record => record.Code == "suspected-secret")
                .ToArray();
            Assert.Contains(
                suspected,
                record => record.Message.Contains("Password=", StringComparison.Ordinal));
            Assert.All(
                suspected,
                record =>
                {
                    Assert.True(
                        record.Message.Contains("***", StringComparison.Ordinal)
                        || record.Message.Contains("[REDACTED]", StringComparison.Ordinal),
                        "A flagged excerpt must carry a visible redaction marker.");
                    Assert.DoesNotContain(PlantedSecret, record.Message, StringComparison.Ordinal);
                    Assert.DoesNotContain(PlantedLiteral, record.Message, StringComparison.Ordinal);
                    Assert.Contains(':', record.Message);
                });

            var observations = payloads
                .Where(fragment => fragment.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal))
                .SelectMany(fragment => CanonicalJson.Read<ImmutableArray<ObservationDto>>(fragment.Payload.AsSpan()))
                .Where(dto => dto.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal))
                .ToArray();
            Assert.NotEmpty(observations);
            Assert.All(observations, dto => Assert.Equal(fileHash, dto.DocumentHash));
            Assert.DoesNotContain(observations, dto => dto.DocumentHash.Equals(secretHash, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static void AssertNoPlantedSecretValue(JsonNode? node, string canonicalKey)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                Assert.False(
                    string.Equals(text, PlantedSecret, StringComparison.Ordinal),
                    $"Canonical payload '{canonicalKey}' still contains the planted secret value.");
                Assert.DoesNotContain(PlantedLiteral, text, StringComparison.Ordinal);
                break;
            case JsonObject obj:
                foreach (var property in obj)
                {
                    AssertNoPlantedSecretValue(property.Value, canonicalKey);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    AssertNoPlantedSecretValue(item, canonicalKey);
                }

                break;
        }
    }

    private static void CopyClone(string fixture, string cloneRoot)
    {
        Directory.CreateDirectory(cloneRoot);
        File.Copy(
            Path.Combine(AnalysisTestPaths.RepoRoot, "Directory.Packages.props"),
            Path.Combine(cloneRoot, "Directory.Packages.props"));
        File.Copy(
            Path.Combine(AnalysisTestPaths.RepoRoot, "NuGet.Config"),
            Path.Combine(cloneRoot, "NuGet.Config"));
        File.WriteAllText(
            Path.Combine(cloneRoot, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """);

        var destination = Path.Combine(cloneRoot, "SyntheticSolution");
        foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(fixture, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(ExcludedSegments.Contains))
            {
                continue;
            }

            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
