using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class DiagnosticsEnvelopeMappingTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    [Trait("Requirement", "ROSE-58")]
    public void ToWire_CopiesSnapshotDiagnosticsFieldForFieldInStableOrder()
    {
        var later = new DiagnosticRecord("missing-project", "z-project is absent.", "z-project/z.csproj");
        var earlier = new DiagnosticRecord("compilation-error", "a-project produced errors.", "a-project/a.csproj");
        var snapshot = Snapshot(
            diagnostics: [later, earlier],
            secrets: []);

        var document = DomainMapper.ToWire(snapshot, Context);

        Assert.Equal(2, document.Diagnostics.Records.Length);
        Assert.Equal(earlier.Code, document.Diagnostics.Records[0].Code);
        Assert.Equal(earlier.Message, document.Diagnostics.Records[0].Message);
        Assert.Equal(earlier.IdentityOrKey, document.Diagnostics.Records[0].IdentityOrKey);
        Assert.Equal(later.Code, document.Diagnostics.Records[1].Code);
        Assert.Equal(later.Message, document.Diagnostics.Records[1].Message);
        Assert.Equal(later.IdentityOrKey, document.Diagnostics.Records[1].IdentityOrKey);
    }

    [Fact]
    [Trait("Requirement", "ROSE-58")]
    public void ToWire_FlattensSuspectedSecretsWithoutTheRawSecret()
    {
        const string rawSecret = "hunter2";
        var span = new SourceSpan(12, 4, 12, 28);
        var excerpt = RedactedExcerpt.Create("Password=***");
        var evidence = SuspectedSecretEvidence.Create(
            DocumentId.Create("src/Acme.Orders/Program.cs"),
            span,
            DocumentHash.Create(new string('a', 64)),
            excerpt);
        var snapshot = Snapshot(diagnostics: [], secrets: [evidence]);

        var document = DomainMapper.ToWire(snapshot, Context);
        var record = Assert.Single(document.Diagnostics.Records);
        var json = Encoding.UTF8.GetString(CanonicalJson.Write(document.Diagnostics).AsSpan());

        Assert.Equal("suspected-secret", record.Code);
        Assert.Equal("src/Acme.Orders/Program.cs", record.IdentityOrKey);
        Assert.Contains("12", record.Message, StringComparison.Ordinal);
        Assert.Contains("4", record.Message, StringComparison.Ordinal);
        Assert.Contains("28", record.Message, StringComparison.Ordinal);
        Assert.Contains(excerpt.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains("***", record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(rawSecret, record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(rawSecret, record.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(rawSecret, record.IdentityOrKey, StringComparison.Ordinal);
        Assert.DoesNotContain(rawSecret, json, StringComparison.Ordinal);
        Assert.Contains("***", json, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-58")]
    public void ToWire_EmptySnapshot_StillHasEmptyDiagnostics()
    {
        var document = DomainMapper.ToWire(FactualSnapshot.Empty, Context);

        Assert.True(document.Diagnostics.Records.IsEmpty);
    }

    private static FactualSnapshot Snapshot(
        ImmutableArray<DiagnosticRecord> diagnostics,
        ImmutableArray<SuspectedSecretEvidence> secrets) =>
        new(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty,
            diagnostics,
            secrets);
}
