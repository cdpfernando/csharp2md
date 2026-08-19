using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Facts.Serialization;

[JsonSerializable(typeof(FactualJsonDocument))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
public partial class FactualJsonContext : JsonSerializerContext;
