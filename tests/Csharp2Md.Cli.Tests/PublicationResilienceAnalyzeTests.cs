using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Cli;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli.Tests;

public sealed class PublicationResilienceAnalyzeTests
{
    private static readonly Regex BacktickKeyPattern = new("`([^`]+)`", RegexOptions.Compiled);

    [Fact]
    [Trait("Requirement", "APR-35")]
    [Trait("Requirement", "APR-36")]
    [Trait("Requirement", "APR-37")]
    [Trait("Requirement", "APR-38")]
    [Trait("Requirement", "APR-39")]
    [Trait("Requirement", "APR-40")]
    public async Task Analyze_DefaultBudget_CommitsAValidNavigableDeterministicPackage()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "PublicationResilience",
            "PublicationResilience.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var firstOutput = CliTestPaths.UniqueOutputPath();
        var secondOutput = CliTestPaths.UniqueOutputPath();
        try
        {
            var first = await AnalyzeDefaultBudgetAsync(solutionPath, firstOutput);
            var second = await AnalyzeDefaultBudgetAsync(solutionPath, secondOutput);

            AssertCommittedWithoutPipelineFailure(first);
            AssertBuilderContainsIsStructural(first.Snapshot);
            AssertNestedSignaturesRoundTrip(first.Snapshot);
            AssertRetrievalGuideKeysResolve(first.PackageDirectory);
            AssertPublicationGates(first);
            var (validateExit, _, validateStderr) = await CliInvoke.RunAsync(
                ["validate", "--package", first.PackageDirectory]);
            Assert.True(
                validateExit is ExitCodes.Success or ExitCodes.Degraded,
                $"validate rejected the package; exit {validateExit}: {validateStderr}");
            Assert.Equal(first.ExitCode, validateExit);
            Assert.Equal(PackageDigest(first.PackageDirectory), PackageDigest(second.PackageDirectory));
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(firstOutput);
            CliTestPaths.TryDeleteDirectory(secondOutput);
        }
    }

    private static async Task<PublishedRun> AnalyzeDefaultBudgetAsync(string solutionPath, string outputPath)
    {
        string[] args = ["analyze", "--solution", solutionPath, "--output", outputPath];
        Assert.DoesNotContain(args, static argument => argument.Contains("reading-budget", StringComparison.Ordinal));
        Assert.DoesNotContain(args, static argument => argument.Contains("max-file-reads", StringComparison.Ordinal));

        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(args);
        var packageDirectory = Directory.GetDirectories(outputPath)
            .Single(static path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"));
        var read = FactualPackageReader.Read(packageDirectory);
        return new PublishedRun(exitCode, stdout, stderr, packageDirectory, read.Snapshot);
    }

    private static void AssertCommittedWithoutPipelineFailure(PublishedRun run)
    {
        Assert.DoesNotContain("csharp2md:", run.Stderr, StringComparison.Ordinal);
        Assert.True(
            run.ExitCode is ExitCodes.Success or ExitCodes.Degraded,
            $"analyze did not commit; exit {run.ExitCode}, stderr: {run.Stderr}");
        Assert.Contains(run.Snapshot.Facts, static fact => fact is Solution);
        Assert.Contains(run.Snapshot.Facts, static fact => fact is Symbol);
        Assert.True(File.Exists(Path.Combine(run.PackageDirectory, "manifest.json")));
    }

    private static void AssertBuilderContainsIsStructural(FactualSnapshot snapshot)
    {
        var constructor = Assert.Single(
            snapshot.Facts.OfType<Symbol>(),
            symbol => symbol.Signature.Value.Contains("metadata=.ctor", StringComparison.Ordinal)
                && symbol.Signature.Value.Contains("OrderBuilder", StringComparison.Ordinal));
        var owned = snapshot.Observations
            .Where(observation => observation.Identity.Owner.Equals(constructor.Reference))
            .ToArray();
        Assert.NotEmpty(owned);
        Assert.All(
            owned,
            observation => Assert.True(
                observation.Identity.Kind is ObservationKind.Invocation or ObservationKind.DataAccess,
                observation.Identity.Kind.ToString()));

        var relation = Assert.Single(
            snapshot.ConfirmedRelations,
            candidate => candidate.Kind is RelationKind.Contains && candidate.Target.Equals(constructor.Reference));
        Assert.Contains(snapshot.Facts.OfType<Document>(), document => document.Reference.Equals(relation.Source));
        Assert.NotEmpty(relation.DerivedFrom.DerivedFrom);
        Assert.All(
            relation.DerivedFrom.DerivedFrom,
            identity => Assert.False(
                identity.Kind is ObservationKind.Invocation or ObservationKind.DataAccess,
                identity.Kind.ToString()));
    }

    private static void AssertNestedSignaturesRoundTrip(FactualSnapshot snapshot)
    {
        var named = AssertSymbol(
            snapshot,
            parameters => parameters.Contains("(string Name, int Age)", StringComparison.Ordinal)
                && !parameters.Contains("[,,]", StringComparison.Ordinal)
                && !parameters.Contains("Dictionary<", StringComparison.Ordinal));
        var nested = AssertSymbol(
            snapshot,
            parameters => parameters.Contains(
                "global::System.Collections.Generic.Dictionary<(string, int), int>",
                StringComparison.Ordinal));
        var array = AssertSymbol(
            snapshot,
            parameters => parameters.Contains("int[,,]", StringComparison.Ordinal)
                && !parameters.Contains("Name", StringComparison.Ordinal)
                && !parameters.Contains("Dictionary<", StringComparison.Ordinal));
        var mixed = AssertSymbol(
            snapshot,
            parameters => parameters.Contains("(string Name, int Age)", StringComparison.Ordinal)
                && parameters.Contains("int[,,]", StringComparison.Ordinal)
                && parameters.Contains("(bool Ok, byte[] Buffer)", StringComparison.Ordinal));

        AssertRoundTrip(named);
        AssertRoundTrip(nested);
        AssertRoundTrip(array);
        AssertRoundTrip(mixed);
    }

    private static Symbol AssertSymbol(FactualSnapshot snapshot, Func<string, bool> parametersMatch) =>
        Assert.Single(
            snapshot.Facts.OfType<Symbol>(),
            symbol => parametersMatch(symbol.Signature.Component("parameters") ?? string.Empty));

    private static void AssertRoundTrip(Symbol published)
    {
        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(
                new FactualSnapshot([published], [], [], [], [], []),
                new ManifestContext("s-test", "PublicationResilience.slnx")));
        var restoredSymbol = Assert.Single(restored.Facts.OfType<Symbol>());
        Assert.Equal(published, restoredSymbol);
        Assert.Equal(published.Signature, restoredSymbol.Signature);
        Assert.Equal(published.Signature.Value, restoredSymbol.Signature.Value);
    }

    private static void AssertRetrievalGuideKeysResolve(string packageDirectory)
    {
        var relativeKeys = Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("postings/unknowns.json", relativeKeys);
        Assert.DoesNotContain("postings/frontiers.json", relativeKeys);
        Assert.Contains(relativeKeys, key => key.StartsWith("postings/unknowns.", StringComparison.Ordinal));
        Assert.Contains(relativeKeys, key => key.StartsWith("postings/frontiers.", StringComparison.Ordinal));

        var guide = File.ReadAllText(Path.Combine(packageDirectory, "retrieval.md"));
        Assert.DoesNotContain("`postings/unknowns.json`", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("`postings/frontiers.json`", guide, StringComparison.Ordinal);
        foreach (Match match in BacktickKeyPattern.Matches(guide))
        {
            var candidate = match.Groups[1].Value;
            if (!candidate.Contains('/', StringComparison.Ordinal)
                || (!candidate.EndsWith(".json", StringComparison.Ordinal)
                    && !candidate.EndsWith(".md", StringComparison.Ordinal)))
            {
                continue;
            }

            Assert.True(
                relativeKeys.Contains(candidate),
                $"retrieval.md backticks '{candidate}' which is absent from the publication.");
        }
    }

    private static void AssertPublicationGates(PublishedRun run)
    {
        PackageValidator.ValidatePackageDirectory(run.PackageDirectory);
        var read = FactualPackageReader.Read(run.PackageDirectory);
        Assert.NotEqual(FactualSnapshot.Empty, read.Snapshot);

        var certification = CanonicalJson.Read<RunCertificationEnvelope>(
            File.ReadAllBytes(Path.Combine(run.PackageDirectory, "run-certification.json")));
        Assert.True(
            certification.Status is "passed" or "degraded",
            $"run-certification status '{certification.Status}' is not a committed outcome.");
        var expectedExit = certification.Status == "passed" ? ExitCodes.Success : ExitCodes.Degraded;
        Assert.Equal(expectedExit, run.ExitCode);

        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(run.PackageDirectory, "manifest.json")));
        Assert.NotEmpty(manifest.Artifacts);
        foreach (var artifact in manifest.Artifacts)
        {
            var path = Path.Combine(run.PackageDirectory, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"manifest lists '{artifact.Path}' but the file is missing.");
        }

        var derivedCeiling = CeilingCalculator.Derive().CeilingBytes;
        Assert.NotNull(manifest.Provenance);
        Assert.Equal(derivedCeiling, manifest.Provenance.ArtifactCeilingBytes);
        var offenders = Directory.EnumerateFiles(run.PackageDirectory, "*", SearchOption.AllDirectories)
            .Select(file => new
            {
                Path = Path.GetRelativePath(run.PackageDirectory, file).Replace(Path.DirectorySeparatorChar, '/'),
                Bytes = new FileInfo(file).Length,
                FullPath = file,
            })
            .Where(file => file.Bytes > derivedCeiling)
            .ToArray();
        Assert.All(
            offenders,
            offender => Assert.True(
                IsSingleRecordShard(offender.FullPath),
                $"'{offender.Path}' ({offender.Bytes} bytes) exceeds the {derivedCeiling}-byte derived ceiling and is not the permitted indivisible single-record shard."));
    }

    private static bool IsSingleRecordShard(string path)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(path));
        return node switch
        {
            System.Text.Json.Nodes.JsonArray array => array.Count == 1,
            System.Text.Json.Nodes.JsonObject { Count: > 0 } obj
                when obj.All(static property => property.Value is System.Text.Json.Nodes.JsonArray) =>
                obj.Sum(static property => ((System.Text.Json.Nodes.JsonArray)property.Value!).Count) == 1,
            _ => false,
        };
    }

    private static string PackageDigest(string packageDirectory)
    {
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(packageDirectory, path).Replace(Path.DirectorySeparatorChar, '/'), StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
            incremental.AppendData(System.Text.Encoding.UTF8.GetBytes(relative));
            incremental.AppendData([0]);
            incremental.AppendData(File.ReadAllBytes(file));
            incremental.AppendData([0]);
        }

        return Convert.ToHexString(incremental.GetCurrentHash());
    }

    private sealed record PublishedRun(
        int ExitCode,
        string Stdout,
        string Stderr,
        string PackageDirectory,
        FactualSnapshot Snapshot);
}
