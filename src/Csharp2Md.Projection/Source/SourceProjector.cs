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
            var identity = DocumentId.Create(document.Identity.Id);
            fragments.Add(StagedFragment.Deferred(
                ArtifactRole.Payload,
                key,
                () => Read(source, identity)));
        }

        return fragments.ToImmutable();
    }

    private static ImmutableArray<byte> Read(ISourceDocumentReader source, DocumentId identity)
    {
        if (!source.TryRead(identity, out var bytes) || bytes.IsDefault)
        {
            return [];
        }

        return bytes;
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
