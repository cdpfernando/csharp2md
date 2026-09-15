using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class EmptySnapshotMappingTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    [Trait("Requirement", "STOR-06")]
    public void ToWire_EmptySnapshot_HasNoPayloadRecords()
    {
        var document = DomainMapper.ToWire(FactualSnapshot.Empty, Context);

        Assert.True(document.Solutions.IsEmpty);
        Assert.True(document.Projects.IsEmpty);
        Assert.True(document.Documents.IsEmpty);
        Assert.True(document.Symbols.IsEmpty);
        Assert.True(document.Components.IsEmpty);
        Assert.True(document.DeploymentUnits.IsEmpty);
        Assert.True(document.EntryPoints.IsEmpty);
        Assert.True(document.BoundaryOperations.IsEmpty);
        Assert.True(document.ExternalSystems.IsEmpty);
        Assert.True(document.Contracts.IsEmpty);
        Assert.True(document.ContractBindings.IsEmpty);
        Assert.True(document.ContractRevisions.IsEmpty);
        Assert.True(document.DataStores.IsEmpty);
        Assert.True(document.DataObjects.IsEmpty);
        Assert.True(document.DataFields.IsEmpty);
        Assert.True(document.DataOperations.IsEmpty);
        Assert.True(document.ConfigurationBindings.IsEmpty);
        Assert.Empty(document.Observations);
        Assert.Empty(document.ConfirmedRelations);
        Assert.True(document.Candidates.IsEmpty);
        Assert.True(document.Unresolved.IsEmpty);
        Assert.True(document.Frontiers.IsEmpty);
        Assert.True(document.Quarantine.IsEmpty);
    }

    [Fact]
    [Trait("Requirement", "STOR-06")]
    public void ToWire_EmptySnapshot_DoesNotPopulateManifestArtifacts()
    {
        var document = DomainMapper.ToWire(FactualSnapshot.Empty, Context);

        Assert.True(document.Manifest.Artifacts.IsDefaultOrEmpty);
    }

    [Fact]
    [Trait("Requirement", "STOR-06")]
    public void ToWire_EmptySnapshot_WritesZeroCoverageAndNotEvaluatedCertification()
    {
        var document = DomainMapper.ToWire(FactualSnapshot.Empty, Context);

        AssertZero(document.Coverage.EntryPointCoverage);
        AssertZero(document.Coverage.LinkedCallCoverage);
        AssertZero(document.Coverage.ContractCoverage);
        AssertZero(document.Coverage.PersistenceCoverage);
        Assert.Equal("degraded", document.RunCertification.Status);
        Assert.True(document.Diagnostics.Records.IsEmpty);
        Assert.True(document.Measurements.Records.IsEmpty);
    }

    [Fact]
    [Trait("Requirement", "STOR-13")]
    public void FromWire_EmptyDocument_EqualsFactualSnapshotEmpty()
    {
        var document = DomainMapper.ToWire(FactualSnapshot.Empty, Context);

        var restored = DomainMapper.FromWire(document);

        Assert.Equal(FactualSnapshot.Empty, restored);
        Assert.True(restored.Facts.IsEmpty);
        Assert.True(restored.Observations.IsEmpty);
        Assert.True(restored.ConfirmedRelations.IsEmpty);
        Assert.True(restored.Candidates.IsEmpty);
        Assert.True(restored.Unresolved.IsEmpty);
        Assert.True(restored.Frontiers.IsEmpty);
    }

    private static void AssertZero(CoverageMetricDto metric)
    {
        Assert.Equal(0, metric.Numerator);
        Assert.Equal(0, metric.Denominator);
        Assert.Equal(0, metric.Exclusions);
        Assert.Equal(0, metric.Unknowns);
        Assert.True(metric.DegradationReasons.IsEmpty);
    }
}
