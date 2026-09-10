using Csharp2Md.Analysis.Inventory;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class DocumentPolicyReportTests
{
    [Fact]
    [Trait("Requirement", "GCPC-034")]
    public void FromOutcomes_MixOfAcceptedAndExcluded_TotalsCountsAndBytesPerCategory()
    {
        var report = DocumentPolicyReport.FromOutcomes(
        [
            (DocumentPolicyCategory.CSharpSource, true, 100L),
            (DocumentPolicyCategory.CSharpSource, true, 50L),
            (DocumentPolicyCategory.FrontendScript, false, 20L),
            (DocumentPolicyCategory.FrontendScript, false, 5L),
            (DocumentPolicyCategory.Archive, false, 7L),
        ]);

        var cSharp = report.For(DocumentPolicyCategory.CSharpSource);
        Assert.Equal(2, cSharp.AcceptedCount);
        Assert.Equal(150L, cSharp.AcceptedBytes);
        Assert.Equal(0, cSharp.ExcludedCount);
        Assert.Equal(0L, cSharp.ExcludedBytes);

        var frontend = report.For(DocumentPolicyCategory.FrontendScript);
        Assert.Equal(0, frontend.AcceptedCount);
        Assert.Equal(2, frontend.ExcludedCount);
        Assert.Equal(25L, frontend.ExcludedBytes);

        var archive = report.For(DocumentPolicyCategory.Archive);
        Assert.Equal(1, archive.ExcludedCount);
        Assert.Equal(7L, archive.ExcludedBytes);
    }

    [Fact]
    [Trait("Requirement", "GCPC-034")]
    public void AcceptedAndExcludedTotals_SumAcrossCategories_EqualTheEnumeratedCount()
    {
        var report = DocumentPolicyReport.FromOutcomes(
        [
            (DocumentPolicyCategory.CSharpSource, true, 10L),
            (DocumentPolicyCategory.ProjectFile, true, 5L),
            (DocumentPolicyCategory.FrontendScript, false, 3L),
            (DocumentPolicyCategory.Archive, false, 2L),
        ]);

        Assert.Equal(2, report.AcceptedCount);
        Assert.Equal(15L, report.AcceptedBytes);
        Assert.Equal(2, report.ExcludedCount);
        Assert.Equal(5L, report.ExcludedBytes);
        Assert.Equal(4, report.AcceptedCount + report.ExcludedCount);
    }

    [Fact]
    [Trait("Requirement", "GCPC-034")]
    public void For_CategoryWithNoOutcomes_ReturnsAllZeroTotal()
    {
        var report = DocumentPolicyReport.Empty;

        var total = report.For(DocumentPolicyCategory.BinaryOrCertificate);

        Assert.Equal(0, total.AcceptedCount);
        Assert.Equal(0L, total.AcceptedBytes);
        Assert.Equal(0, total.ExcludedCount);
        Assert.Equal(0L, total.ExcludedBytes);
    }

    [Fact]
    [Trait("Requirement", "GCPC-034")]
    public void Merge_TwoReports_AddsMatchingCategoriesAndKeepsDistinctOnes()
    {
        var left = DocumentPolicyReport.FromOutcomes(
        [
            (DocumentPolicyCategory.CSharpSource, true, 10L),
            (DocumentPolicyCategory.Archive, false, 4L),
        ]);
        var right = DocumentPolicyReport.FromOutcomes(
        [
            (DocumentPolicyCategory.CSharpSource, true, 6L),
            (DocumentPolicyCategory.StaticAsset, false, 1L),
        ]);

        var merged = left.Merge(right);

        var cSharp = merged.For(DocumentPolicyCategory.CSharpSource);
        Assert.Equal(2, cSharp.AcceptedCount);
        Assert.Equal(16L, cSharp.AcceptedBytes);
        Assert.Equal(1, merged.For(DocumentPolicyCategory.Archive).ExcludedCount);
        Assert.Equal(1, merged.For(DocumentPolicyCategory.StaticAsset).ExcludedCount);
        Assert.Equal(4, merged.AcceptedCount + merged.ExcludedCount);
    }
}
