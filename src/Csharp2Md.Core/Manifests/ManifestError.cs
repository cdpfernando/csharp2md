namespace Csharp2Md.Core.Manifests;

public enum ManifestErrorCode
{
    FileMissing,
    MalformedJson,
    ZeroEntries,
}

public readonly record struct ManifestError(ManifestErrorCode Code, string Message);
