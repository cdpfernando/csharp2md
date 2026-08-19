namespace Csharp2Md.Core.Topic;

/// <summary>
/// Single source of truth for where anything lives under the output root in the LLMWiki topic
/// layout (WIKI-01, WIKI-02, WIKI-03). A pure path calculator — no filesystem access — so the
/// layout can be asserted without running a pipeline.
/// </summary>
public static class TopicLayout
{
    private const string RawDirectoryName = "raw";
    private const string CodebaseDirectoryName = "codebase";

    /// <summary>Everything Phase 1 generates: <c>&lt;outputRoot&gt;/raw</c>.</summary>
    public static string RawRoot(string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);
        return Path.Combine(outputRoot, RawDirectoryName);
    }

    /// <summary>Source-derived documents mirror the input tree here: <c>&lt;outputRoot&gt;/raw/codebase</c>.</summary>
    public static string CodebaseRoot(string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);
        return Path.Combine(RawRoot(outputRoot), CodebaseDirectoryName);
    }

    /// <summary>A service's root within the codebase tree: <c>&lt;outputRoot&gt;/raw/codebase/&lt;service&gt;</c>.</summary>
    public static string ServiceRoot(string outputRoot, ServiceName service) =>
        Path.Combine(CodebaseRoot(outputRoot), service.Value);
}
