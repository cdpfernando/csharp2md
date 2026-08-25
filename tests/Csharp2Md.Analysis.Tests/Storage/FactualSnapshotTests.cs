using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class FactualSnapshotTests
{
    [Fact]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "ROSE-58")]
    public void Empty_HasLengthZeroArraysForEveryFamily()
    {
        var empty = FactualSnapshot.Empty;

        Assert.False(empty.Facts.IsDefault);
        Assert.Empty(empty.Facts);
        Assert.False(empty.Observations.IsDefault);
        Assert.Empty(empty.Observations);
        Assert.False(empty.ConfirmedRelations.IsDefault);
        Assert.Empty(empty.ConfirmedRelations);
        Assert.False(empty.Candidates.IsDefault);
        Assert.Empty(empty.Candidates);
        Assert.False(empty.Unresolved.IsDefault);
        Assert.Empty(empty.Unresolved);
        Assert.False(empty.Frontiers.IsDefault);
        Assert.Empty(empty.Frontiers);
        Assert.False(empty.Diagnostics.IsDefault);
        Assert.Empty(empty.Diagnostics);
        Assert.False(empty.SuspectedSecrets.IsDefault);
        Assert.Empty(empty.SuspectedSecrets);
    }

    [Fact]
    [Trait("Requirement", "STOR-10")]
    public void Merge_SharedSolutionIdentity_DoesNotThrowAndConcatenatesFacts()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln"));

        var left = SnapshotWithFact(solution);
        var right = SnapshotWithFact(solution);

        var merged = left.Merge(right);

        Assert.Equal(2, merged.Facts.Length);
        Assert.Equal(solution, merged.Facts[0]);
        Assert.Equal(solution, merged.Facts[1]);
        Assert.Empty(merged.Observations);
        Assert.Empty(merged.ConfirmedRelations);
        Assert.Empty(merged.Candidates);
        Assert.Empty(merged.Unresolved);
        Assert.Empty(merged.Frontiers);
        Assert.Empty(merged.Diagnostics);
        Assert.Empty(merged.SuspectedSecrets);
    }

    [Fact]
    [Trait("Requirement", "ROSE-58")]
    public void SixArgumentConstructor_StillCompilesAndLeavesDiagnosticsAndSecretsEmpty()
    {
        var snapshot = new FactualSnapshot(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty);

        Assert.Empty(snapshot.Diagnostics);
        Assert.Empty(snapshot.SuspectedSecrets);
    }

    [Fact]
    [Trait("Requirement", "ROSE-58")]
    public void Merge_ConcatenatesDiagnosticsAndSuspectedSecrets()
    {
        var leftDiagnostic = new DiagnosticRecord("missing-project", "Acme.DoesNotExist is absent.", "Acme.DoesNotExist/Acme.DoesNotExist.csproj");
        var rightDiagnostic = new DiagnosticRecord("compilation-error", "Acme.Broken produced error diagnostics.", "Acme.Broken/Acme.Broken.csproj");
        var leftSecret = Secret("src/Left.cs", "Password=***");
        var rightSecret = Secret("src/Right.cs", "token=[REDACTED]");

        var left = new FactualSnapshot(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty,
            ImmutableArray.Create(leftDiagnostic),
            ImmutableArray.Create(leftSecret));
        var right = new FactualSnapshot(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty,
            ImmutableArray.Create(rightDiagnostic),
            ImmutableArray.Create(rightSecret));

        var merged = left.Merge(right);

        Assert.Equal(2, merged.Diagnostics.Length);
        Assert.Equal(leftDiagnostic, merged.Diagnostics[0]);
        Assert.Equal(rightDiagnostic, merged.Diagnostics[1]);
        Assert.Equal(2, merged.SuspectedSecrets.Length);
        Assert.Equal(leftSecret, merged.SuspectedSecrets[0]);
        Assert.Equal(rightSecret, merged.SuspectedSecrets[1]);
    }

    private static FactualSnapshot SnapshotWithFact(IFact fact) =>
        new(
            ImmutableArray.Create(fact),
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty);

    private static SuspectedSecretEvidence Secret(string document, string excerpt) =>
        SuspectedSecretEvidence.Create(
            DocumentId.Create(document),
            new SourceSpan(1, 1, 1, 8),
            DocumentHash.Create(new string('a', 64)),
            RedactedExcerpt.Create(excerpt));
}
