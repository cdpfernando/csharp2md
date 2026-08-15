namespace Csharp2Md.Core.Output;

/// <summary>One service's entry in the root index (P1-14).</summary>
public sealed record ServiceIndexEntry(ServiceName Service, string IndexPath);

/// <summary>
/// Writes the per-service <c>index.md</c> (P1-13) and the root <c>index.md</c> (P1-14).
/// </summary>
/// <remarks>
/// Every link is relative, so the generated tree stays portable: browsable on GitHub, in an IDE, or
/// fed to an LLM without a server (spec Assumptions). Separators are normalized to <c>/</c> for the
/// same reason — a Windows-style link is dead on GitHub.
/// </remarks>
public static class IndexWriter
{
    public const string FileName = "index.md";

    /// <summary>
    /// P1-13: one <c>index.md</c> at the root of the service's output folder, linking every file
    /// generated for that service. That location is what
    /// <see cref="Rendering.MarkdownRenderer.IndexLink"/> points each document's backlink at.
    /// </summary>
    public static string WriteServiceIndex(
        string serviceOutputRoot,
        ServiceName service,
        IEnumerable<string> writtenPaths)
    {
        ArgumentNullException.ThrowIfNull(serviceOutputRoot);
        ArgumentNullException.ThrowIfNull(writtenPaths);

        var links = writtenPaths
            .Select(path => RelativeLink(serviceOutputRoot, path))
            .OrderBy(link => link, StringComparer.Ordinal)
            .Select(link => $"- [{SourceName(link)}]({link})");

        return WriteFile(serviceOutputRoot, $"# {service.Value}", links);
    }

    /// <summary>P1-14: one root <c>index.md</c> linking every per-service index.</summary>
    public static string WriteRootIndex(string outputRoot, IEnumerable<ServiceIndexEntry> services)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);
        ArgumentNullException.ThrowIfNull(services);

        var links = services
            .Select(entry => (entry.Service, Link: RelativeLink(outputRoot, entry.IndexPath)))
            .OrderBy(entry => entry.Service.Value, StringComparer.Ordinal)
            .Select(entry => $"- [{entry.Service.Value}]({entry.Link})");

        return WriteFile(outputRoot, "# Services", links);
    }

    private static string WriteFile(string root, string heading, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, FileName);

        File.WriteAllText(path, $"{heading}\n\n{string.Join('\n', lines)}\n");

        return path;
    }

    private static string RelativeLink(string fromDirectory, string targetPath) =>
        "./" + Path.GetRelativePath(fromDirectory, targetPath).Replace('\\', '/');

    /// <summary>The source file the link points at: <c>./Foo.cs.md</c> reads as <c>Foo.cs</c>.</summary>
    private static string SourceName(string link)
    {
        var withoutPrefix = link["./".Length..];

        return withoutPrefix.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? withoutPrefix[..^".md".Length]
            : withoutPrefix;
    }
}
