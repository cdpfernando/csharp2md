using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Microsoft.CodeAnalysis;
using DomainDocument = Csharp2Md.Domain.Facts.Document;
using DomainDocumentId = Csharp2Md.Domain.Literals.DocumentId;

namespace Csharp2Md.Analysis.Extraction;

internal static class ObservationMaterializer
{
    public static ExtractorVersion Version { get; } = new(1);

    public static EvidenceLocator CreateLocator(DomainDocument document, SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);

        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var start = lineSpan.StartLinePosition;
        var end = lineSpan.EndLinePosition;
        var span = new SourceSpan(
            start.Line + 1,
            start.Character + 1,
            Math.Max(end.Line + 1, start.Line + 1),
            Math.Max(end.Character + 1, 1));
        return new EvidenceLocator(
            DomainDocumentId.Create(document.Reference.Id.Value),
            document.RelativePath,
            span);
    }

    public static DocumentHash HashFileBytes(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        var digest = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(absolutePath)));
        return DocumentHash.Create(digest);
    }
}
