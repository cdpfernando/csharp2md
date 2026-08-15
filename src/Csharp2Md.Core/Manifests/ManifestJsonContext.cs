using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Manifests;

[JsonSerializable(typeof(Manifest))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class ManifestJsonContext : JsonSerializerContext
{
}
