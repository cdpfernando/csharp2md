using System.Security.Cryptography;
using System.Xml.Linq;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Inventory;

internal sealed record InventoriedDocuments(
    ImmutableArray<Document> Documents,
    ImmutableArray<Document> CSharpDocuments,
    ImmutableArray<Document> ConfigurationDocuments,
    ImmutableArray<DiagnosticRecord> Diagnostics,
    ImmutableArray<string> ExcludedRelativePaths,
    DocumentPolicyReport PolicyReport);

internal static class DocumentInventory
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    private static readonly HashSet<string> ProjectItemNames = new(StringComparer.Ordinal)
    {
        "Compile",
        "Content",
        "None",
        "EmbeddedResource",
        "AdditionalFiles",
        "Page",
        "Resource",
    };

    public static InventoriedDocuments Collect(
        string authorizedRoot,
        Project owningProject,
        string projectFilePath,
        IReadOnlyList<string>? listedProjectFilePaths = null,
        ImmutableArray<string> allowlistedRelativePaths = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedRoot);
        ArgumentNullException.ThrowIfNull(owningProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var root = Path.GetFullPath(authorizedRoot);
        var projectFullPath = Path.GetFullPath(projectFilePath);
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new ArgumentException($"'{projectFilePath}' has no containing directory.", nameof(projectFilePath));
        var listedDirectories = ListedProjectDirectories(listedProjectFilePaths, projectFullPath);

        var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var absolutePaths = new HashSet<string>(comparison);

        if (Directory.Exists(projectDirectory) && ContainsPath(root, projectDirectory))
        {
            foreach (var file in EnumerateProjectDirectoryFiles(projectDirectory, listedDirectories))
            {
                TryAddInventoriedPath(absolutePaths, root, file);
            }
        }

        if (File.Exists(projectFullPath))
        {
            foreach (var item in ReadExplicitProjectItems(projectFullPath, projectDirectory))
            {
                TryAddInventoriedPath(absolutePaths, root, item);
            }
        }

        var policy = new SupportedDocumentPolicy(new ClassifierCapabilityRegistry([]), allowlistedRelativePaths);

        var documents = ImmutableArray.CreateBuilder<Document>();
        var csharpDocuments = ImmutableArray.CreateBuilder<Document>();
        var configurationDocuments = ImmutableArray.CreateBuilder<Document>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticRecord>();
        var excludedRelativePaths = ImmutableArray.CreateBuilder<string>();
        var excludedExtensions = new SortedSet<string>(StringComparer.Ordinal);
        var outcomes = ImmutableArray.CreateBuilder<(DocumentPolicyCategory Category, bool Accepted, long Bytes)>();
        foreach (var absolute in absolutePaths.OrderBy(path => path, comparison))
        {
            var relative = ToRelativeDocumentPath(root, absolute);
            if (relative is null)
            {
                continue;
            }

            var decision = policy.Decide(relative);
            if (!decision.Accepted)
            {
                excludedRelativePaths.Add(relative);
                excludedExtensions.Add(ExtensionForReporting(relative));
                outcomes.Add((decision.Category, false, new FileInfo(absolute).Length));
                continue;
            }

            var bytes = File.ReadAllBytes(absolute);
            outcomes.Add((decision.Category, true, bytes.LongLength));
            var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var document = Document.Create(owningProject.Id, relative, DocumentHash.Create(digest));
            documents.Add(document);
            switch (decision.Category)
            {
                case DocumentPolicyCategory.CSharpSource:
                    csharpDocuments.Add(document);
                    break;
                case DocumentPolicyCategory.Configuration:
                    configurationDocuments.Add(document);
                    break;
            }
        }

        if (excludedRelativePaths.Count > 0)
        {
            diagnostics.Add(new DiagnosticRecord(
                "unsupported-document",
                $"{excludedRelativePaths.Count} document(s) excluded by the supported-document policy: "
                    + string.Join(", ", excludedExtensions) + ".",
                null));
        }

        return new InventoriedDocuments(
            documents.ToImmutable(),
            csharpDocuments.ToImmutable(),
            configurationDocuments.ToImmutable(),
            diagnostics.ToImmutable(),
            excludedRelativePaths.ToImmutable(),
            DocumentPolicyReport.FromOutcomes(outcomes));
    }

    private static string ExtensionForReporting(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return extension.Length > 0 ? extension.ToLowerInvariant() : Path.GetFileName(relativePath);
    }

    private static void TryAddInventoriedPath(HashSet<string> absolutePaths, string root, string candidate)
    {
        if (!File.Exists(candidate) || HasExcludedSegment(candidate) || !ContainsPath(root, candidate))
        {
            return;
        }

        absolutePaths.Add(Path.GetFullPath(candidate));
    }

    private static IEnumerable<string> EnumerateProjectDirectoryFiles(
        string projectDirectory,
        IReadOnlyList<string> listedProjectDirectories)
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
                if (ExcludedDirectoryNames.Contains(Path.GetFileName(child))
                    || IsNestedListedProjectDirectory(child, projectDirectory, listedProjectDirectories))
                {
                    continue;
                }

                pending.Push(child);
            }
        }
    }

    private static IReadOnlyList<string> ListedProjectDirectories(
        IReadOnlyList<string>? listedProjectFilePaths,
        string projectFilePath)
    {
        var paths = listedProjectFilePaths is { Count: > 0 } listed ? listed : [projectFilePath];
        var directories = new List<string>(paths.Count);
        foreach (var path in paths)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                directories.Add(Normalize(directory));
            }
        }

        return directories;
    }

    private static bool IsNestedListedProjectDirectory(
        string directory,
        string owningProjectDirectory,
        IReadOnlyList<string> listedProjectDirectories)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var owning = Normalize(owningProjectDirectory);
        var candidate = Normalize(directory);
        var owningPrefix = owning + Path.DirectorySeparatorChar;
        foreach (var listed in listedProjectDirectories)
        {
            if (listed.Equals(owning, comparison)
                || !listed.Equals(candidate, comparison)
                || !listed.StartsWith(owningPrefix, comparison))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static IEnumerable<string> ReadExplicitProjectItems(string projectFilePath, string projectDirectory)
    {
        var document = XDocument.Load(projectFilePath);
        foreach (var element in document.Descendants())
        {
            if (!ProjectItemNames.Contains(element.Name.LocalName))
            {
                continue;
            }

            var include = element.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include)
                || include.Contains('*', StringComparison.Ordinal)
                || include.Contains('$', StringComparison.Ordinal))
            {
                continue;
            }

            yield return Path.GetFullPath(Path.Combine(projectDirectory, include));
        }
    }

    private static bool HasExcludedSegment(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(ExcludedDirectoryNames.Contains);
    }

    private static string? ToRelativeDocumentPath(string root, string absolute)
    {
        var relative = Path.GetRelativePath(root, absolute).Replace('\\', '/');
        if (Path.IsPathRooted(relative)
            || relative.Length >= 2 && char.IsAsciiLetter(relative[0]) && relative[1] == ':')
        {
            return null;
        }

        var segments = relative.Split('/');
        if (segments.Any(static segment => segment is "" or "." or ".."))
        {
            return null;
        }

        return relative;
    }

    private static bool ContainsPath(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var normalizedRoot = Normalize(root);
        var normalizedCandidate = Normalize(candidate);
        if (normalizedCandidate.Equals(normalizedRoot, comparison))
        {
            return true;
        }

        return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
