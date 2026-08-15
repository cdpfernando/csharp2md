namespace Csharp2Md.Core.Manifests;

/// <summary>
/// Domain-specific result of loading a manifest — not a generic <c>Result&lt;T, TError&gt;</c>
/// (per <c>dotnet-skills:csharp-coding-standards</c>: "don't build a generic Result&lt;T&gt; —
/// each operation knows what success and failure look like"). design.md's original sketch used a
/// generic signature; this deviates deliberately, per CLAUDE.md's authoring-style precedence.
/// </summary>
public sealed record ManifestLoadResult
{
    public bool IsSuccess { get; private init; }
    public Manifest? Manifest { get; private init; }
    public ManifestError? Error { get; private init; }

    public static ManifestLoadResult Success(Manifest manifest) => new()
    {
        IsSuccess = true,
        Manifest = manifest,
    };

    public static ManifestLoadResult Failed(ManifestError error) => new()
    {
        IsSuccess = false,
        Error = error,
    };
}
