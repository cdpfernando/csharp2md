namespace Csharp2Md.Analysis.Tests.Certification;

/// <summary>
/// GCPC-074, GCPC-075, GCPC-076: proves the labeled corpora authored in
/// <c>fixtures/CertificationCorpus/labels/</c> are structurally sound ground truth -- every certified
/// area carries positives, negatives and lookalikes, every item carries a source reference and a
/// written rationale, and every referenced construct still exists in its fixture, so a stale label
/// fails loudly instead of silently dropping out of the denominator.
/// </summary>
public sealed class LabeledCorpusIntegrityTests
{
    public static IEnumerable<object[]> Areas() =>
        LabeledCorpusReader.AllAreas().Select(area => new object[] { area });

    [Theory]
    [MemberData(nameof(Areas))]
    [Trait("Requirement", "GCPC-076")]
    public void Read_EveryCertifiedArea_CarriesPositiveNegativeAndLookalikeItems(CertifiedArea area)
    {
        var entries = LabeledCorpusReader.Read(area);

        Assert.Contains(entries, entry => entry.Kind == LabelKind.Positive);
        Assert.Contains(entries, entry => entry.Kind == LabelKind.Negative);
        Assert.Contains(entries, entry => entry.Kind == LabelKind.Lookalike);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    [Trait("Requirement", "GCPC-074")]
    public void Read_EveryLabeledItem_CarriesASourceReferenceAndAWrittenRationale(CertifiedArea area)
    {
        var entries = LabeledCorpusReader.Read(area);

        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Id));
            Assert.False(string.IsNullOrWhiteSpace(entry.SourceFile));
            Assert.False(string.IsNullOrWhiteSpace(entry.Symbol));
            // A one-line placeholder is not a rationale: require enough prose to actually explain the
            // expected outcome, not just restate the symbol name.
            Assert.True(
                entry.Rationale.Length >= 40,
                $"'{entry.Id}' rationale is too short to be a written justification: '{entry.Rationale}'.");
        });
    }

    [Theory]
    [MemberData(nameof(Areas))]
    [Trait("Requirement", "GCPC-075")]
    public void Read_EveryLabeledItemsConstruct_StillExistsInTheFixtureSource(CertifiedArea area)
    {
        var entries = LabeledCorpusReader.Read(area);

        Assert.All(entries, entry =>
        {
            AssertSymbolPresent(entry.Id, entry.SourceFile, entry.Symbol);
            if (entry.RelatedSourceFile is not null)
            {
                AssertSymbolPresent(entry.Id, entry.RelatedSourceFile, entry.Symbol);
            }
        });
    }

    [Fact]
    [Trait("Requirement", "GCPC-074")]
    public void Read_EveryEntryId_IsUniqueWithinItsArea()
    {
        foreach (var area in LabeledCorpusReader.AllAreas())
        {
            var entries = LabeledCorpusReader.Read(area);
            var ids = entries.Select(entry => entry.Id).ToArray();
            Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        }
    }

    private static void AssertSymbolPresent(string entryId, string relativeSourceFile, string symbol)
    {
        var fullPath = Path.Combine(AnalysisTestPaths.RepoRoot, relativeSourceFile.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"'{entryId}' cites a missing fixture file: '{fullPath}'.");

        var source = File.ReadAllText(fullPath);
        Assert.True(
            source.Contains(symbol, StringComparison.Ordinal),
            $"'{entryId}' cites symbol '{symbol}', which no longer appears in '{relativeSourceFile}'. The label is stale.");
    }
}
