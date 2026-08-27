using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Source;

internal static class ProjectionPackageFactory
{
    internal static readonly ManifestContext Context = new("s-test", "Acme.sln");

    internal static (PublishedPackageView View, ISourceDocumentReader Reader, ImmutableArray<Document> Documents) PackageWith(
        params (string ProjectPath, string RelativePath, byte[] Bytes)[] files) =>
        Assemble([], files);

    internal static (PublishedPackageView View, ISourceDocumentReader Reader, Document Document) PackageWithSecret(
        string projectPath,
        string relativePath,
        string body,
        SourceSpanDto span)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, projectPath);
        var document = Document.Create(projectId, relativePath, HashBytes(bytes));
        var hash = DocumentHash.Create(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        var (view, reader, documents) = Assemble(
            [
                SuspectedSecretEvidence.Create(
                    DocumentId.Create(document.Reference.Id.Value),
                    new SourceSpan(span.StartLine, span.StartColumn, span.EndLine, span.EndColumn),
                    hash,
                    RedactedExcerpt.Create("Password=***")),
            ],
            (projectPath, relativePath, bytes));
        return (view, reader, Assert.Single(documents));
    }

    private static (PublishedPackageView View, ISourceDocumentReader Reader, ImmutableArray<Document> Documents) Assemble(
        ImmutableArray<SuspectedSecretEvidence> secrets,
        params (string ProjectPath, string RelativePath, byte[] Bytes)[] files)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var facts = ImmutableArray.CreateBuilder<IFact>();
        facts.Add(Solution.Create(solutionId));

        var bytesById = ImmutableDictionary.CreateBuilder<DocumentId, ImmutableArray<byte>>();
        var documents = ImmutableArray.CreateBuilder<Document>();
        foreach (var (projectPath, relativePath, bytes) in files)
        {
            var projectId = ProjectId.Create(solutionId, projectPath);
            facts.Add(Project.Create(projectId));
            var document = Document.Create(projectId, relativePath, HashBytes(bytes));
            facts.Add(document);
            documents.Add(document);
            bytesById[DocumentId.Create(document.Reference.Id.Value)] = [.. bytes];
        }

        var view = PublishedPackageView.From(
            DomainMapper.ToWire(
                new FactualSnapshot(facts.ToImmutable(), [], [], [], [], [], suspectedSecrets: secrets),
                Context));
        return (view, new DictionarySourceReader(bytesById.ToImmutable()), documents.ToImmutable());
    }

    private sealed class DictionarySourceReader : ISourceDocumentReader
    {
        private readonly ImmutableDictionary<DocumentId, ImmutableArray<byte>> _bytes;

        public DictionarySourceReader(ImmutableDictionary<DocumentId, ImmutableArray<byte>> bytes)
        {
            _bytes = bytes;
            Documents = [.. bytes.Keys.OrderBy(static id => id.Value, StringComparer.Ordinal)];
        }

        public ImmutableArray<DocumentId> Documents { get; }

        public bool TryRead(DocumentId document, out ImmutableArray<byte> bytes) =>
            _bytes.TryGetValue(document, out bytes);
    }

    private static DocumentHash HashBytes(byte[] bytes) =>
        DocumentHash.Create(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
}
