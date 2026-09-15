using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Tests.Shared;

/// <summary>
/// A reader over no source documents, for the store tests that open a session without staging source.
/// Linked only into the test projects that can see <see cref="ISourceDocumentReader"/>.
/// </summary>
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
