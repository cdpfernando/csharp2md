using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Literals;

public readonly record struct EvidenceLocator : IComparable<EvidenceLocator>
{
    public DocumentId Document { get; }

    public string RelativePath { get; }

    public SourceSpan Span { get; }

    public EvidenceLocator(DocumentId document, string relativePath, SourceSpan span)
    {
        Document = document;
        RelativePath = FactIdGrammar.ValidateRelativePath(relativePath, nameof(relativePath));
        Span = span;
    }

    public int CompareTo(EvidenceLocator other)
    {
        var documentComparison = StringComparer.Ordinal.Compare(Document.Value, other.Document.Value);
        if (documentComparison != 0)
        {
            return documentComparison;
        }

        var pathComparison = StringComparer.Ordinal.Compare(RelativePath, other.RelativePath);
        if (pathComparison != 0)
        {
            return pathComparison;
        }

        return (Span.StartLine, Span.StartColumn, Span.EndLine, Span.EndColumn)
            .CompareTo((other.Span.StartLine, other.Span.StartColumn, other.Span.EndLine, other.Span.EndColumn));
    }
}
