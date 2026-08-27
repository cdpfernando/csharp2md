using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Literals;

public readonly record struct DeclarationLocator
{
    public DocumentId Document { get; }

    public string RelativePath { get; }

    public SourceSpan Span { get; }

    public DocumentHash Hash { get; }

    public DeclarationLocator(DocumentId document, string relativePath, SourceSpan span, DocumentHash hash)
    {
        if (document.Equals(default(DocumentId)))
        {
            throw new ArgumentException("A declaration locator requires an initialized document.", nameof(document));
        }

        if (hash.Equals(default(DocumentHash)))
        {
            throw new ArgumentException("A declaration locator requires an initialized hash.", nameof(hash));
        }

        Document = document;
        RelativePath = FactIdGrammar.ValidateRelativePath(relativePath, nameof(relativePath));
        Span = span;
        Hash = hash;
    }
}
