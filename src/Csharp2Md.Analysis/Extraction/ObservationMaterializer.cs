using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Domain.Identity;
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

    public static ObservationDraft Redact(ObservationDraft draft, SnapshotAccumulator accumulator)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(accumulator);

        var kept = new List<PayloadEntry>();
        foreach (var entry in draft.Payload.Entries)
        {
            if (SecretRedactor.TryRedact(entry.Value.Value, out var excerpt))
            {
                Record(draft, excerpt, accumulator);
                continue;
            }

            kept.Add(entry);
        }

        var diagnostic = draft.Diagnostic;
        if (SecretRedactor.TryRedact(diagnostic.Message, out var diagnosticExcerpt))
        {
            Record(draft, diagnosticExcerpt, accumulator);
            diagnostic = new BindingDiagnostic(diagnostic.Code, diagnosticExcerpt.Value);
        }

        return draft with
        {
            Payload = NormalizedPayload.Create(kept),
            Diagnostic = diagnostic,
        };
    }

    private static void Record(
        ObservationDraft draft,
        RedactedExcerpt excerpt,
        SnapshotAccumulator accumulator) =>
        accumulator.AddSuspectedSecret(
            SuspectedSecretEvidence.Create(
                draft.Locator.Document,
                draft.Locator.Span,
                draft.DocumentHash,
                excerpt));
}
