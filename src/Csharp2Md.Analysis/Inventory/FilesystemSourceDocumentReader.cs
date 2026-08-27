using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Inventory;

internal sealed class FilesystemSourceDocumentReader : ISourceDocumentReader
{
    private string _authorizedRoot = "";
    private ImmutableDictionary<DocumentId, string> _paths = ImmutableDictionary<DocumentId, string>.Empty;
    private ImmutableArray<DocumentId> _documents = [];

    public FilesystemSourceDocumentReader()
    {
    }

    public FilesystemSourceDocumentReader(string authorizedRoot, IReadOnlyDictionary<DocumentId, string> paths)
    {
        Load(authorizedRoot, paths);
    }

    public ImmutableArray<DocumentId> Documents => _documents;

    internal void Load(string authorizedRoot, IReadOnlyDictionary<DocumentId, string> paths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedRoot);
        ArgumentNullException.ThrowIfNull(paths);
        _authorizedRoot = Path.GetFullPath(authorizedRoot);
        _paths = paths.ToImmutableDictionary();
        _documents = _paths.Keys.OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray();
    }

    public bool TryRead(DocumentId document, out ImmutableArray<byte> bytes)
    {
        bytes = default;
        if (!_paths.TryGetValue(document, out var absolute) || !File.Exists(absolute))
        {
            return false;
        }

        try
        {
            PathGuard.RejectEscapes(_authorizedRoot, absolute);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        bytes = SharedFileRead.Read(absolute);
        return true;
    }
}
