using System.Globalization;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Source;

internal static class SourceProjector
{
    public static ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(source);

        var documents = view.Document.Documents
            .OrderBy(static dto => dto.Identity.Id, StringComparer.Ordinal)
            .ToArray();
        if (documents.Length == 0)
        {
            return [];
        }

        var fragments = ImmutableArray.CreateBuilder<StagedFragment>(documents.Length);
        foreach (var document in documents)
        {
            var key = ArtifactKey(document);
            var spans = SecretRedactor.SpansFor(document.Identity.Id, view.Document.Diagnostics);
            var publication = new DocumentPublication(source, document, key, spans);
            fragments.Add(StagedFragment.Deferred(ArtifactRole.Payload, key, publication.SourceBytes));
            if (!spans.IsDefaultOrEmpty)
            {
                fragments.Add(StagedFragment.Deferred(
                    ArtifactRole.Payload,
                    key + ".meta.json",
                    publication.EnvelopeBytes));
            }
        }

        return fragments.ToImmutable();
    }

    private sealed class DocumentPublication
    {
        private readonly ISourceDocumentReader _source;
        private readonly DocumentDto _document;
        private readonly string _artifactKey;
        private readonly ImmutableArray<SourceSpanDto> _spans;
        private ImmutableArray<byte> _sourceBytes;
        private ImmutableArray<byte> _envelopeBytes;
        private bool _ready;

        public DocumentPublication(
            ISourceDocumentReader source,
            DocumentDto document,
            string artifactKey,
            ImmutableArray<SourceSpanDto> spans)
        {
            _source = source;
            _document = document;
            _artifactKey = artifactKey;
            _spans = spans;
        }

        public ImmutableArray<byte> SourceBytes()
        {
            Ensure();
            return _sourceBytes;
        }

        public ImmutableArray<byte> EnvelopeBytes()
        {
            Ensure();
            return _envelopeBytes;
        }

        private void Ensure()
        {
            if (_ready)
            {
                return;
            }

            var identity = DocumentId.Create(_document.Identity.Id);
            if (!_source.TryRead(identity, out var original) || original.IsDefault)
            {
                _sourceBytes = [];
                _envelopeBytes = [];
                _ready = true;
                return;
            }

            _sourceBytes = SecretRedactor.Redact(original, _spans);
            _envelopeBytes = _spans.IsDefaultOrEmpty
                ? []
                : RedactionEnvelope.Fragment(
                    _document.Identity.Id,
                    _artifactKey,
                    original,
                    _sourceBytes,
                    _spans).Payload;
            _ready = true;
        }
    }

    private static string ArtifactKey(DocumentDto document)
    {
        var relative = document.RelativePath.Replace('\\', '/');
        return "source/" + ProjectSlug(document.OwningProject) + "/" + relative;
    }

    private static string ProjectSlug(string owningProject)
    {
        const string marker = ";path=";
        var start = owningProject.IndexOf(marker, StringComparison.Ordinal);
        var encoded = start < 0 ? owningProject : owningProject[(start + marker.Length)..];
        var end = encoded.IndexOf(';');
        if (end >= 0)
        {
            encoded = encoded[..end];
        }

        var path = PercentDecode(encoded).Replace('\\', '/');
        var file = path[(path.LastIndexOf('/') + 1)..];
        if (file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            file = file[..^".csproj".Length];
        }

        return file.ToLowerInvariant();
    }

    private static string PercentDecode(string value)
    {
        var bytes = new List<byte>(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '%' && i + 2 < value.Length)
            {
                bytes.Add(byte.Parse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 2;
            }
            else
            {
                bytes.Add((byte)value[i]);
            }
        }

        return Encoding.UTF8.GetString([.. bytes]);
    }
}
