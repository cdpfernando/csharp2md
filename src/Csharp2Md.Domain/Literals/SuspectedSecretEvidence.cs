namespace Csharp2Md.Domain.Literals;

/// <summary>
/// An excerpt is accepted only when it carries a visible redaction marker (<c>***</c> or
/// <c>[REDACTED]</c>). This is a shape check, not a secret-detection algorithm: it rejects text
/// that carries no redaction marker at all, which is the narrow contract this type owns. Deciding
/// what to redact from a source span is workstream 4's job.
/// </summary>
public readonly record struct RedactedExcerpt
{
    private const string MaskMarker = "***";
    private const string RedactedMarker = "[REDACTED]";

    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized redacted excerpt has no value.");

    public static RedactedExcerpt Create(string excerpt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(excerpt, nameof(excerpt));
        if (!excerpt.Contains(MaskMarker, StringComparison.Ordinal) && !excerpt.Contains(RedactedMarker, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"An excerpt must carry a redaction marker ('{MaskMarker}' or '{RedactedMarker}') to be treated as redacted.",
                nameof(excerpt));
        }

        return new RedactedExcerpt(excerpt);
    }

    private RedactedExcerpt(string value) => _value = value;

    public override string ToString() => Value;
}

public readonly record struct SuspectedSecretEvidence
{
    public DocumentId Document { get; }

    public SourceSpan Span { get; }

    public DocumentHash Hash { get; }

    public RedactedExcerpt Excerpt { get; }

    private SuspectedSecretEvidence(DocumentId document, SourceSpan span, DocumentHash hash, RedactedExcerpt excerpt)
    {
        Document = document;
        Span = span;
        Hash = hash;
        Excerpt = excerpt;
    }

    public static SuspectedSecretEvidence Create(DocumentId document, SourceSpan span, DocumentHash hash, RedactedExcerpt excerpt) =>
        new(document, span, hash, excerpt);
}
