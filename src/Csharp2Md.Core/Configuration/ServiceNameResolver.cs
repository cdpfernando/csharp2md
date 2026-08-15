namespace Csharp2Md.Core.Configuration;

/// <summary>Static pure function over a <see cref="ConfigIndex"/> — no hidden state.</summary>
public static class ServiceNameResolver
{
    public static NameResolution Resolve(string logicalName, ConfigIndex index)
    {
        if (index.LogicalNames.TryGetValue(logicalName, out var value))
        {
            // P2-07 / P2-08: literal address -> HardCoded; env-var indirection -> Dynamic.
            return new NameResolution(
                logicalName, LooksLikeEnvVarReference(value) ? ResolutionKind.Dynamic : ResolutionKind.HardCoded);
        }

        if (index.DockerComposeServiceNames.Contains(logicalName))
        {
            // P2-08: service-discovery/registry lookup (Docker Compose's DNS-based resolution).
            return new NameResolution(logicalName, ResolutionKind.Dynamic);
        }

        // P2-09: no match against any known config source -> Unresolved, raw name preserved.
        return new NameResolution(logicalName, ResolutionKind.Unresolved);
    }

    // Common .NET/shell env-var interpolation shapes: "${VAR}", "$VAR", "%VAR%".
    private static bool LooksLikeEnvVarReference(string value) =>
        (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}'))
        || (value.StartsWith('%') && value.EndsWith('%') && value.Length > 2)
        || (value.StartsWith('$') && !value.StartsWith("${", StringComparison.Ordinal));
}
