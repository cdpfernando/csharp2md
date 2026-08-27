using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Storage;

public interface ISourceDocumentReader
{
    ImmutableArray<DocumentId> Documents { get; }

    bool TryRead(DocumentId document, out ImmutableArray<byte> bytes);
}
