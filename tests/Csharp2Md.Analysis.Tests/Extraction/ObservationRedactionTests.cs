using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ObservationRedactionTests
{
    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public void Redact_PasswordEqualsSecretLiteral_OmitsSecretFromObservationAndRecordsMaskedEvidence()
    {
        const string secret = "secret";
        var accumulator = new SnapshotAccumulator();
        var draft = CreateDraft(
            NormalizedPayload.Create(
            [
                new PayloadEntry(
                    "value",
                    StructuralLiteral.Create(LiteralRole.ConfigurationKey, "Password=" + secret, "value")),
            ]),
            new BindingDiagnostic("bound", "bound"));

        var redacted = ObservationMaterializer.Redact(draft, accumulator);
        var observation = Assert.Single(OccurrenceOrdinalAssigner.Assign([redacted]));
        var evidence = Assert.Single(accumulator.ToSnapshot().SuspectedSecrets.ToArray());

        Assert.DoesNotContain(secret, observation.Identity.Payload.Entries.Select(entry => entry.Value.Value));
        Assert.DoesNotContain(secret, observation.Diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, observation.Identity.Owner.Id.Value, StringComparison.Ordinal);
        Assert.Empty(observation.Identity.Payload.Entries);
        Assert.True(
            evidence.Excerpt.Value.Contains("***", StringComparison.Ordinal)
            || evidence.Excerpt.Value.Contains("[REDACTED]", StringComparison.Ordinal),
            "A flagged excerpt must carry a visible redaction marker.");
        Assert.DoesNotContain(secret, evidence.Excerpt.Value, StringComparison.Ordinal);
        Assert.Equal(draft.Locator.Document, evidence.Document);
        Assert.Equal(draft.Locator.Span, evidence.Span);
        Assert.Equal(draft.DocumentHash, evidence.Hash);
        Assert.NotEqual(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))),
            evidence.Hash.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public void Redact_PasswordEqualsSecretDiagnostic_OmitsSecretFromDiagnosticMessage()
    {
        const string secret = "secret";
        var accumulator = new SnapshotAccumulator();
        var draft = CreateDraft(
            NormalizedPayload.Create([]),
            new BindingDiagnostic("unbound", "Password=" + secret));

        var redacted = ObservationMaterializer.Redact(draft, accumulator);
        var observation = Assert.Single(OccurrenceOrdinalAssigner.Assign([redacted]));

        Assert.DoesNotContain(secret, observation.Diagnostic.Message, StringComparison.Ordinal);
        Assert.NotEqual("Password=" + secret, observation.Diagnostic.Message);
        Assert.Single(accumulator.ToSnapshot().SuspectedSecrets.ToArray());
    }

    private static ObservationDraft CreateDraft(NormalizedPayload payload, BindingDiagnostic diagnostic) =>
        new(
            Solution.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx")).Reference,
            ObservationKind.Configuration,
            payload,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Orders/Program.cs", new SourceSpan(3, 1, 3, 16)),
            EvidenceMethod.Syntactic,
            diagnostic,
            DocumentHash.Create(new string('a', 64)));
}
