using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class PackageBuilderTests
{
    [Fact][Trait("Requirement", "PKG-01")] public void Build_ContainsRootManifest() => Assert.Contains(Plan().Artifacts, artifact => artifact.Path.Value == "manifest.json");
    [Fact][Trait("Requirement", "PKG-03")] public void Build_ContainsAllMachineAndMarkdownBytes() => Assert.All(Plan().Artifacts, artifact => Assert.False(artifact.Payload.IsDefaultOrEmpty));
    [Fact][Trait("Requirement", "PKG-04")] public void Build_ReservesMeasurementsBeforeStaging() => Assert.Contains(Plan().Artifacts, artifact => artifact.Path.Value == "measurements.json");
    [Fact][Trait("Requirement", "PKG-05")] public void Build_ReservesCertificationBeforeStaging() => Assert.Contains(Plan().Artifacts, artifact => artifact.Path.Value == "certification.json");
    [Fact][Trait("Requirement", "PKG-06")] public void Build_PreservesMarkdownArtifacts() => Assert.Contains(Plan().Artifacts, artifact => artifact.Family == ArtifactFamily.Markdown);
    [Fact][Trait("Requirement", "PKG-08")] public void Build_RecordsIncludeTestsInManifest() => Assert.True(PackageBuilder.Build(Model(), true).Manifest.IncludeTests);
    [Fact][Trait("Requirement", "STO-06")] public void Build_OrdersArtifactsCanonically() => Assert.Equal(Plan().Artifacts.Select(artifact => artifact.Path.Value).Order(StringComparer.Ordinal), Plan().Artifacts.Select(artifact => artifact.Path.Value));
    [Fact][Trait("Requirement", "STO-07")] public void Build_IsPermutationStable() { var first=PackageBuilder.Build(Model()); var second=PackageBuilder.Build(Model()); Assert.Equal(first.PackageDigest, second.PackageDigest); Assert.Equal(first.Artifacts.Select(artifact => artifact.Payload), second.Artifacts.Select(artifact => artifact.Payload)); }
    [Fact][Trait("Requirement", "CRT-03")] public void Build_SeparatesExtractionAndPublicationMeasurements() { var measurements=Plan().Measurements; Assert.Equal(0, measurements.Extraction.ExtractedCount); Assert.Equal(Plan().Artifacts.Length, measurements.PublishedArtifactCount); }
    [Fact][Trait("Requirement", "CRT-03")] public void Build_RecordsFilteredReason() => Assert.Equal("retention", Assert.Single(Plan().Measurements.FilteredByReason).Reason);
    [Fact][Trait("Requirement", "EDG-03")] public void Build_FailsBeforePublicationWhenArtifactCeilingIsExceeded() => Assert.StartsWith("package-budget: 'artifacts'. corpus: 'unpinned'. by-family: ", Assert.Throws<PackageBudgetExceededException>(() => PackageBuilder.Build(Model(), false, new PackageBudget(1, long.MaxValue))).Message, StringComparison.Ordinal);
    [Fact][Trait("Requirement", "EDG-03")] public void Build_FailsBeforePublicationWhenByteCeilingIsExceeded() => Assert.StartsWith("package-budget: 'bytes'. corpus: 'unpinned'. by-family: ", Assert.Throws<PackageBudgetExceededException>(() => PackageBuilder.Build(Model(), false, new PackageBudget(int.MaxValue, 1))).Message, StringComparison.Ordinal);
    [Fact][Trait("Requirement", "EDG-03")] public void Build_BudgetDiagnosticQuotesTheFamilyBreakdown()
    {
        var message = Assert.Throws<PackageBudgetExceededException>(() => PackageBuilder.Build(Model(), false, new PackageBudget(1, long.MaxValue))).Message;
        Assert.Contains("Table=4/", message, StringComparison.Ordinal);
        Assert.Contains("Graph=1/", message, StringComparison.Ordinal);
        Assert.Contains("Index=9/", message, StringComparison.Ordinal);
        Assert.Contains("Measure=2/", message, StringComparison.Ordinal);
        Assert.Contains("Markdown=2/", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Manifest=", message, StringComparison.Ordinal);
    }
    // One component root with one HTTP dependency and one measure produces a package whose family shape is
    // fixed by the writers: 4 local tables, 1 entity graph shard, 8 navigation indexes plus the documents
    // router, 2 measure artifacts and 2 Markdown pages (the summary and the root page). The dependency is
    // component-scoped, so no document earns a page. CRT-03 requires the breakdown to report exactly that.
    [Fact][Trait("Requirement", "CRT-03")] public void Build_CountsArtifactsByFamily() => Assert.Equal(
        [(ArtifactFamily.Table, 4), (ArtifactFamily.Graph, 1), (ArtifactFamily.Index, 9), (ArtifactFamily.Measure, 2), (ArtifactFamily.Markdown, 2)],
        Plan().Measurements.ByFamily.Select(family => (family.Family, family.ArtifactCount)));
    [Fact][Trait("Requirement", "CRT-03")] public void Build_SumsBytesByFamily() => Assert.All(
        Plan().Measurements.ByFamily,
        family => Assert.Equal(Plan().Artifacts.Where(artifact => artifact.Family == family.Family).Sum(artifact => (long)artifact.Payload.Length), family.Bytes));
    [Fact][Trait("Requirement", "CRT-03")] public void Build_LeavesThePublicationTrailersOutOfTheBreakdown()
    {
        var measurements = Plan().Measurements;
        Assert.Equal(measurements.PublishedArtifactCount - 3, measurements.ByFamily.Sum(family => family.ArtifactCount));
        Assert.DoesNotContain(measurements.ByFamily, family => family.Family is ArtifactFamily.Manifest or ArtifactFamily.Certification or ArtifactFamily.Measurement);
    }
    [Fact][Trait("Requirement", "CRT-03")] public void Build_OrdersTheFamilyBreakdownCanonically() { var breakdown = Plan().Measurements.ByFamily; Assert.Equal(breakdown.Select(family => family.Family).Order(), breakdown.Select(family => family.Family)); Assert.Equal(CanonicalJson.Write(Plan().Measurements).ToArray(), CanonicalJson.Write(PackageBuilder.Build(Model()).Measurements).ToArray()); }
    [Fact][Trait("Requirement", "CRT-03")] public void Build_RoundTripsTheFamilyBreakdownThroughCanonicalJson()
    {
        var expected = Plan().Measurements.ByFamily;
        var read = CanonicalJson.Read<PublicationMeasurements>(Plan().Artifacts.Single(artifact => artifact.Path.Value == "measurements.json").Payload.AsSpan());
        Assert.Equal(expected.Select(family => (family.Family, family.ArtifactCount, family.Bytes)), read.ByFamily.Select(family => (family.Family, family.ArtifactCount, family.Bytes)));
    }
    [Fact][Trait("Requirement", "PKG-01")] public void Build_AcceptsMultipleSolutions() { var model=Model(); var extra=new SolutionRetrievalModel(CanonicalIdentity.CreateSolution("other","src/Other.sln"),[new EntityHandle("component:other")], [], []); var plan=PackageBuilder.Build(new RetrievalModel(model.Solutions.Add(extra))); Assert.Equal(2, plan.Manifest.Solutions.Length); }
    [Fact][Trait("Requirement", "STO-04")] public void Build_AssignsOneArtifactPathPerPayload() => Assert.Equal(Plan().Artifacts.Length, Plan().Artifacts.Select(artifact => artifact.Path.Value).Distinct(StringComparer.Ordinal).Count());
    // EDG-03 names a limit "aplicavel ao corpus", and CRT-04 and CRT-05 are where the spec writes those numbers
    // down: 1,500 artifacts / 64 MiB for eShopOnContainers and 750 / 25 MiB for Pitstop. The expectations below
    // are the spec's figures converted by hand, never read back from PackageBudget.
    [Fact][Trait("Requirement", "CRT-04")] public void ForCorpus_AppliesTheCeilingCrt04PinsForEShopOnContainers()
    {
        var budget = PackageBudget.ForCorpus(Corpus("eShopOnContainers-ServicesAndWebApps.sln"));
        Assert.Equal(1_500, budget.MaximumArtifacts);
        Assert.Equal(67_108_864L, budget.MaximumBytes);
    }

    [Fact][Trait("Requirement", "CRT-05")] public void ForCorpus_AppliesTheCeilingCrt05PinsForPitstop()
    {
        var budget = PackageBudget.ForCorpus(Corpus("pitstop.sln"));
        Assert.Equal(750, budget.MaximumArtifacts);
        Assert.Equal(26_214_400L, budget.MaximumBytes);
    }

    // eShop is deliberately not pinned: the spec gives it no size ceiling, only CRT-06's collision rule.
    [Fact][Trait("Requirement", "EDG-03")] public void ForCorpus_LeavesACorpusTheSpecDoesNotPinOnTheDefault()
    {
        var budget = PackageBudget.ForCorpus(Corpus("eShop.slnx"));
        Assert.Equal(1_500, budget.MaximumArtifacts);
        Assert.Equal(100_663_296L, budget.MaximumBytes);
    }

    // Both ceilings bind the one committed package, so the applied limit is the componentwise minimum - and a
    // pinned corpus is therefore always strictly tighter than the 96 MiB default rather than merely different.
    [Fact][Trait("Requirement", "EDG-03")] public void ForCorpus_AppliesEveryPinnedCeilingAPackageTouches()
    {
        var budget = PackageBudget.ForCorpus(new RetrievalModel(
            [Solution("eshoponcontainers", "src/eShopOnContainers-ServicesAndWebApps.sln"), Solution("pitstop", "src/pitstop.sln")]));
        Assert.Equal(750, budget.MaximumArtifacts);
        Assert.Equal(26_214_400L, budget.MaximumBytes);
        Assert.True(budget.MaximumBytes < PackageBudget.Default.MaximumBytes);
    }

    // The selection has to reach Build, not merely exist. The same 800-root model is refused under Pitstop's
    // 750-artifact ceiling and accepted under an unpinned name, where the default allows 1,500.
    [Fact][Trait("Requirement", "EDG-03")] public void Build_RefusesAPinnedCorpusAtItsOwnCeilingRatherThanTheDefault()
    {
        Assert.StartsWith(
            "package-budget: 'artifacts'. corpus: 'pitstop.sln'. by-family: ",
            Assert.Throws<PackageBudgetExceededException>(() => PackageBuilder.Build(Crowded("src/pitstop.sln"))).Message,
            StringComparison.Ordinal);
        var accepted = PackageBuilder.Build(Crowded("src/App.sln"));
        Assert.InRange(accepted.Artifacts.Length, 751, 1_500);
    }

    // CRT-03's remaining two dimensions. The single-solution package puts every solution-scoped artifact under
    // one prefix, so its count is the family total (18) minus the one package-level Markdown summary.
    [Fact][Trait("Requirement", "CRT-03")] public void Build_AttributesArtifactsToTheirSolution()
    {
        var plan = Plan();
        var solution = Assert.Single(plan.Measurements.BySolution);
        var prefix = $"solutions/{solution.SolutionId}/";
        var owned = plan.Artifacts.Where(artifact => artifact.Path.Value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        Assert.Equal(plan.Measurements.ByFamily.Sum(family => family.ArtifactCount) - 1, solution.ArtifactCount);
        Assert.Equal(owned.Length, solution.ArtifactCount);
        Assert.Equal(owned.Sum(artifact => (long)artifact.Payload.Length), solution.Bytes);
    }

    [Fact][Trait("Requirement", "CRT-03")] public void Build_SeparatesTheTwoSolutionsOfOnePackage()
    {
        var extra = new SolutionRetrievalModel(CanonicalIdentity.CreateSolution("other", "src/Other.sln"), [new EntityHandle("component:other")], [], []);
        var plan = PackageBuilder.Build(new RetrievalModel(Model().Solutions.Add(extra)));
        Assert.Equal(2, plan.Measurements.BySolution.Length);
        Assert.All(plan.Measurements.BySolution, solution => Assert.True(solution.ArtifactCount > 0 && solution.Bytes > 0));
        Assert.Equal(
            plan.Measurements.BySolution.Select(solution => solution.SolutionId).Order(StringComparer.Ordinal),
            plan.Measurements.BySolution.Select(solution => solution.SolutionId));
    }

    // The corpus dimension records the ceiling that was actually applied, so measurements.json says which EDG-03
    // limit the package was measured against. 26,214,400 is CRT-05's 25 MiB, computed here rather than read back.
    [Fact][Trait("Requirement", "CRT-03")] public void Build_RecordsTheCorpusAndTheCeilingItWasMeasuredAgainst()
    {
        var plan = PackageBuilder.Build(Corpus("pitstop.sln"));
        var corpus = plan.Measurements.Corpus;
        Assert.NotNull(corpus);
        Assert.Equal("pitstop.sln", corpus.Corpus);
        Assert.Equal(750, corpus.MaximumArtifacts);
        Assert.Equal(26_214_400L, corpus.MaximumBytes);
        Assert.Equal(plan.Measurements.ByFamily.Sum(family => family.ArtifactCount), corpus.ArtifactCount);
        Assert.Equal(plan.Measurements.ByFamily.Sum(family => family.Bytes), corpus.Bytes);
    }

    [Fact][Trait("Requirement", "CRT-03")] public void Build_NamesACorpusTheSpecDoesNotPinAsUnpinned() =>
        Assert.Equal("unpinned", Plan().Measurements.Corpus?.Corpus);

    [Fact][Trait("Requirement", "CRT-03")] public void Build_RoundTripsTheSolutionAndCorpusDimensions()
    {
        var plan = Plan();
        var read = CanonicalJson.Read<PublicationMeasurements>(plan.Artifacts.Single(artifact => artifact.Path.Value == "measurements.json").Payload.AsSpan());
        Assert.Equal(
            plan.Measurements.BySolution.Select(solution => (solution.SolutionId, solution.ArtifactCount, solution.Bytes)),
            read.BySolution.Select(solution => (solution.SolutionId, solution.ArtifactCount, solution.Bytes)));
        var corpus = read.Corpus;
        Assert.NotNull(corpus);
        Assert.Equal(plan.Measurements.Corpus!.Corpus, corpus.Corpus);
        Assert.Equal(plan.Measurements.Corpus!.MaximumBytes, corpus.MaximumBytes);
    }

    [Fact][Trait("Requirement", "EDG-03")] public void Build_BudgetDiagnosticNamesTheCorpus() =>
        Assert.Contains("corpus: 'pitstop.sln'.", Assert.Throws<PackageBudgetExceededException>(
            () => PackageBuilder.Build(Crowded("src/pitstop.sln"))).Message, StringComparison.Ordinal);

    private static RetrievalModel Corpus(string fileName) => new([Solution("corpus", "src/" + fileName)]);

    private static SolutionRetrievalModel Solution(string key, string path) =>
        new(CanonicalIdentity.CreateSolution(key, path), [new EntityHandle("component:orders")], [], []);

    private static RetrievalModel Crowded(string path) =>
        new([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution("crowded", path),
            [.. Enumerable.Range(0, 800).Select(index => new EntityHandle($"component:c{index}"))],
            [],
            [])]);

    private static PackagePlan Plan() => PackageBuilder.Build(Model());
    private static RetrievalModel Model() =>
        new([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution("app", "src/App.sln"),
            [new EntityHandle("component:orders")],
            [new AggregatedDependency(AggregationScope.Component, new EntityHandle("component:orders"), new EntityHandle("component:billing"), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], [])],
            [new ScopeMeasures(AggregationScope.Component, new EntityHandle("component:orders"), 0, 1, 0, [], [], new GapCounts(0, 0, 0))])]);
}
