using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Facts.Metadata;

public readonly record struct Evidence : IComparable<Evidence>
{
    public DocumentFactId DocumentId { get; }

    public string RelativePath { get; }

    public int StartLine { get; }

    public int StartColumn { get; }

    public int EndLine { get; }

    public int EndColumn { get; }

    public bool GeneratedOrigin { get; }

    public Evidence(
        DocumentFactId documentId,
        string relativePath,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        bool generatedOrigin = false)
    {
        RelativePath = ValidateRelativePath(relativePath);
        ValidatePosition(startLine, startColumn, nameof(startLine));
        ValidatePosition(endLine, endColumn, nameof(endLine));
        if ((endLine, endColumn).CompareTo((startLine, startColumn)) < 0)
        {
            throw new ArgumentException("The evidence end must not precede its start.", nameof(endLine));
        }

        DocumentId = documentId;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
        GeneratedOrigin = generatedOrigin;
    }

    public int CompareTo(Evidence other)
    {
        var documentComparison = StringComparer.Ordinal.Compare(DocumentId.Value, other.DocumentId.Value);
        if (documentComparison != 0)
        {
            return documentComparison;
        }

        var pathComparison = StringComparer.Ordinal.Compare(RelativePath, other.RelativePath);
        if (pathComparison != 0)
        {
            return pathComparison;
        }

        return (StartLine, StartColumn, EndLine, EndColumn)
            .CompareTo((other.StartLine, other.StartColumn, other.EndLine, other.EndColumn));
    }

    private static string ValidateRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path[0] is '/' or '\\' ||
            path.Contains('\\', StringComparison.Ordinal) ||
            (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':') ||
            path.Split('/').Any(static segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Evidence paths must be normalized relative paths.", nameof(path));
        }

        return path;
    }

    private static void ValidatePosition(int line, int column, string parameterName)
    {
        if (line <= 0 || column <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Evidence coordinates must be one-based.");
        }
    }
}
