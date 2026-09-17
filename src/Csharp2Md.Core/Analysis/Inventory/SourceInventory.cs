using System.Security.Cryptography;

namespace Csharp2Md.Core.Analysis.Inventory;

internal enum SourceDocumentKind
{
    CSharpSource,
    ProjectFile,
    Configuration,
    Unsupported,
}

internal sealed record AnalysisPolicy(bool IncludeTests)
{
    public string PolicyIdentity { get; } = IncludeTests
        ? "analysis-policy/include-tests=true"
        : "analysis-policy/include-tests=false";
}

internal sealed record InventoriedSourceDocument(
    string RelativePath,
    SourceDocumentKind Kind,
    bool IsTest,
    string ContentDigest,
    long ByteLength);

internal sealed record SourceInventoryResult(
    ImmutableArray<InventoriedSourceDocument> Accepted,
    ImmutableArray<string> ExcludedRelativePaths,
    AnalysisPolicy Policy);

internal static class SourceInventory
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    public static SourceInventoryResult Collect(
        string authorizedRoot,
        string projectDirectory,
        AnalysisPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentNullException.ThrowIfNull(policy);

        var root = PathGuard.Normalize(authorizedRoot);
        var projectDir = PathGuard.Normalize(projectDirectory);
        if (!PathGuard.ContainsPath(root, projectDir))
        {
            throw new InvalidOperationException(
                $"The project directory '{projectDir}' escapes the authorized root '{root}'.");
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var absolutePaths = new SortedSet<string>(comparison);
        if (Directory.Exists(projectDir))
        {
            foreach (var file in EnumerateFiles(projectDir))
            {
                PathGuard.RejectEscapes(root, file);
                if (!PathGuard.ContainsPath(root, file) || HasExcludedSegment(file))
                {
                    continue;
                }

                absolutePaths.Add(Path.GetFullPath(file));
            }
        }

        var accepted = ImmutableArray.CreateBuilder<InventoriedSourceDocument>();
        var excluded = ImmutableArray.CreateBuilder<string>();
        foreach (var absolute in absolutePaths)
        {
            PathGuard.RejectEscapes(root, absolute);
            var relative = PathGuard.ToLogicalPath(root, absolute);
            if (!IsSafeLogicalRelative(relative))
            {
                excluded.Add(relative);
                continue;
            }

            var kind = Classify(relative);
            var isTest = LooksLikeTestDocument(relative);
            if (kind == SourceDocumentKind.Unsupported || (isTest && !policy.IncludeTests))
            {
                excluded.Add(relative);
                continue;
            }

            var bytes = File.ReadAllBytes(absolute);
            accepted.Add(new InventoriedSourceDocument(
                relative,
                kind,
                isTest,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                bytes.LongLength));
        }

        return new SourceInventoryResult(accepted.ToImmutable(), excluded.ToImmutable(), policy);
    }

    internal static SourceDocumentKind Classify(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(relativePath);

        if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return SourceDocumentKind.CSharpSource;
        }

        if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return SourceDocumentKind.ProjectFile;
        }

        if (fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return SourceDocumentKind.Configuration;
        }

        return SourceDocumentKind.Unsupported;
    }

    internal static bool LooksLikeTestDocument(string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment.Equals("test", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("tests", StringComparison.OrdinalIgnoreCase)
                || segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
                || segment.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase)
                || segment.EndsWith(".IntegrationTests", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a project's own logical path looks like a test project - the same segment convention
    /// <see cref="LooksLikeTestDocument"/> applies to a document's path, since a project path is a
    /// relative path with the same shape (e.g. <c>SistemaB.Testes/SistemaB.Testes.csproj</c>). Used to keep
    /// a test project's Component/DeploymentUnit roots and its outbound relations out of retention when
    /// tests are excluded, independent of whether any of its documents survived per-file inventory.
    /// </summary>
    internal static bool IsTestProject(Analysis.ProjectIdentity project) => LooksLikeTestDocument(project.LogicalRelativePath);

    private static IEnumerable<string> EnumerateFiles(string projectDirectory)
    {
        var pending = new Stack<string>();
        pending.Push(projectDirectory);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (ExcludedDirectoryNames.Contains(Path.GetFileName(child)))
                {
                    continue;
                }

                pending.Push(child);
            }
        }
    }

    private static bool HasExcludedSegment(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(ExcludedDirectoryNames.Contains);
    }

    private static bool IsSafeLogicalRelative(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)
            || relative[0] is '/' or '\\'
            || relative.Contains('\\', StringComparison.Ordinal)
            || (relative.Length >= 2 && char.IsAsciiLetter(relative[0]) && relative[1] == ':'))
        {
            return false;
        }

        var segments = relative.Split('/');
        return !segments.Any(static segment => segment is "" or "." or "..");
    }
}
