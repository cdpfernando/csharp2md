using System.Text;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Wire;

/// <summary>
/// GCPC-001, GCPC-002, GCPC-004, GCPC-009: the v2 envelope shapes carrying evaluation state, per-reason
/// counts, the closed status vocabulary (<c>not_evaluated</c> has left it) and the <c>not_applicable</c>
/// metric form.
/// </summary>
public sealed class CoverageAndCertificationEnvelopeTests
{
    [Fact]
    [Trait("Requirement", "GCPC-001")]
    public void RunCertificationEnvelope_NotEvaluated_CannotBeConstructed()
    {
        Assert.Throws<ArgumentException>(() => new RunCertificationEnvelope("not_evaluated", []));
    }

    [Fact]
    [Trait("Requirement", "GCPC-001")]
    public void RunCertificationEnvelope_NotEvaluated_CannotBeDeserialized()
    {
        var oldStatusJson = Encoding.UTF8.GetBytes("{\"status\":\"not_evaluated\",\"reasons\":[]}");

        Assert.ThrowsAny<Exception>(() => CanonicalJson.Read<RunCertificationEnvelope>(oldStatusJson));
    }

    [Theory]
    [InlineData("passed")]
    [InlineData("degraded")]
    [InlineData("failed")]
    [Trait("Requirement", "GCPC-001")]
    public void RunCertificationEnvelope_RoundTrips_ForEveryAllowedStatus(string status)
    {
        var envelope = new RunCertificationEnvelope(status, ["a stated reason"]);

        var bytes = CanonicalJson.Write(envelope);
        var restored = CanonicalJson.Read<RunCertificationEnvelope>(bytes.AsSpan());

        // ImmutableArray<T>'s default equality is reference-based, so a non-empty array is compared
        // element-wise rather than via whole-record Assert.Equal (which the codebase avoids for the
        // same reason elsewhere, e.g. DiagnosticsEnvelopeMappingTests).
        Assert.Equal(status, restored.Status);
        Assert.Equal("a stated reason", Assert.Single(restored.Reasons));
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    public void CoverageMetricDto_NotApplicable_RequiresAReason()
    {
        Assert.Throws<ArgumentException>(() => CoverageMetricDto.NotApplicable(""));
        Assert.Throws<ArgumentException>(() => CoverageMetricDto.NotApplicable(null!));
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    public void CoverageMetricDto_Evaluated_CannotCarryANotApplicableReason()
    {
        Assert.Throws<ArgumentException>(
            () => new CoverageMetricDto("evaluated", "unexpected reason", 1, 1, 0, 0, []));
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    public void CoverageMetricDto_UnknownState_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new CoverageMetricDto("satisfied", null, 1, 1, 0, 0, []));
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    public void CoverageMetricDto_RoundTrips_InTheNotApplicableForm()
    {
        var metric = CoverageMetricDto.NotApplicable("The solution declares no entry points.");

        var bytes = CanonicalJson.Write(metric);
        var restored = CanonicalJson.Read<CoverageMetricDto>(bytes.AsSpan());

        Assert.Equal("not_applicable", restored.State);
        Assert.Equal("The solution declares no entry points.", restored.NotApplicableReason);
        Assert.Equal(0, restored.Numerator);
        Assert.Equal(0, restored.Denominator);
        Assert.Equal(0, restored.Exclusions);
        Assert.Equal(0, restored.Unknowns);
    }

    [Fact]
    [Trait("Requirement", "GCPC-002")]
    [Trait("Requirement", "GCPC-004")]
    public void CoverageMetricDto_RoundTrips_InTheEvaluatedFormWithDegradationReasons()
    {
        var metric = CoverageMetricDto.Evaluated(
            numerator: 7,
            denominator: 10,
            exclusions: 1,
            unknowns: 2,
            degradationReasons:
            [
                new DegradationReasonDto(
                    "reflection-dispatch", "Reflection-based dispatch cannot be statically bound.", 2),
            ]);

        var bytes = CanonicalJson.Write(metric);
        var restored = CanonicalJson.Read<CoverageMetricDto>(bytes.AsSpan());

        Assert.Equal("evaluated", restored.State);
        Assert.Null(restored.NotApplicableReason);
        Assert.Equal(7, restored.Numerator);
        Assert.Equal(10, restored.Denominator);
        Assert.Equal(1, restored.Exclusions);
        Assert.Equal(2, restored.Unknowns);
        var reason = Assert.Single(restored.DegradationReasons);
        Assert.Equal("reflection-dispatch", reason.Code);
        Assert.Equal("Reflection-based dispatch cannot be statically bound.", reason.Detail);
        Assert.Equal(2, reason.AffectedCount);
    }

    [Fact]
    [Trait("Requirement", "GCPC-002")]
    public void CoverageEnvelope_RoundTrips_AcrossAllFourMetrics_MixingEvaluatedAndNotApplicable()
    {
        var envelope = new CoverageEnvelope(
            CoverageMetricDto.Evaluated(1, 2, 0, 1, []),
            CoverageMetricDto.NotApplicable("No in-solution invocations were recognized."),
            CoverageMetricDto.Evaluated(0, 0, 0, 0, []),
            CoverageMetricDto.NotApplicable("No data-access operations were recognized."));

        var bytes = CanonicalJson.Write(envelope);
        var restored = CanonicalJson.Read<CoverageEnvelope>(bytes.AsSpan());

        Assert.Equal("evaluated", restored.EntryPointCoverage.State);
        Assert.Equal("not_applicable", restored.LinkedCallCoverage.State);
        Assert.Equal("evaluated", restored.ContractCoverage.State);
        Assert.Equal("not_applicable", restored.PersistenceCoverage.State);
    }

    [Fact]
    [Trait("Requirement", "GCPC-004")]
    public void DegradationReasonDto_RequiresCodeDetailAndNonNegativeCount()
    {
        Assert.Throws<ArgumentException>(() => new DegradationReasonDto("", "detail", 0));
        Assert.Throws<ArgumentException>(() => new DegradationReasonDto("code", "", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DegradationReasonDto("code", "detail", -1));
    }

    [Fact]
    [Trait("Requirement", "GCPC-002")]
    public void CoverageMetricDto_OldShape_CannotBeDeserialized()
    {
        // Pre-v2 shape: no "state"/"not_applicable_reason"; degradation_reasons as bare strings.
        var oldShapeJson = Encoding.UTF8.GetBytes(
            "{\"numerator\":1,\"denominator\":2,\"exclusions\":0,\"unknowns\":0,\"degradation_reasons\":[]}");

        Assert.ThrowsAny<Exception>(() => CanonicalJson.Read<CoverageMetricDto>(oldShapeJson));
    }

    [Fact]
    [Trait("Requirement", "GCPC-003")]
    public void CoverageSchema_DeclaresTheV2ShapeAsRequired()
    {
        var schemaPath = Path.Combine(
            StorageTestPaths.RepoRoot, "contracts", "json-schema", "envelopes", "coverage.json");
        var schema = File.ReadAllText(schemaPath);

        Assert.Contains("\"state\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"not_applicable_reason\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"affected_count\"", schema, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-001")]
    public void RunCertificationSchema_DeclaresReasonsAsRequired()
    {
        var schemaPath = Path.Combine(
            StorageTestPaths.RepoRoot, "contracts", "json-schema", "envelopes", "run_certification.json");
        var schema = File.ReadAllText(schemaPath);

        Assert.Contains("\"reasons\"", schema, StringComparison.Ordinal);
    }
}
