using System.Text.RegularExpressions;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeMultiSolutionTests
{
    [Fact]
    [Trait("Requirement", "STOR-56")]
    [Trait("Requirement", "STOR-57")]
    public async Task Analyze_TwoSolutions_SecondCommitRejected_LeavesFirstPackageAndExits2()
    {
        var firstSolution = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        var secondSolution = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Payments",
            "Acme.Payments.slnx");
        Assert.True(Path.Exists(firstSolution), $"Fixture solution was not found at '{firstSolution}'.");
        Assert.True(Path.Exists(secondSolution), $"Fixture solution was not found at '{secondSolution}'.");

        var outputPath = CliTestPaths.UniqueOutputPath();
        var rejectKey = Path.GetFullPath(secondSolution);
        IAnalysisEngine engine = new AnalysisEngine(
            new SecondCommitRejectingStore(new FilesystemTransactionalStore(outputPath), rejectKey));

        try
        {
            var (exitCode, _, _) = await CliInvoke.RunAsync(
                [
                    "analyze",
                    "--solution", firstSolution,
                    "--solution", secondSolution,
                    "--output", outputPath,
                ],
                engine);

            Assert.Equal(2, exitCode);

            var children = Directory.Exists(outputPath)
                ? Directory.GetDirectories(outputPath)
                    .Where(static path => Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"))
                    .ToArray()
                : [];
            var child = Assert.Single(children);
            var result = FactualPackageReader.Read(child);
            Assert.NotEmpty(result.Snapshot.Facts);
            Assert.Contains(result.Snapshot.Facts, static fact => fact is Solution);

            var manifest = CanonicalJson.Read<ManifestEnvelope>(
                File.ReadAllBytes(Path.Combine(child, "manifest.json")));
            Assert.Equal("Acme.Orders.slnx", manifest.SolutionFileName);
            Assert.NotEqual("Acme.Payments.slnx", manifest.SolutionFileName);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    private sealed class SecondCommitRejectingStore : ITransactionalStore
    {
        private readonly ITransactionalStore _inner;
        private readonly string _rejectCanonical;

        public SecondCommitRejectingStore(ITransactionalStore inner, string rejectCanonical)
        {
            _inner = inner;
            _rejectCanonical = rejectCanonical;
        }

        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader)
        {
            var session = _inner.Open(solutionKey, sourceReader);
            if (string.Equals(solutionKey, _rejectCanonical, StringComparison.OrdinalIgnoreCase))
            {
                return new RejectingSession(session);
            }

            return session;
        }

        private sealed class RejectingSession : IStoreSession
        {
            private readonly IStoreSession _inner;

            public RejectingSession(IStoreSession inner)
            {
                _inner = inner;
            }

            public void Stage(FactualSnapshot snapshot) => _inner.Stage(snapshot);

            public CommittedPublication Commit() =>
                throw new PublicationRejectedException("schema", "second-solution");

            public void Abort() => _inner.Abort();
        }
    }
}
