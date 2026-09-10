using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Wire;

/// <summary>GCPC-056..GCPC-060: provenance names the running build, all five version axes, and the
/// deterministic parameters that shaped the package -- never a timestamp.</summary>
public sealed class ProvenanceDtoTests
{
    [Fact]
    public void Current_CalledTwice_IsByteIdentical()
    {
        var first = ProvenanceDto.Current();
        var second = ProvenanceDto.Current();

        Assert.True(CanonicalJson.Write(first).AsSpan().SequenceEqual(CanonicalJson.Write(second).AsSpan()));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Current_NamesTheRunningGeneratorBuild()
    {
        var provenance = ProvenanceDto.Current();

        Assert.False(string.IsNullOrWhiteSpace(provenance.GeneratorVersion));
        Assert.False(string.IsNullOrWhiteSpace(provenance.BuildIdentity));
    }

    [Fact]
    public void Current_PublishesAllFiveVersionAxes()
    {
        var provenance = ProvenanceDto.Current();
        var versions = TaxonomyTables.Default.Versions;

        Assert.Equal(versions.SchemaVersion, provenance.SchemaVersion);
        Assert.Equal(versions.TaxonomyVersion, provenance.TaxonomyVersion);
        Assert.Equal(versions.ObservationSchemaVersion, provenance.ObservationSchemaVersion);
        Assert.Equal(versions.ExtractorSetVersion, provenance.ExtractorSetVersion);
        Assert.Equal(versions.ClassifierSetVersion, provenance.ClassifierSetVersion);
    }

    [Fact]
    public void Current_PublishesTheDerivedCeilingAndItsTokenEstimator()
    {
        var provenance = ProvenanceDto.Current();
        var ceiling = CeilingCalculator.Derive();

        Assert.Equal(ceiling.CeilingBytes, provenance.ArtifactCeilingBytes);
        Assert.Equal(ceiling.TokenEstimatorId, provenance.TokenEstimatorId);
    }

    [Fact]
    public void Current_PublishesTheDocumentPolicyVersionAndAllowlistDigest()
    {
        var provenance = ProvenanceDto.Current();

        Assert.False(string.IsNullOrWhiteSpace(provenance.DocumentPolicyVersion));
        Assert.False(string.IsNullOrWhiteSpace(provenance.AllowlistDigest));
    }

    [Fact]
    public void ComputeAllowlistDigest_SameEntriesInDifferentOrder_ProduceTheSameDigest()
    {
        var forward = ProvenanceDto.ComputeAllowlistDigest(["b.ts", "a.ts"]);
        var reversed = ProvenanceDto.ComputeAllowlistDigest(["a.ts", "b.ts"]);

        Assert.Equal(forward, reversed);
        Assert.NotEqual(ProvenanceDto.EmptyAllowlistDigest, forward);
    }

    [Fact]
    public void ProvenanceDto_HasNoTimestampOrDurationField()
    {
        var forbidden = new[] { "timestamp", "duration", "date", "time" };
        foreach (var property in typeof(ProvenanceDto).GetProperties())
        {
            var name = property.Name.ToLowerInvariant();
            Assert.DoesNotContain(forbidden, token => name.Contains(token, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ManifestEnvelope_HasNoTimestampOrDurationFieldOutsideMeasurements()
    {
        var forbidden = new[] { "timestamp", "duration" };
        foreach (var property in typeof(ManifestEnvelope).GetProperties())
        {
            var name = property.Name.ToLowerInvariant();
            Assert.DoesNotContain(forbidden, token => name.Contains(token, StringComparison.Ordinal));
        }

        // Timestamps and durations live only in the measurements envelope.
        var measurementProperties = typeof(MeasurementRecordDto).GetProperties().Select(static p => p.Name).ToArray();
        Assert.Contains("Timestamp", measurementProperties);
        Assert.Contains("DurationMilliseconds", measurementProperties);
    }
}
