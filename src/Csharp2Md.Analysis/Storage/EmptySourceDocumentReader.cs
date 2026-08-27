using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Storage;

internal sealed class EmptySourceDocumentReader : ISourceDocumentReader
{
    internal static readonly EmptySourceDocumentReader Instance = new();

    public ImmutableArray<DocumentId> Documents => [];

    public bool TryRead(DocumentId document, out ImmutableArray<byte> bytes)
    {
        bytes = default;
        return false;
    }
}
