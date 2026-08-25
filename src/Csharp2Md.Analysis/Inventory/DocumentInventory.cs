using System.Xml.Linq;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;

namespace Csharp2Md.Analysis.Inventory;

internal sealed record InventoriedDocuments(
    ImmutableArray<Document> Documents,
    ImmutableArray<Document> CSharpDocuments,
    ImmutableArray<DiagnosticRecord> Diagnostics);

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
        string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedRoot);
        ArgumentNullException.ThrowIfNull(owningProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var root = Path.GetFullPath(authorizedRoot);
        var projectFullPath = Path.GetFullPath(projectFilePath);
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new ArgumentException($"'{projectFilePath}' has no containing directory.", nameof(projectFilePath));

        var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var absolutePaths = new HashSet<string>(comparison);

        if (Directory.Exists(projectDirectory) && ContainsPath(root, projectDirectory))
        {
            foreach (var file in EnumerateProjectDirectoryFiles(projectDirectory))
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

        var documents = ImmutableArray.CreateBuilder<Document>();
        var csharpDocuments = ImmutableArray.CreateBuilder<Document>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticRecord>();
        foreach (var absolute in absolutePaths.OrderBy(path => path, comparison))
        {
            var relative = ToRelativeDocumentPath(root, absolute);
            if (relative is null)
            {
                continue;
            }

            var document = Document.Create(owningProject.Id, relative);
            documents.Add(document);
            if (IsCSharpDocument(relative))
            {
                csharpDocuments.Add(document);
                continue;
            }

            diagnostics.Add(new DiagnosticRecord(
                "unsupported-document",
                "The document is not C# and will not be extracted.",
                relative));
        }

        return new InventoriedDocuments(
            documents.ToImmutable(),
            csharpDocuments.ToImmutable(),
            diagnostics.ToImmutable());
    }

    private static bool IsCSharpDocument(string relativePath) =>
        relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    private static void TryAddInventoriedPath(HashSet<string> absolutePaths, string root, string candidate)
    {
        if (!File.Exists(candidate) || HasExcludedSegment(candidate) || !ContainsPath(root, candidate))
        {
            return;
        }

        absolutePaths.Add(Path.GetFullPath(candidate));
    }

    private static IEnumerable<string> EnumerateProjectDirectoryFiles(string projectDirectory)
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
