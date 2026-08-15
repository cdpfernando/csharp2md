namespace Csharp2Md.Core.Configuration;

/// <summary>
/// Logical-name index built from <c>appsettings*.json</c> and <c>docker-compose.yml</c> across all
/// discovered service roots (T9's <c>ServiceNameResolver</c> classifies hits against this).
/// </summary>
public sealed record ConfigIndex(
    IReadOnlyDictionary<string, string> LogicalNames,
    IReadOnlySet<string> DockerComposeServiceNames);

public sealed record ConfigIndexResult(ConfigIndex Index, IReadOnlyList<string> Warnings);
