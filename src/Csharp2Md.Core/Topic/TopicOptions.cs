using System.Text.RegularExpressions;

namespace Csharp2Md.Core.Topic;

/// <summary>
/// The validated <c>topic</c>/<c>domain</c> pair every frontmatter block and <c>topic.yaml</c>
/// carries (WIKI-14, WIKI-15, WIKI-16). Explicit-property form, not a validating primary
/// constructor — the <c>dotnet-skills:csharp-coding-standards</c> value-object snippet's
/// primary-constructor form does not compile (CS0111), per <c>ServiceName.cs</c>'s own precedent.
/// </summary>
public sealed record TopicOptions
{
    public const string DefaultDomain = "system-design";

    public string Topic { get; }
    public string Domain { get; }

    private TopicOptions(string topic, string domain)
    {
        Topic = topic;
        Domain = domain;
    }

    /// <summary>Slug of the input directory name, with the default domain (WIKI-15, WIKI-16).</summary>
    public static TopicOptions Default(string inputRoot) =>
        new(Slugify(InputDirectoryName(inputRoot)), DefaultDomain);

    /// <summary>
    /// Applies defaults for omitted values and validates the resolved topic slug (WIKI-14). Returns
    /// an error result rather than throwing — the same "expected error, not exception" convention
    /// <c>ManifestLoader</c> uses.
    /// </summary>
    public static TopicOptionsResult Create(string? topic, string? domain, string inputRoot)
    {
        var resolvedTopic = topic ?? Slugify(InputDirectoryName(inputRoot));
        var resolvedDomain = domain ?? DefaultDomain;

        if (!TopicSlugPatterns.Slug().IsMatch(resolvedTopic))
        {
            return TopicOptionsResult.Failed(
                $"Topic '{resolvedTopic}' is not a valid slug: expected lowercase alphanumeric " +
                "segments separated by '-' or '/', matching " +
                "^[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$.");
        }

        return TopicOptionsResult.Success(new TopicOptions(resolvedTopic, resolvedDomain));
    }

    /// <summary>Lowercases, collapses non-alphanumeric runs to a single '-', trims leading/trailing '-'.</summary>
    public static string Slugify(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var collapsed = TopicSlugPatterns.NonAlphanumericRun().Replace(name.ToLowerInvariant(), "-");
        return collapsed.Trim('-');
    }

    private static string InputDirectoryName(string inputRoot)
    {
        var trimmed = inputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }
}

/// <summary>
/// Domain-specific result of validating <see cref="TopicOptions"/> — not a generic
/// <c>Result&lt;T, TError&gt;</c> (per <c>dotnet-skills:csharp-coding-standards</c>: "don't build a
/// generic Result&lt;T&gt; — each operation knows what success and failure look like"), matching
/// <c>ManifestLoadResult</c>'s precedent.
/// </summary>
public sealed record TopicOptionsResult
{
    public bool IsSuccess { get; private init; }
    public TopicOptions? Options { get; private init; }
    public string? Error { get; private init; }

    public static TopicOptionsResult Success(TopicOptions options) => new() { IsSuccess = true, Options = options };

    public static TopicOptionsResult Failed(string error) => new() { IsSuccess = false, Error = error };
}

internal static partial class TopicSlugPatterns
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$")]
    public static partial Regex Slug();

    [GeneratedRegex("[^a-z0-9]+")]
    public static partial Regex NonAlphanumericRun();
}
