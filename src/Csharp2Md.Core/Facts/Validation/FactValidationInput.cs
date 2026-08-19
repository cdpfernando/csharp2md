using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Facts.Validation;

internal sealed record DocumentExtent
{
    public DocumentFactId DocumentId { get; }

    public string RelativePath { get; }

    public ImmutableArray<int> LineLengths { get; }

    private DocumentExtent(DocumentFactId documentId, string relativePath, ImmutableArray<int> lineLengths)
    {
        DocumentId = documentId;
        RelativePath = relativePath;
        LineLengths = lineLengths;
    }

    public static DocumentExtent Create(DocumentFactId documentId, string relativePath, IEnumerable<int> lineLengths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(lineLengths);
        var lengths = lineLengths.ToImmutableArray();
        if (lengths.IsEmpty || lengths.Any(static length => length < 0))
        {
            throw new ArgumentException("A document extent requires non-negative lengths for at least one line.", nameof(lineLengths));
        }

        return new DocumentExtent(documentId, relativePath, lengths);
    }
}

internal sealed record FactValidationInput(
    ImmutableArray<IFact> Facts,
    ImmutableArray<AnalysisDiagnostic> Diagnostics,
    ImmutableArray<DocumentExtent> Documents,
    ImmutableHashSet<FactId> KnownFactIds)
{
    public static FactValidationInput Create(
        IEnumerable<IFact> facts,
        IEnumerable<AnalysisDiagnostic>? diagnostics = null,
        IEnumerable<DocumentExtent>? documents = null,
        IEnumerable<FactId>? knownFactIds = null) =>
        new(
            facts.ToImmutableArray(),
            (diagnostics ?? []).ToImmutableArray(),
            (documents ?? []).ToImmutableArray(),
            (knownFactIds ?? []).ToImmutableHashSet());
}
