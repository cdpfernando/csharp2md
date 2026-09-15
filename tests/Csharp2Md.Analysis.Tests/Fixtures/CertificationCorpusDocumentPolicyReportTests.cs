using Csharp2Md.Analysis.Inventory;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-034: proves the document policy report's counts and bytes per category against the T6
/// fixture's known contents (fixtures/CertificationCorpus/Certification.WebAssets/), and that
/// accepted plus excluded equals the total enumerated document count.
/// </summary>
public sealed class CertificationCorpusDocumentPolicyReportTests
{
    [Fact]
    [Trait("Requirement", "GCPC-034")]
    public void Collect_CertificationWebAssets_ReportsKnownCountsAndBytesPerCategory()
    {
        var report = CollectWebAssetsPolicyReport();

        var cSharp = report.For(DocumentPolicyCategory.CSharpSource);
        Assert.Equal(1, cSharp.AcceptedCount);
        Assert.Equal(781L, cSharp.AcceptedBytes);
        Assert.Equal(0, cSharp.ExcludedCount);

        var projectFile = report.For(DocumentPolicyCategory.ProjectFile);
        Assert.Equal(1, projectFile.AcceptedCount);
        Assert.Equal(207L, projectFile.AcceptedBytes);

        var frontend = report.For(DocumentPolicyCategory.FrontendScript);
        Assert.Equal(0, frontend.AcceptedCount);
        Assert.Equal(3, frontend.ExcludedCount);
        Assert.Equal(416L, frontend.ExcludedBytes); // app.ts (155) + app.js (165) + app.js.map (96)

        var staticAsset = report.For(DocumentPolicyCategory.StaticAsset);
        Assert.Equal(1, staticAsset.ExcludedCount);
        Assert.Equal(68L, staticAsset.ExcludedBytes); // logo.png

        var archive = report.For(DocumentPolicyCategory.Archive);
        Assert.Equal(1, archive.ExcludedCount);
        Assert.Equal(160L, archive.ExcludedBytes); // assets.zip

        var packageManagement = report.For(DocumentPolicyCategory.PackageManagementArtifact);
        Assert.Equal(1, packageManagement.ExcludedCount);
        Assert.Equal(124L, packageManagement.ExcludedBytes); // package-lock.json

        var binaryOrCertificate = report.For(DocumentPolicyCategory.BinaryOrCertificate);
        Assert.Equal(1, binaryOrCertificate.ExcludedCount);
        Assert.Equal(2421L, binaryOrCertificate.ExcludedBytes); // cert.pfx
    }

    [Fact]
    [Trait("Requirement", "GCPC-034")]
    public void Collect_CertificationWebAssets_AcceptedPlusExcludedEqualsEnumeratedTotal()
    {
        var report = CollectWebAssetsPolicyReport();

        // 2 accepted (WebAssetHost.cs, Certification.WebAssets.csproj) + 7 excluded
        // (app.ts, app.js, app.js.map, logo.png, assets.zip, package-lock.json, cert.pfx) = 9.
        Assert.Equal(2, report.AcceptedCount);
        Assert.Equal(7, report.ExcludedCount);
        Assert.Equal(9, report.AcceptedCount + report.ExcludedCount);
        Assert.Equal(988L, report.AcceptedBytes);
        Assert.Equal(3189L, report.ExcludedBytes);
    }

    private static DocumentPolicyReport CollectWebAssetsPolicyReport()
    {
        var solutionPath = CertificationCorpusPaths.SolutionPath;
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))
            ?? throw new InvalidOperationException($"'{solutionPath}' has no directory.");
        var existing = listed
            .Select(path => Path.GetFullPath(Path.Combine(solutionDirectory, path)))
            .Where(File.Exists)
            .ToArray();
        var root = AuthorizedRoot.Compute(solutionPath, existing);
        var facts = InventoryFacts.Create(solutionPath, listed, root);
        var webAssetsProjectPath = existing.Single(
            path => path.Contains("Certification.WebAssets.csproj", StringComparison.Ordinal));
        var project = facts.Projects.Single(
            candidate => candidate.Id.Value.Contains("Certification.WebAssets.csproj", StringComparison.Ordinal));

        return DocumentInventory.Collect(root, project, webAssetsProjectPath, existing).PolicyReport;
    }
}
