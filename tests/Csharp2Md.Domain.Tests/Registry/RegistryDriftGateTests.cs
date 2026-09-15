using System.Text;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class RegistryDriftGateTests
{
    private static readonly UTF8Encoding NoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    [Trait("Requirement", "TAX-86")]
    [Trait("Requirement", "ENG-60")]
    public void CommittedRegistry_MatchesFreshEmission_ByteForByte()
    {
        var committedBytes = File.ReadAllBytes(TaxonomyRegistryWriter.CommittedPath);
        var freshBytes = NoBomUtf8.GetBytes(TaxonomyRegistryWriter.Write(TaxonomyTables.Default));

        Assert.True(
            committedBytes.AsSpan().SequenceEqual(freshBytes),
            $"contracts/taxonomy-registry.json has drifted from the emitter: {DescribeDifference(committedBytes, freshBytes)}");
    }

    [Fact]
    [Trait("Requirement", "ENG-61")]
    public void TaxonomyTraceability_Tax46And50And51And53_ReadVerifiedWithNoSpecPrecisionGap()
    {
        var path = Path.Combine(
            DomainTestPaths.RepoRoot, ".specs", "features", "knowledge-taxonomy-contract", "spec.md");
        var lines = File.ReadAllLines(path);

        foreach (var id in new[] { "TAX-46", "TAX-50", "TAX-51", "TAX-53" })
        {
            var line = Assert.Single(lines, candidate => candidate.StartsWith($"| {id} ", StringComparison.Ordinal));
            Assert.Contains("| Verified |", line, StringComparison.Ordinal);
            Assert.DoesNotContain("spec-precision gap", line, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-86")]
    public void HandEditedCopy_FailsTheByteComparison_NamingTheDifferingEntry()
    {
        var freshJson = TaxonomyRegistryWriter.Write(TaxonomyTables.Default);
        // observation_schema_version stays at 1 under F3's version bump (only schema_version,
        // taxonomy_version, extractor_set_version and classifier_set_version advance to 2), so it is
        // still a genuine hand-edit target here.
        var handEdited = freshJson.Replace("\"observation_schema_version\": 1", "\"observation_schema_version\": 2", StringComparison.Ordinal);
        Assert.NotEqual(freshJson, handEdited);

        var freshBytes = NoBomUtf8.GetBytes(freshJson);
        var handEditedBytes = NoBomUtf8.GetBytes(handEdited);

        Assert.False(freshBytes.AsSpan().SequenceEqual(handEditedBytes));

        var description = DescribeDifference(freshBytes, handEditedBytes);
        Assert.Contains("observation_schema_version", description, StringComparison.Ordinal);
        Assert.Contains("line 4", description, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-86")]
    public void ByteComparison_CatchesOrderingAndWhitespaceChangesAParsedComparisonWouldTreatAsEquivalent()
    {
        var freshJson = TaxonomyRegistryWriter.Write(TaxonomyTables.Default);

        var reordered = freshJson
            .Replace("\"contract-implementation\"", "\"__temp-swap__\"", StringComparison.Ordinal)
            .Replace("\"data-object-mapping\"", "\"contract-implementation\"", StringComparison.Ordinal)
            .Replace("\"__temp-swap__\"", "\"data-object-mapping\"", StringComparison.Ordinal);
        Assert.NotEqual(freshJson, reordered);
        Assert.False(NoBomUtf8.GetBytes(freshJson).AsSpan().SequenceEqual(NoBomUtf8.GetBytes(reordered)));

        var trailingNewlineAdded = freshJson + "\n";
        using var freshDocument = System.Text.Json.JsonDocument.Parse(freshJson);
        using var whitespaceChangedDocument = System.Text.Json.JsonDocument.Parse(trailingNewlineAdded);
        Assert.Equal(freshDocument.RootElement.GetRawText(), whitespaceChangedDocument.RootElement.GetRawText());
        Assert.False(NoBomUtf8.GetBytes(freshJson).AsSpan().SequenceEqual(NoBomUtf8.GetBytes(trailingNewlineAdded)));
    }

    [Fact]
    [Trait("Requirement", "TAX-86")]
    public void CommittedPath_ResolvesFromAnyCurrentWorkingDirectory()
    {
        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetTempPath();

            Assert.True(File.Exists(TaxonomyRegistryWriter.CommittedPath));
            Assert.Equal(
                Path.Combine("contracts", "taxonomy-registry.json"),
                Path.GetRelativePath(DomainTestPaths.RepoRoot, TaxonomyRegistryWriter.CommittedPath));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    private static string DescribeDifference(byte[] expected, byte[] actual)
    {
        var expectedLines = Encoding.UTF8.GetString(expected).Split('\n');
        var actualLines = Encoding.UTF8.GetString(actual).Split('\n');

        var differences = new List<string>();
        var lineCount = Math.Max(expectedLines.Length, actualLines.Length);
        for (var i = 0; i < lineCount; i++)
        {
            var expectedLine = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var actualLine = i < actualLines.Length ? actualLines[i] : "<missing>";
            if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                differences.Add($"line {i + 1}: expected '{expectedLine}' but found '{actualLine}'");
            }
        }

        return differences.Count == 0 ? "(no line-level differences found)" : string.Join("; ", differences);
    }
}
