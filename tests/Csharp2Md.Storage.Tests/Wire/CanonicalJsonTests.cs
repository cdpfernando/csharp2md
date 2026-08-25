using System.Text;
using System.Text.Json;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Wire;

public sealed class CanonicalJsonTests
{
    [Fact]
    [Trait("Requirement", "STOR-44")]
    public void Write_OmitsUtf8Bom()
    {
        var bytes = CanonicalJson.Write(SampleSolution());

        Assert.False(bytes.IsDefaultOrEmpty);
        Assert.NotEqual((byte)0xEF, bytes[0]);
    }

    [Fact]
    [Trait("Requirement", "STOR-44")]
    public void Write_UsesLineFeedNewlinesOnly()
    {
        var bytes = CanonicalJson.Write(SampleSolution());

        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Contains((byte)'\n', bytes);
    }

    [Fact]
    [Trait("Requirement", "STOR-44")]
    public void Write_IndentsWithTwoSpaces()
    {
        var bytes = CanonicalJson.Write(SampleSolution());
        var json = Encoding.UTF8.GetString(bytes.AsSpan());

        Assert.Contains("\n  \"identity\"", json, StringComparison.Ordinal);
        Assert.Contains("\n    \"id\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\t", json, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-44")]
    public void Read_RoundTripsDtoValues()
    {
        var original = SampleSolution();

        var restored = CanonicalJson.Read<SolutionDto>(CanonicalJson.Write(original).AsSpan());

        Assert.Equal(original, restored);
    }

    [Fact]
    [Trait("Requirement", "STOR-07")]
    public void StorageJsonContext_RegistersEveryPublicWireDto()
    {
        var unregistered = typeof(StorageJsonContext).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == "Csharp2Md.Storage.Wire")
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(type => type != typeof(StorageJsonContext))
            .Where(type => StorageJsonContext.Default.GetTypeInfo(type) is null)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unregistered.Length == 0,
            "StorageJsonContext is missing [JsonSerializable] for: " + string.Join(", ", unregistered));
    }

    [Fact]
    [Trait("Requirement", "STOR-07")]
    public void Write_UsesSnakeCasePropertyNamesFromSourceGeneratedContext()
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Write(SampleSolution()).AsSpan());

        Assert.Contains("\"solution_id\"", json, StringComparison.Ordinal);
        Assert.Contains("\"content_sha256\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fact_type\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SolutionId\"", json, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-07")]
    public void Read_RejectsUnknownProperty()
    {
        var json = """{"id":"s-1","fact_type":"Solution","unknown_field":true}"""u8.ToArray();

        var exception = Assert.Throws<JsonException>(() => CanonicalJson.Read<FactReferenceDto>(json));

        Assert.Contains("unknown_field", exception.Message, StringComparison.Ordinal);
    }

    private static SolutionDto SampleSolution() =>
        new(new FactReferenceDto("s-1", "Solution"), "Acme.sln", "abc");
}
