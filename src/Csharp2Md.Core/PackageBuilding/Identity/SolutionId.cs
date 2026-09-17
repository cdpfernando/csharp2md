using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Csharp2Md.Core.PackageBuilding.Identity;

[JsonConverter(typeof(SolutionIdJsonConverter))]
internal readonly record struct SolutionId
{
    private static readonly Regex Pattern = new("^sol_[0-9a-v]{16}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public string Value { get; }

    public SolutionId(string value)
    {
        if (value is null || !Pattern.IsMatch(value))
        {
            throw new ArgumentException("A solution ID must match ^sol_[0-9a-v]{16}$.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}

internal sealed class SolutionIdJsonConverter : JsonConverter<SolutionId>
{
    public override SolutionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("A solution ID must be a string."));

    public override void Write(Utf8JsonWriter writer, SolutionId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
