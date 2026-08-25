using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Tests.Mapping;

namespace Csharp2Md.Storage.Tests.Filesystem;

public sealed class FilesystemCompactLayoutTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-39")]
    [Trait("Requirement", "STOR-40")]
    [Trait("Requirement", "STOR-41")]
    [Trait("Requirement", "STOR-42")]
    [Trait("Requirement", "STOR-43")]
    [Trait("Requirement", "STOR-46")]
    public void Commit_MultiFamilySnapshot_MatchesPartitionLayoutWithoutIdentityDirectories()
    {
        using var output = TempOutputRoot.Create();
        var snapshot = MultiFamilySnapshot();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var session = store.Open(SolutionKey);
        session.Stage(snapshot);
        session.Commit();

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var onDisk = FilesystemTestPaths.SnapshotFiles(child);
        var relativeFiles = onDisk.Keys.ToArray();
        var directories = Directory.EnumerateDirectories(child, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetFileName(path))
            .ToArray();

        Assert.Contains("facts/structural.json", relativeFiles);
        Assert.Contains("facts/architecture.json", relativeFiles);
        Assert.Contains("facts/contract.json", relativeFiles);
        Assert.Contains("facts/persistence.json", relativeFiles);
        Assert.Contains("facts/configuration.json", relativeFiles);

        var invocationWire = TaxonomyTables.Default.ObservationKinds
            .Single(descriptor => descriptor.Kind == ObservationKind.Invocation).WireName;
        var containsWire = TaxonomyTables.Default.Relations
            .Single(descriptor => descriptor.Kind == RelationKind.Contains).WireName;
        Assert.NotEqual(nameof(ObservationKind.Invocation), invocationWire);
        Assert.Contains("observations/" + invocationWire + ".json", relativeFiles);
        Assert.Contains("relations/confirmed/" + containsWire + ".json", relativeFiles);

        var identities = snapshot.Facts.Select(fact => fact.Reference.Id.Value)
            .Concat(snapshot.Observations.Select(observation => observation.Identity.Owner.Id.Value))
            .ToArray();
        Assert.NotEmpty(identities);
        Assert.All(identities, identity => Assert.DoesNotContain(identity, directories));
        Assert.DoesNotContain("Payments.Api", directories);
        Assert.DoesNotContain("Invoice.cs", directories);
        Assert.DoesNotContain("Acme.Payments", directories);

        Assert.All(relativeFiles, key =>
        {
            Assert.DoesNotContain("Payments.Api", key, StringComparison.Ordinal);
            Assert.DoesNotContain("Invoice.cs", key, StringComparison.Ordinal);
            Assert.DoesNotContain("catalogs", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("postings", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("retrieval.md", key, StringComparison.OrdinalIgnoreCase);
            Assert.False(key.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
            Assert.False(key.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        });
        Assert.DoesNotContain("catalogs", directories, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("postings", directories, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("source", directories, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, bytes) in onDisk)
        {
            if (key == "measurements.json")
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("\"timestamp\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"duration_milliseconds\"", text, StringComparison.Ordinal);
        }

        AssertCanonicalRecordsSerializedOnce(onDisk);
    }

    private static void AssertCanonicalRecordsSerializedOnce(IReadOnlyDictionary<string, byte[]> onDisk)
    {
        var payloadFiles = onDisk
            .Where(pair =>
                pair.Key.StartsWith("facts/", StringComparison.Ordinal)
                || pair.Key.StartsWith("observations/", StringComparison.Ordinal)
                || pair.Key.StartsWith("relations/", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(payloadFiles);

        for (var left = 0; left < payloadFiles.Length; left++)
        {
            for (var right = left + 1; right < payloadFiles.Length; right++)
            {
                var leftJson = Encoding.UTF8.GetString(payloadFiles[left].Value);
                var rightJson = Encoding.UTF8.GetString(payloadFiles[right].Value);
                Assert.DoesNotContain(leftJson, rightJson, StringComparison.Ordinal);
                Assert.DoesNotContain(rightJson, leftJson, StringComparison.Ordinal);
            }
        }
    }

    private static FactualSnapshot MultiFamilySnapshot()
    {
        var snapshot = FactualSnapshot.Empty;
        foreach (object[] row in FactRoundTripTests.FactFixtures())
        {
            snapshot = snapshot.Merge((FactualSnapshot)row[1]);
        }

        return snapshot.Merge(ObservationSnapshot()).Merge(ContainsRelationSnapshot());
    }

    private static FactualSnapshot ObservationSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln"));
        var observation = Observation.Create(
            solution.Reference,
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Payments/Invoice.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("BIND001", "Bound successfully."),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
        return new FactualSnapshot([], [observation], [], [], [], []);
    }

    private static FactualSnapshot ContainsRelationSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var relation = ConfirmedRelation.Create(
            RelationKind.Contains,
            Solution.Create(solutionId).Reference,
            Project.Create(projectId).Reference,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([
                new ObservationIdentity(
                    Solution.Create(solutionId).Reference,
                    ObservationKind.Invocation,
                    NormalizedPayload.Create([]),
                    1)]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
        return new FactualSnapshot([], [], [relation], [], [], []);
    }
}
