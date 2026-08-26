using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Microsoft.CodeAnalysis;
using DomainDocument = Csharp2Md.Domain.Facts.Document;

namespace Csharp2Md.Analysis.Extraction;

internal sealed record BoundOccurrence(
    SyntaxNode Node,
    SemanticModel Model,
    DomainDocument Document,
    DocumentHash DocumentHash,
    FactReference Owner,
    CancellationToken CancellationToken)
{
    public Compilation Compilation => Model.Compilation;
}
