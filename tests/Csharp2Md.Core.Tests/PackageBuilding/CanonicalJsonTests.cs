using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class CanonicalJsonTests
{
    [Fact]
    [Trait("Requirement", "STO-07")]
    public void Write_OmitsUtf8Bom()
    {
        var bytes = CanonicalJson.Write(SampleMeasurements());

        Assert.False(bytes.IsDefaultOrEmpty);
        Assert.NotEqual((byte)0xEF, bytes[0]);
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    public void Write_UsesLineFeedNewlinesOnly()
    {
        var bytes = CanonicalJson.Write(SampleManifest());

        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Contains((byte)'\n', bytes);
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    public void Read_RoundTripsRegisteredContractValues()
    {
        var original = SampleMeasurements();

        var restored = CanonicalJson.Read<ExtractionMeasurements>(CanonicalJson.Write(original).AsSpan());

        Assert.Equal(original, restored);
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    [Trait("Requirement", "PUB-01")]
    public void Write_SamePreparedModel_ProducesIdenticalBytes()
    {
        var first = CanonicalJson.Write(SampleManifest());
        var second = CanonicalJson.Write(SampleManifest());
        var firstJson = Encoding.UTF8.GetString(first.AsSpan());
        var secondJson = Encoding.UTF8.GetString(second.AsSpan());

        Assert.Equal(firstJson, secondJson);
    }

    [Fact]
    [Trait("Requirement", "PUB-01")]
    public void CoreJsonContext_RegistersOnlyCurrentContractTypes()
    {
        var registered = typeof(CoreJsonContext)
            .GetCustomAttributesData()
            .Where(data => data.AttributeType == typeof(JsonSerializableAttribute))
            .Select(data => (Type)data.ConstructorArguments[0].Value!)
            .ToArray();

        Assert.NotEmpty(registered);
        Assert.All(
            registered,
            type => Assert.True(
                type.Namespace is not null && type.Namespace.StartsWith("Csharp2Md.Core", StringComparison.Ordinal)
                    || type.IsGenericType,
                $"Registered type '{type.FullName}' is not a current Core contract."));
        Assert.DoesNotContain(registered, type => type.Name.EndsWith("Dto", StringComparison.Ordinal));
        Assert.DoesNotContain(registered, type => type.Name is "FactualGraph" or "LogicalEntity" or "VariantOccurrence");
        Assert.Null(CoreJsonContext.Default.GetTypeInfo(typeof(FactualGraph)));
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    public void Write_UnregisteredType_ThrowsNotSupportedException()
    {
        var exception = Assert.Throws<NotSupportedException>(() => CanonicalJson.Write(new UnregisteredContract("x")));
        Assert.Contains(nameof(CoreJsonContext), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    public void Read_RejectsUnknownProperty()
    {
        var json = """{"extracted_count":1,"filtered_count":0,"unknown_field":true}"""u8.ToArray();

        var exception = Assert.Throws<JsonException>(() => CanonicalJson.Read<ExtractionMeasurements>(json));
        Assert.Contains("unknown_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    public void Write_UsesSnakeCasePropertyNamesFromSourceGeneratedContext()
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Write(SampleManifest()).AsSpan());

        Assert.Contains("\"token_estimator\"", json, StringComparison.Ordinal);
        Assert.Contains("\"token_divisor\"", json, StringComparison.Ordinal);
        Assert.Contains("\"include_tests\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"TokenEstimator\"", json, StringComparison.Ordinal);
    }

    private static ExtractionMeasurements SampleMeasurements() => new(3, 1);

    private static PackageManifest SampleManifest() =>
        new(
            PackageManifest.TokenEstimatorName,
            PackageManifest.TokenDivisorValue,
            includeTests: false,
            ImmutableArray.Create(new SolutionManifestEntry(
                new SolutionId("sol_0123456789abcdef"),
                "src/Acme.sln",
                new RootsManifestEntry("indexes/roots.json", 1),
                Enum.GetValues<NavigationIndexKind>()
                    .Select(kind => new IndexManifestEntry(kind, $"indexes/{kind.ToString().ToLowerInvariant()}.json"))
                    .ToImmutableArray(),
                ImmutableArray.Create(
                    new JourneyManifestEntry(JourneyKind.Locate, NavigationIndexKind.Roots),
                    new JourneyManifestEntry(JourneyKind.FollowFlow, NavigationIndexKind.Outgoing),
                    new JourneyManifestEntry(JourneyKind.ReverseImpact, NavigationIndexKind.Incoming),
                    new JourneyManifestEntry(JourneyKind.EvidenceDisposition, NavigationIndexKind.Evidence)))));

    private sealed record UnregisteredContract(string Name);
}
